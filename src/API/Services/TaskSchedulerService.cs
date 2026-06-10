using System.Dynamic;
using Crochik.Logging;
using Crochik.Messaging;
using Crochik.Mongo;
using Messages.Flow;
using MongoDB.Driver;
using NCrontab;
using PI.Shared.App;
using PI.Shared.Constants;
using PI.Shared.Data.Adapters;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;

namespace Services;

public class TaskSchedulerService : AbstractMessageQueueService, ILifetimeService
{
    private readonly MongoConnection _connection;
    private readonly IEventTypeAdapter _eventTypeAdapter;
    private readonly ObjectTypeService _objectTypeService;
    private readonly TimeSpan SleepTime = TimeSpan.FromMinutes(1);
    private bool _stop;
    private readonly bool _enabled;

    public TaskSchedulerService(
        ILogger<TaskSchedulerService> logger,
        MongoConnection connection,
        IConfiguration configuration,
        IMessageBroker messageBroker,
        // IAPMService apmService,
        IEventTypeAdapter eventTypeAdapter,
        ObjectTypeService objectTypeService
    ) : base(logger, configuration, messageBroker)
    {
        _connection = connection;
        _eventTypeAdapter = eventTypeAdapter;
        _objectTypeService = objectTypeService;
        _enabled = configuration.GetSection(GetType().Name).GetValue<bool>("Enabled");
    }

    protected override void Init(IMessageQueue queue, TypeMapper mapper)
    {
        MessageBroker.Bind(queue, ActionIds.GetRoute(ActionIds.DelayEvent));
        mapper.Register<DelayAction.Message>();
    }

    public override void Start()
    {
        if (!_enabled)
        {
            Logger.LogInformation("Task Scheduler is disabled");
            return;
        }

        base.Start();

        _stop = false;
        Task.Run(SchedulerAsync);
    }

    public override void Stop()
    {
        _stop = true;

        base.Stop();
    }

    private async Task SchedulerAsync()
    {
        while (!_stop)
        {
            // run batch
            try
            {
                await RunScheduledEventsAsync();
                
                while (!_stop)
                {
                    if (!await RunNextTaskAsync()) break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to run tasks");
            }

            // sleep
            await Task.Delay(SleepTime);
        }
    }

    private async Task<bool> RunScheduledEventsAsync()
    {
        var list = await _connection.Filter<EventType>()
            .OfTypeBuilder<EventType, Trigger, ScheduledTrigger>(
                x => x.Trigger,
                q => q
                    .Lt(x => x.Start, DateTime.UtcNow)
                    .Ne(x => x.IsActive, false)
            )
            .FindAsync();

        foreach (var evt in list)
        {
            var trigger = (ScheduledTrigger)evt.Trigger;
            var schedule = CrontabSchedule.TryParse(trigger.Schedule);
            if (schedule == null)
            {
                Logger.LogError("Invalid {Schedule} for {EventTypeId}", trigger.Schedule, evt.Id);
                continue;
            }

            var next = schedule.GetNextOccurrence(DateTime.UtcNow);
            var update = await _connection.Filter<EventType>()
                .Eq(x => x.Id, evt.Id)
                .OfTypeBuilder<EventType, Trigger, ScheduledTrigger>(
                    x => x.Trigger,
                    q => q
                        .Lt(x => x.Start, DateTime.UtcNow)
                        .Ne(x => x.IsActive, false)
                )
                .Update
                .Set(x => ((ScheduledTrigger)x.Trigger).Start, next)
                .UpdateAndGetOneAsync();

            if (update == null)
            {
                Logger.LogError("Failed to update {EventTypeId} with next iteration", evt.Id);
                continue;
            }

            await ProcessAsync(update);
        }

        return false;
    }

    private async Task ProcessAsync(EventType eventType)
    {
        var trigger = (ScheduledTrigger)eventType.Trigger;

        using var scope = Logger.AddScope(new
        {
            eventType.AccountId,
            eventType.ObjectType,
            trigger.ObjectStatusId,
            trigger.Start,
            trigger.Schedule,
        });
        
        Logger.LogInformation("Process Scheduled Event");
        
        var criteria = trigger.Criteria ?? Enumerable.Empty<Condition>();
        
        if (trigger.FlowId.HasValue)
        {
            // only applies to one flow
            criteria = criteria.Append(Condition.Eq(nameof(FlowObjectModel.FlowId), trigger.FlowId.Value));
        }
        else
        {
            // find flows where it is used
            var query = _connection.Filter<Flow>()
                .Eq(x => x.AccountId, eventType.AccountId)
                .Eq(x => x.ObjectType, eventType.ObjectType);

            if (trigger.ObjectStatusId.HasValue)
            {
                query.ElemMatchBuilder(
                    x => x.Steps,
                    q => q
                        .Eq(x => x.EventIdTrigger, eventType.Id)
                        .Eq(x => x.CurrentStatusId, trigger.ObjectStatusId.Value)
                );
            }
            else
            {
                query.ElemMatchBuilder(x => x.Steps, q => q.Eq(x => x.EventIdTrigger, eventType.Id));
            }

            var flows = await query.FindAsync();
            if (flows.Count < 1) return;

            Logger.LogInformation("Found {Flows}", flows.Count);
            
            criteria = flows.Count switch
            {
                1 => criteria.Append(Condition.Eq(nameof(FlowObjectModel.FlowId), flows[0].Id)),
                _ => criteria.Append(Condition.In(nameof(FlowObjectModel.FlowId), flows.Select(x => x.Id).ToArray())),
            };
        }
        
        if (trigger.ObjectStatusId.HasValue)
        {
            criteria = criteria.Append(Condition.Eq(nameof(FlowObjectModel.ObjectStatusId), trigger.ObjectStatusId.Value));
        }
        
        var context = new AccountContext(eventType.AccountId);
        var objectType = await _objectTypeService.GetAsync(context, eventType.ObjectType);

        var stages = AppDataViewPipelineBuilder.New(
            _connection, 
            context, 
            new AppDataView
            {
                ObjectType = objectType.Name,
                Criteria = new Criteria
                {
                    Conditions = criteria.ToArray(),
                },
                Fields = new []
                {
                    Model.IdFieldName,
                    nameof(Model.Name),
                    nameof(FlowObjectModel.ObjectStatusId),
                    nameof(FlowObjectModel.FlowId),
                },
                OrderBy = Model.IdFieldName,
            }, 
            objectType
        )
        .BuildPipeline();

        var pipeline = PipelineDefinition<ExpandoObject, ExpandoObject>.Create(stages);

        var objs = await _connection.Database
            .GetCollection<ExpandoObject>(objectType.CollectionName)
            .Aggregate(pipeline)
            .ToListAsync();

        foreach (var obj in objs)
        {
            if (!obj.TryGetGuidParam(Model.IdFieldName, out var id))
            {
                Logger.LogError("Couldn't find id for object");
                continue;
            }
            
            if (!obj.TryGetGuidParam(nameof(FlowObjectModel.FlowId), out var flowId))
            {
                Logger.LogError("Couldn't find id for {ObjectId}", id);
                continue;
            }

            var objectStatusId = obj.TryGetGuidParam(nameof(FlowObjectModel.ObjectStatusId), out var statusId) ? statusId : default(Guid?);
            
            Logger.LogInformation("Fire event for {ObjectId}: {FlowId} {ObjectStatusId}", id, flowId, objectStatusId);
            
            var evt = new GenericFlowEvent
            {
                ObjectType = objectType.FullName,
                TargetId = id,
                AccountId = eventType.AccountId,
                StatusId = objectStatusId,
                FlowId = flowId,
                EventTypeId = eventType.Id,
                Description = eventType.Description,
                Action = "ScheduledTask",
                // Actor = context.Actor(),
            };

            await MessageBroker.DispatchAsync(evt);
        }
    }

    private async Task<bool> RunNextTaskAsync()
    {
        var task = await GetLockAsync();
        if (task == null)
        {
            return false;
        }

        if (task is not PostEventScheduledTask postEventTask)
        {
            Logger.LogError("Don't know how to handle {TaskId} of {TaskType}", task.Id, task.GetType().FullName);
            return false;
        }

        Logger.LogInformation("Found {TaskId}", task.Id);

        try
        {
            await PostEventAsync(postEventTask);
            await MarkTaskCompletedAsync(postEventTask);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error running {ScheduledTaskId}", postEventTask.Id);
            await MarkAsFailedAsync(postEventTask, ex.Message);
        }

        if (postEventTask.AutoReschedule != null)
        {
            var schedule = CrontabSchedule.TryParse(postEventTask.AutoReschedule);
            if (schedule == null)
            {
                Logger.LogError("Couldn't parse crontab schedule for {TaskId}: {Value}", postEventTask.Id, postEventTask.AutoReschedule);
                return true;
            }

            // for now only 
            var repeat = new PostEventScheduledTask
            {
                Id = Guid.NewGuid(),
                Tag = postEventTask.Tag,
                CreatedOn = DateTime.UtcNow,
                Event = new GenericFlowEvent(postEventTask.Event)
                {
                    RunId = Guid.NewGuid(),
                },
                Time = schedule.GetNextOccurrence(DateTime.UtcNow),
            };

            await _connection.InsertAsync(repeat);

            Logger.LogInformation("New {TaskId} Scheduled for {Time}", repeat.Id, repeat.Time);

            // TODO: post event?
            // ...
        }

        return true;
    }


    private async Task PostEventAsync(PostEventScheduledTask task)
    {
        Logger.LogInformation("Processing {TaskId} scheduled for {Time}", task.Id, task.Time);

        var evt = new GenericFlowEvent(task.Event);

        await MessageBroker.DispatchAsync(evt);
    }

    protected override async Task OnMessageAsync(IMessage evt)
    {
        try
        {
            switch (evt.Body)
            {
                case DelayAction.Message delayedEvent:
                    await SaveAsync(delayedEvent);
                    break;

                default:
                    Logger.LogError("Unexpected message");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process action");
        }
        finally
        {
            evt.Acknowledge();
        }
    }

    private async Task SaveAsync(DelayAction.Message action)
    {
        using var scope = Logger.AddScope(new
        {
            action.Options.DelayedEventId,
            action.Options.Tag,
        });

        Logger.LogInformation("Schedule Task");

        if (!action.Options.DelayedEventId.HasValue)
        {
            Logger.LogInformation("Missing Event. Nothing to do.");
            return;
        }

        var description = action.GetEventDescription(action.Options.DelayedEventId);
        if (string.IsNullOrEmpty(description))
        {
            var eventType = await _eventTypeAdapter.GetByIdAsync(action.Options.DelayedEventId.Value);
            description = eventType.Name;
        }

        // ?!?!?!?!
        var appointment = (action.Event as LeadWithAppointmentEvent)?.Appointment?.Appointment;

        var baseDate = action.Options.Anchor switch
        {
            DelayActionOptions.Anchors.Appointment => action.Options.When == DelayActionOptions.BeforeAfter.Before ?
                appointment.Start :
                appointment.End,
            _ => DateTime.UtcNow
        };

        var task = new PostEventScheduledTask
        {
            Id = Guid.NewGuid(),
            AccountId = action.Event.AccountId,
            CreatedOn = DateTime.UtcNow,
            Tag = action.Options.Tag,
            Name = description,
            Event = new GenericFlowEvent(action.Event)
            {
                EventTypeId = action.Options.DelayedEventId,
                Action = nameof(ActionIds.DelayEvent),
                Description = "Delayed Event",
            },
            Time = baseDate.Add(action.Options.GetTimeSpan()),
        };

        if (!string.IsNullOrEmpty(action.Options.Tag))
        {
            // cancel any matching tasks
            await _connection.Filter<ScheduledTask, PostEventScheduledTask>()
                .Eq(x => x.Event.AccountId, task.Event.AccountId)
                .Eq(x => x.Event.ObjectType, task.Event.ObjectType)
                .Eq(x => x.Event.TargetId, task.Event.TargetId)
                .Eq(x => x.Tag, task.Tag)
                .Eq(x => x.Finished, null)
                .Update
                .Set(x => x.Finished, DateTime.UtcNow)
                .Set(x => x.Error, "Rescheduled")
                .UpdateManyAsync();
        }

        await _connection.InsertAsync<ScheduledTask>(task);

        Logger.LogInformation("Scheduled {TaskId} for {Time}", task.Id, task.Time);

        // TODO: check if it is in the very near future and run if it is
        // ...
    }

    private async Task<ScheduledTask> GetLockAsync()
    {
        var result = await _connection.Filter<ScheduledTask>()
            .Lte(x => x.Time, DateTime.UtcNow)
            .Eq(x => x.Started, null)
            .Eq(x => x.Finished, null)
            .SortAsc(x => x.Time)
            .Update
            .Set(x => x.Started, DateTime.UtcNow)
            .UpdateAndGetOneAsync();

        return result;
    }

    private async Task MarkTaskCompletedAsync(ScheduledTask task)
    {
        var result = await _connection.Filter<ScheduledTask>()
            .Eq(x => x.Id, task.Id)
            .Update
            .Set(x => x.Finished, DateTime.UtcNow)
            .UpdateAndGetOneAsync();
    }

    private async Task MarkAsFailedAsync(ScheduledTask task, string error)
    {
        var result = await _connection.Filter<ScheduledTask>()
            .Eq(x => x.Id, task.Id)
            .Update
            .Set(x => x.Error, error)
            .Set(x => x.Finished, DateTime.UtcNow)
            .UpdateAndGetOneAsync();
    }
}