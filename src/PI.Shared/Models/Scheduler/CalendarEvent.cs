using System;
using AutoMapper;

namespace PI.Shared.Models
{
    public class CalendarEvent
    {
        public string Id { get; set; }
        public FreeBusyStatus ShowAs { get; set; }
        public CalendarEventType Type { get; set; }
        public bool IsCancelled { get; set; }
        public bool IsAllDay { get; set; }
        public string Subject { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string WebLink { get; set; }
        public string Source { get; set; }
    }

    public class CalendarEventProfile : Profile
    {
        public CalendarEventProfile()
        {
            // convert into utc
            CreateMap<O365Event, CalendarEvent>(MemberList.Destination)
                .ForMember(dst => dst.Start, opt => opt.MapFrom(src => DateTime.SpecifyKind(src.Start, DateTimeKind.Utc)))
                .ForMember(dst => dst.End, opt => opt.MapFrom(src => DateTime.SpecifyKind(src.End, DateTimeKind.Utc)))
                .ForMember(dst => dst.Source, opt => opt.MapFrom(src => "o365"))
                .ForMember(d => d.Subject, o => o.MapFrom(s => s.Name))
                ;

            CreateMap<Appointment, CalendarEvent>(MemberList.Destination)
                .ForMember(dst => dst.Start, opt => opt.MapFrom(src => DateTime.SpecifyKind(src.Start, DateTimeKind.Utc)))
                .ForMember(dst => dst.End, opt => opt.MapFrom(src => DateTime.SpecifyKind(src.End, DateTimeKind.Utc)))
                .ForMember(d => d.ShowAs, o => o.MapFrom(s => FreeBusyStatus.Busy))
                .ForMember(d => d.Type, o => o.MapFrom(s => CalendarEventType.SingleInstance))
                .ForMember(d => d.IsCancelled, o => o.MapFrom(s => !s.IsActive))
                .ForMember(d => d.Source, o => o.MapFrom(s => "pi"))
                ;
        }
    }
}