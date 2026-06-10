using Crochik.Mongo;
using McpServer.Tools.Attributes;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;
using PI.Shared.Services;

namespace McpServer.Tools;

public class SchedulerTools(ILogger<SchedulerTools> logger, MongoConnection connection, AppointmentSchedulerService schedulerService)
{
    [McpTool(
        Name = "get_availability_for_organization",
        Description = "Get Availability to schedule a in-home consultation Appointments for Organization",
        ExamplePrompts =
        [
            "What appointment slots are available next week?",
            "Check availability for an organization from March 20 to March 25"
        ])
    ]
    public async Task<string> GetAvailabilityForOrganizationAsync(
        IEntityContext context,
        [McpParameter(Description = "Organization Id", Required = true)]
        string organizationId,
        [McpParameter(Description = "Start Date (in the future, ISO 8601)", Required = true)]
        DateTime startDate,
        [McpParameter(Description = "End Date (max 30 days from start date, ISO 8601)", Required = true)]
        DateTime endDate
    )
    {
        if (startDate < DateTime.UtcNow)
        {
            return "Error: Invalid start date, should be in the future";
        }

        if (startDate > endDate || (endDate - startDate).TotalDays > 30)
        {
            return "Error: Invalid end date, must be at most 30 days from start date";
        }

        if (!Guid.TryParse(organizationId, out var id))
        {
            return "Error: Invalid Organization ID";
        }

        var orgQuery = connection.Filter<Entity, Organization>()
            .Eq(x => x.Id, id)
            .Ne(x => x.IsActive, false);

        var organization = await orgQuery.FirstOrDefaultAsync();
        if (organization == null)
        {
            logger.LogInformation("{OrganizationId} not found/inactive", id);
            return "Error: Organization not available.";
        }

        var query = connection.Filter<SchedulerSettings>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .In(x => x.ClientId, new[] { null, context.ClientId })
                .Eq(x => x.EntityId, organization.Id)
                .SortDesc(x => x.ClientId) // prefer match to client, null is a fallback
            ;

        var settings = await query.FirstOrDefaultAsync();

        if (!settings.IsActive)
        {
            logger.LogInformation("{SchedulerSettingsId} for {EntityId}: {OutOfServiceMessage} ", settings.Id, organization.Id, settings.OutOfServiceMessage);
            return "No Availability: " + (settings.OutOfServiceMessage ?? "Not Available");
        }

        var session = new SchedulerSession
        {
            Id = Model.NewGuid(),
            AccountId = context.AccountId.Value,
            // ExternalId = jti,
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            // Referer = referer
            EntityId = organization.Id,
            Name = settings.Name,
            TimeZoneId = organization.TimeZoneId,
            Entity = organization,
            Settings = settings,
        };

        await connection.InsertAsync(session);

        var slots = (await schedulerService.GetSlotsAsync(context, session, startDate, endDate))?.ToArray();
        if (slots?.IsEmpty() ?? true)
        {
            return "Sorry. We are fully booked";
        }

        return string.Join("\n", slots.Select(x => $"- {x.Start}"));
    }

    [McpTool(
        Name = "schedule_appointment",
        Description = "Schedule Appointment for a customer in the organization"
    )]
    public async Task<string> GetAvailabilityForOrganizationAsync(
        IEntityContext context,
        [McpParameter(Description = "Organization Id", Required = true)]
        string organizationId,
        [McpParameter(Description = "Customer Id", Required = true)]
        string customerId,
        [McpParameter(Description = "Appointment Start Date/Time (ISO 8601)", Required = true)]
        DateTime startDateAndTime
    )
    {
        await Task.CompletedTask;
        return "Scheduled";
    }

    [McpTool(
        Name = "find_customer",
        Description = "Find Customer in the organization using name, phone number or email address"
    )]
    public async Task<string> FindCustomerAsync(
        IEntityContext context,
        [McpParameter(Description = "Organization Id", Required = true)]
        string organizationId,
        [McpParameter(Description = "Phone", Required = false)]
        string phoneNumber,
        [McpParameter(Description = "Email", Required = false)]
        string email,
        [McpParameter(Description = "Name", Required = false)]
        string name
    )
    {
        await Task.CompletedTask;
        return "Didn't find a customer, search again or offer to create a new one";
    }

    [McpTool(
        Name = "create_customer",
        Description = "Create customer in an organization"
    )]
    public async Task<string> CreateCustomerAsync(
        IEntityContext context,
        [McpParameter(Description = "Organization Id", Required = true)]
        string organizationId,
        [McpParameter(Description = "Phone", Required = true)]
        string phoneNumber,
        [McpParameter(Description = "Email", Required = true)]
        string email,
        [McpParameter(Description = "Name", Required = true)]
        string name,
        [McpParameter(Description = "Full Address", Required = true)]
        string fullAddress
    )
    {
        await Task.CompletedTask;
        return "Customer created. Customer ID: " + Model.NewGuid();
    }

}