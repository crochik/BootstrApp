using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Extensions;
using Crochik.Mongo;
using MongoDB.Bson;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Layout;
using PI.Shared.Services;

namespace Controllers;

public class CalendarViewBuilder
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private IEntityContext _context;
    private DataViewRequest _request;
    private bool _agenda;
    private Guid? _leadId;
    private DateTime? _startDate;
    private DateTime? _endDate;
    private Guid? _organizationId;
    private Guid[] _userIds;
    private bool _isAdmin;
    private PI.Shared.Models.ObjectType _objectType;
    private bool _includeEvents;

    public CalendarViewBuilder(
        MongoConnection connection,
        ObjectTypeService objectTypeService
    )
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    private IEnumerable<CalendarView> Views()
    {
        if (!_agenda)
        {
            yield return new()
            {
                Name = "Day",
                Type = CalendarViewType.Day,
            };

            if ((_request.Breakpoint ?? ScreenBreakpoint.Large) > ScreenBreakpoint.Small)
            {
                yield return new()
                {
                    Name = "7 Days",
                    Type = CalendarViewType.SevenDays,
                    Group = "UserId",
                };
            }

            yield return new()
            {
                Name = "Work Week",
                Type = CalendarViewType.MondayToFriday,
            };

            yield return new()
            {
                Name = "Week",
                Type = CalendarViewType.Week,
            };

            yield return new()
            {
                Name = "Month",
                Type = CalendarViewType.Month,
            };
        }

        yield return new()
        {
            Name = "Schedule",
            Type = CalendarViewType.Agenda,
        };
    }

    private IEnumerable<FormField> Fields()
    {
        if (!_organizationId.HasValue || _isAdmin)
        {
            yield return new ReferenceField
            {
                Name = nameof(PI.Shared.Models.User.OrganizationId),
                Label = "Organization",
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = nameof(Organization),
                    Criteria = new[]
                    {
                        Condition.Eq(nameof(Entity.IsActive), true),
                    },
                },
                DefaultValue = _organizationId
            };

            if (!_organizationId.HasValue) yield break;
        }

        yield return new MultiReferenceField
        {
            Name = nameof(Appointment.EntityId),
            Label = "User",
            MultiReferenceFieldOptions = new MultiReferenceFieldOptions
            {
                ObjectType = nameof(User),
                Criteria = new[]
                {
                    Condition.Eq(nameof(Entity.IsActive), true),
                    Condition.Eq(nameof(User.OrganizationId), _organizationId),
                }
            },
            DefaultValue = _organizationId.HasValue && _userIds?.Length > 0 ? _userIds : null,
            Visible = _organizationId.HasValue ? null : new[] { "false" },
        };

        yield return new DateRangeField
        {
            Name = "Start",
            Label = "Date",
            DateRangeFieldOptions = new DateRangeFieldOptions
            {
                Min = DateTime.UtcNow.AddDays(-7).ToShortDateString(),
                Max = DateTime.UtcNow.AddDays(30).ToShortDateString(),
                // Presets = new[]
                // {
                //     new DateRangePreset
                //     {
                //         Name = "Next 7 days",
                //         Range = new[]
                //         {
                //             DateTime.UtcNow.ToShortDateString(),
                //             DateTime.UtcNow.AddDays(7).ToShortDateString()
                //         }
                //     }
                // }
            },
            Visible = _agenda || _organizationId.HasValue ? new[] { "false" } : null,
            DefaultValue = new[]
            {
                _startDate,
                _endDate,
            },
        };

        yield return new CheckboxField
        {
            Name = "IncludeEvents",
            Label = "Include Other Events",
            CheckboxFieldOptions = new CheckboxFieldOptions
            {
                Style = CheckboxFieldOptionsStyle.Toggle,
            },
            DefaultValue = _includeEvents,
            Visible = _organizationId.HasValue ? null : new[] { "false" },
        };
    }

    public async Task<IDataViewResponse> BuildAsync(IEntityContext context, DataViewRequest request, bool agenda, Guid? leadId = null)
    {
        _context = context;
        _request = request;
        _agenda = agenda;
        _leadId = leadId;

        _objectType = await _objectTypeService.GetAsync(_context, nameof(Appointment));

        ParseInput();

        return await BuildAsync();
    }

    private void ParseInput()
    {
        _isAdmin = _context.Role == EntityRoleId.Account || _context.Role == EntityRoleId.Admin;

        var criteria = _request.Criteria ?? Array.Empty<Condition>();

        _includeEvents = criteria.FirstOrDefault(x => x.FieldName == "IncludeEvents")?.Value switch
        {
            bool b => b,
            string str => !bool.TryParse(str, out var b) || b,
            _ => true,
        };

        if (_agenda)
        {
            // next 30 days
            _startDate = DateTime.UtcNow.Date;
            _endDate = _startDate.Value.AddDays(30);
        }
        else
        {
            // use criteria
            var startCriteria = criteria.FirstOrDefault(x => x.FieldName == nameof(Appointment.Start) && (x.Operator == Operator.Gt || x.Operator == Operator.Gte))?.Value;
            var endCriteria = criteria.FirstOrDefault(x => x.FieldName == nameof(Appointment.Start) && (x.Operator == Operator.Lt || x.Operator == Operator.Lte))?.Value;

            _startDate = startCriteria switch
            {
                DateTime dt => dt,
                string str => DateTime.TryParse(str, out var dt) ? dt : default(DateTime?), // TODO: should convert to TZ?
                _ => default(DateTime?)
            };

            // TODO: should convert to TZ?
            // ... 
            _startDate ??= DateTime.UtcNow.Date;

            _endDate = endCriteria switch
            {
                DateTime dt => dt,
                string str => DateTime.TryParse(str, out var dt) ? dt : default(DateTime?), // TODO: should convert to TZ?
                _ => default(DateTime?)
            };

            _endDate ??= _startDate.Value.AddDays(30);

            if ((_endDate.Value - _startDate.Value).Days > 31) _endDate = _startDate.Value.AddDays(31);
        }

        _organizationId = criteria.FirstOrDefault(x => x.FieldName == nameof(User.OrganizationId))?.Value.TryToParseObjectId(out var guid) ?? false ? guid : null;
        _organizationId = _context.Role switch
        {
            EntityRoleId.Account or EntityRoleId.Admin => _organizationId,
            EntityRoleId.Organization or EntityRoleId.Manager or EntityRoleId.User => _context.OrganizationId,
            _ => null,
        };

        _userIds = criteria.FirstOrDefault(x => x.FieldName == nameof(Appointment.EntityId))?.Value switch
        {
            string str => Guid.TryParse(str, out var uuid) ? new[] { uuid } : null,
            Guid uuid => [uuid],
            IEnumerable e => e.ToEnumerableObject()
                .Select(x => x.TryToParseObjectId(out var uuid) ? uuid : default(Guid?))
                .Where(x => x != null)
                .Select(x => x.Value)
                .ToArray(),
            _ => null,
        };
        _userIds = _context.Role switch
        {
            EntityRoleId.Account or EntityRoleId.Admin or EntityRoleId.Manager or EntityRoleId.Organization => _userIds,
            EntityRoleId.User => [_context.UserId.Value],
            _ => null,
        };
    }

    private async Task<IDataViewResponse> BuildAsync()
    {
        var response = new DataViewResponse
        {
            Request = _request,
            Options = new CalendarViewOptions
            {
                // TODO: use org settings?
                // ...
                StartHour = 7,
                EndHour = 20,
                Views = Views().ToArray(),
            },
            View = new DataView
            {
                Name = _agenda ? "Agenda" : "Calendar",
                Title = _agenda ? "Agenda" : "Calendar",
                Fields = new FormField[]
                {
                    new TextField
                    {
                        Name = "_id",
                        Label = "Id",
                    },
                    new TextField
                    {
                        Name = "Name",
                    },
                    new DateTimeField
                    {
                        Name = "Start",
                    },
                    new DateTimeField
                    {
                        Name = "End",
                    },
                    new ReferenceField
                    {
                        Name = "UserId",
                        Label = "User",
                        ReferenceFieldOptions = new ReferenceFieldOptions
                        {
                            ObjectType = nameof(PI.Shared.Models.User),
                            LinkUrl = "dataForm://api/v1/CustomObject/User({{value}})/View",
                        }
                    },
                    new ReferenceField
                    {
                        Name = "AppointmentId",
                        Label = "Appointment",
                        ReferenceFieldOptions = new ReferenceFieldOptions
                        {
                            ObjectType = nameof(Appointment),
                            LinkUrl = "dataForm://api/v1/CustomObject/Appointment({{value}})/View"
                        },
                    },
                    new ReferenceField
                    {
                        Name = "LeadId",
                        Label = "Lead",
                        ReferenceFieldOptions = new ReferenceFieldOptions
                        {
                            ObjectType = nameof(Lead),
                            LinkUrl = "dataForm://api/v1/CustomObject/Lead({{value}})/View"
                        },
                    },
                    new TextField
                    {
                        Name = "Type",
                    },
                },
                // Detail = new DataViewDetail
                // {
                //     // TODO: not really right as it won't work for all rows 
                //     Page = "dataForm:/api/v1/CustomObject/Appointment({{id}})/View",
                // },
                DefaultSort = "Start",
                KeyField = "_id",
                IsSelectable = false,
                // Filter = new[] { "" },
                FilterForm = new Form
                {
                    Name = "Filter",
                    Title = "Filter",
                    Fields = Fields().ToArray(),
                },
            }
        };

        var menuActions = Actions().ToArray();
        if (menuActions.Length > 0)
        {
            response.View.Menu = new Menu
            {
                Name = "Edit",
                Items = menuActions,
            };
        }

        _request.Fields = response.View.Fields.Select(x => x.Name).ToArray();

        if (!_organizationId.HasValue)
        {
            response.Message = "EntityId required";
            response.Result = Array.Empty<object>();
            return response;
        }

        var usersQuery = _connection.Filter<Entity, User>()
                .Eq(x => x.AccountId, _context.AccountId)
                .Eq(x => x.OrganizationId, _organizationId)
                .Ne(x => x.IsActive, false)
            ;
        if (_userIds != null && _userIds.Length > 0)
        {
            usersQuery.In(x => x.Id, _userIds);
        }

        var users = await usersQuery.FindAsync();
        if (users == null)
        {
            response.Message = "Bad request";
            response.Result = Array.Empty<object>();
            return response;
        }

        var usersDict = users.ToDictionary(x => x.Id);
        var entities = usersDict.Keys;

        var result = new List<Dictionary<string, object>>();

        if (_includeEvents)
        {
            var events = await _connection.Filter<O365Event>()
                .Eq(x => x.AccountId, _context.AccountId)
                .In(x => x.EntityId, entities)
                .Eq(x => x.ShowAs, FreeBusyStatus.Busy)
                .Gt(x => x.End, _startDate)
                .Lt(x => x.Start, _endDate)
                .Eq(x => x.IsCancelled, false)
                .Eq(x => x.AppointmentId, null) // filter out known appts
                .FindAsync();

            foreach (var evt in events)
            {
                result.Add(new Dictionary<string, object>
                {
                    { "_id", evt.Id },
                    { "Name", evt.Name },
                    { "Start", evt.Start },
                    { "End", evt.End },
                    { "UserId", evt.EntityId },
                    { "UserId|Name", usersDict[evt.EntityId].Name },
                    { "AppointmentId", evt.AppointmentId },
                    { "Type", "Event" },
                });
            }
        }

        var appts = await _connection.Filter<Appointment>()
            .Eq(x => x.AccountId, _context.AccountId)
            .In(x => x.EntityId, entities)
            .Eq(x => x.CancelledOn, null)
            .Gt(x => x.End, _startDate)
            .Lt(x => x.Start, _endDate)
            .FindAsync();

        foreach (var appt in appts)
        {
            result.Add(new Dictionary<string, object>
            {
                { "_id", appt.Id },
                { "Name", appt.Name },
                { "Start", appt.Start },
                { "End", appt.End },
                { "UserId", appt.EntityId },
                { "UserId|Name", usersDict[appt.EntityId].Name },
                { "AppointmentId", appt.Id },
                { "AppointmentId|Name", appt.Name },
                { "LeadId", appt.LeadId },
                { "Type", nameof(Appointment) },
            });
        }

        result.Sort((x, y) =>
        {
            var xDate = (DateTime)x["Start"];
            var yDate = (DateTime)y["Start"];
            return xDate.CompareTo(yDate);
        });

        response.Result = result;

        if (_agenda)
        {
            response.Request.Criteria = (response.Request.Criteria ?? Enumerable.Empty<Condition>())
                .Where(x => x.FieldName != nameof(Appointment.Start))
                .Append(Condition.New(nameof(Appointment.Start), Operator.Gte, DateTime.UtcNow))
                .Append(Condition.New(nameof(Appointment.Start), Operator.Lte, DateTime.UtcNow.AddDays(30)))
                .ToArray();
        }

        return response;
    }

    private IEnumerable<MenuItem> Actions()
    {
        // if (_organizationId.HasValue && !_leadId.HasValue)
        // {
        //     var uri = "dataGrid:/api/v1/Calendar";
        //     if (!_agenda) uri += "/Agenda";
        //     uri += $"?OrganizationId={_organizationId}";
        //
        //     yield return new ActionMenuItem
        //     {
        //         Name = "ToggleView",
        //         Label = _agenda ? "Calendar" : "Agenda",
        //         Action = uri,
        //         Icon = _agenda ? Icons.Calendar : Icons.Agenda,
        //         Visible = new[]
        //         {
        //             "selectedCount=='0'"
        //         },
        //     };
        // }

        if (!_agenda)
        {
            // view
            yield return new ActionMenuItem
            {
                Name = "View",
                Label = "Open...",
                Visible = new[]
                {
                    "selectedCount=='1'",
                    "Type=='Appointment'"
                },
                // Action = GetDataFormUrl(objectType.Name, formName: FormName.View),
                Action = "dataForm:/api/v1/CustomObject/Appointment({{id}})/View",
                Icon = nameof(Icons.CalendarEvent),
            };
        }

        // // lead
        // yield return new ActionMenuItem
        // {
        //     Name = "Lead...",
        //     Visible = new[]
        //     {
        //         "selectedCount=='1'",
        //         "Type=='Appointment'"
        //     },
        //     // Action = GetDataFormUrl(objectType.Name, formName: FormName.View),
        //     Action = "dataForm:/api/v1/CustomObject/Lead({{LeadId}})/View",
        //     Icon = Icons.Contact,
        // };

        // cancel
        if (_objectType.RBAC.Can(_context, ObjectTypePermission.Update))
        {
            yield return new ActionMenuItem
            {
                Name = "Cancel",
                Label = "Cancel Appointment...",
                Action = "dataForm:/api/v1/Scheduler/Appointment({{id}})/Cancel",
                Visible = new[]
                {
                    "selectedCount=='1'",
                    "Type=='Appointment'"
                },
                Icon = nameof(Icons.Delete),
            };
        }

        if (_objectType.CanCreate(_context))
        {
            // Schedule
            yield return new ActionMenuItem
            {
                Name = FormAction.Add,
                Label = "Schedule...",
                Visible = new[] { "selectedCount=='0'" },
                // Action = ObjectTypeService.GetDataFormUrl(objectType.Name, formName: FormName.Add),
                Action = _leadId.HasValue ? $"dataForm:/api/v1/Scheduler/Lead({_leadId})/Appointment" : "dataForm:/api/v1/Scheduler/Appointment",
                Icon = nameof(Icons.Add),
            };

            if (_objectType.RBAC.Can(_context, ObjectTypePermission.Update))
            {
                // Reschedule
                yield return new ActionMenuItem
                {
                    Name = FormAction.Add,
                    Label = "Reschedule...",
                    Visible = new[]
                    {
                        "selectedCount=='1'",
                        "Type=='Appointment'"
                    },
                    // Action = ObjectTypeService.GetDataFormUrl(objectType.Name, formName: FormName.Add),
                    Action = _leadId.HasValue ? $"dataForm:/api/v1/Scheduler/Lead({_leadId})/Appointment(" + "{{id}})" : "dataForm:/api/v1/Scheduler/Appointment({{id}})",
                    Icon = nameof(Icons.CalendarEvent),
                };
            }
        }

        // generic object actions
        // menuActions.AddRange(ObjectTypeService
        //     .GetEditActions(Context, objectType)
        //     .Select(x =>
        //     {
        //         if (x.Visible?.Length == 1 && x.Visible[0] == "selectedCount=='1'")
        //         {
        //             x.Visible = x.Visible
        //                 .Append("Type=='Appointment'")
        //                 .ToArray();
        //         }
        //
        //         return x;
        //     })
        // );        
    }
}