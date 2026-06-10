using System;
using System.Dynamic;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Ical.Net.CalendarComponents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using Appointment = PI.Shared.Models.Appointment;

namespace Controllers;

[Route("/api/v1/[controller]")]
[Authorize("default")]
public class AppointmentController : APIController
{
    private readonly ILogger<AppointmentController> _logger;
    private readonly MongoConnection _connection;

    public AppointmentController(
        ILogger<AppointmentController> logger,
        MongoConnection connection
    )
    {
        _logger = logger;
        _connection = connection;
    }

    [AllowAnonymous]
    [HttpGet("/api/v1/[controller]({id})/ical")]
    public async Task<IActionResult> GetICalFileAsync(Guid id)
    {
        using var scope = _logger.AddScope(new
        {
            AppointmentId = id,
        });

        _logger.LogInformation("Get iCal ile for appointment");

        var appointment = await _connection.Filter<Appointment>()
            .Eq(x => x.Id, id)
            .Ne(x => x.IsActive, false)
            // .Gte(x => x.Start, DateTime.UtcNow)
            .FirstOrDefaultAsync();

        if (appointment == null) throw NotFoundException.New<Appointment>(id);

        var appointmentType = await _connection.Filter<AppointmentType>()
            .Eq(x => x.AccountId, appointment.AccountId)
            .Eq(x => x.Id, appointment.AppointmentTypeId)
            .FirstOrDefaultAsync();

        var entity = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, appointment.AccountId)
            .Eq(x => x.Id, appointment.EntityId)
            .FirstOrDefaultAsync();
        
        var lead = await _connection.Filter<Lead>()
            .Eq(x=>x.AccountId, appointment.AccountId)
            .Eq(x=>x.Id, appointment.LeadId)
            .FirstOrDefaultAsync();

        var context = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(new
        {
            Appointment = new
            {
                appointment.Id,
                appointment.LeadId,
                appointment.EntityId,
                appointment.Name,  
                appointment.LocalDate,
                appointment.LocalTime,
                appointment.TimeZoneId,
            },
            Entity = new
            {
                entity.Id,
                entity.Name,
                entity.Email,
            },
            Lead = new
            {
                lead.Id,
                lead.Name,
                lead.FirstName,
                lead.LastName,
                Email = lead.NormalizedEmail,
                PhoneNumber = lead.NormalizedPhoneNumber,
                lead.Address,
                lead.City,
                lead.State,
                lead.Country,
                lead.PostalCode,
            }
            // Organization
        }));
        
        var calendar = new Ical.Net.Calendar();
        calendar.AddTimeZone(new VTimeZone(appointment.TimeZoneId));
        calendar.Events.Add(new Ical.Net.CalendarComponents.CalendarEvent
        {
            Uid = $"{id}@ProgramInterface.com",
            Summary = resolve(appointmentType.ICalSummary) ?? appointment.Name,
            Description = resolve(appointmentType.ICalDescription), 
            Start = new Ical.Net.DataTypes.CalDateTime(appointment.Start),
            End = new Ical.Net.DataTypes.CalDateTime(appointment.End),
        });

        var iCal = new Ical.Net.Serialization.CalendarSerializer().SerializeToString(calendar);
        return Content(iCal, "text/calendar");
        
        string resolve(string token)
        {
            if (token == null) return null;
            if (!token.Contains("{{")) return token;
            return HandlebarsDotNet.Handlebars.Compile(token).Invoke(context);
        }
    }
}