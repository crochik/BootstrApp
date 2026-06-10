// using System;
// using System.Collections.Generic;
// using AutoMapper;
// using Models;

// namespace Controllers.Models
// {
//     public class EventsAndSlots
//     {
//         // public string AppointmentTypeId { get; set; }
//         public Guid EntityId { get; set; }
//         public DateTime Start { get; set; }
//         public DateTime End { get; set; }
//         public List<TimeSlot> Slots { get; set; }
//         public IEnumerable<PI.Shared.Models.CalendarEvent> Events { get; set; }
//         public AppointmentType AppointmentType { get; set; }

//         public string TimeZoneInfoId { get; set; }
//     }

//     public class EventsAndSlotsProfile : Profile
//     {
//         public EventsAndSlotsProfile()
//         {
//             CreateMap<EntityOpenSlots, EventsAndSlots>()
//                 .ForMember(d => d.AppointmentType, o => o.Ignore()) // ???
//                 ;
//         }
//     }
// }