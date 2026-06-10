using System;
using AutoMapper;
using PI.Shared.Models;

namespace PI.Shared.O365.Extensions
{
    public static class MapperConfigurationExpressionExtensions
    {
        public static IMapperConfigurationExpression AddCalendar(this IMapperConfigurationExpression cfg)
        {
            AddEvent(cfg);
            AddDateTime(cfg);
            // AddRecurrence(cfg);

            return cfg;
        }

        // private static void AddRecurrence(IMapperConfigurationExpression cfg)
        // {
        //     cfg.CreateMap<Calendar.Message.Recurrence, O365RecurrenceDAO>();
        // }

        private static void AddDateTime(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<Microsoft.Graph.DateTimeTimeZone, System.DateTime>()
                .ConstructUsing(
                    // (dt) => TimeZoneInfo.ConvertTimeFromUtc(
                    //     Convert.ToDateTime(dt.DateTime),
                    //     TimeZoneInfo.FindSystemTimeZoneById(dt.TimeZone)
                    // )

                    (dt) => TimeZoneInfo.ConvertTimeToUtc(
                        DateTime.Parse(dt.DateTime),
                        TimeZoneInfo.FindSystemTimeZoneById(dt.TimeZone)
                    )
                );

            // // this to work around the recurrence end date being 01/01/01 when there ins't 
            // cfg.CreateMap<Microsoft.Graph.Date, System.DateTime?>()
            //     .ConstructUsing(dt => dt.Year < 1900 ? (DateTime?)null : new DateTime(dt.Year, dt.Month, dt.Day));
        }

        private static void AddEvent(IMapperConfigurationExpression cfg)
        {
            // // there must be a more elegant way :)
            // cfg.CreateMap<Microsoft.Graph.RecurrencePattern, Calendar.Message.Pattern>()
            //     .ForMember(dst => dst.Sunday, a => a.Ignore())
            //     .ForMember(dst => dst.Monday, a => a.Ignore())
            //     .ForMember(dst => dst.Tuesday, a => a.Ignore())
            //     .ForMember(dst => dst.Wednesday, a => a.Ignore())
            //     .ForMember(dst => dst.Thursday, a => a.Ignore())
            //     .ForMember(dst => dst.Friday, a => a.Ignore())
            //     .ForMember(dst => dst.Saturday, a => a.Ignore())
            //     ;

            // cfg.CreateMap<Microsoft.Graph.Event, Calendar.Message.CalendarEvent>()
            //     .ForMember(d => d.Recurrence, o => o.Ignore())
            //     .AfterMap(
            //         (s, d) =>
            //         {
            //             // set end date to null whenever range has no end
            //             if (s.Recurrence?.Range.Type == Microsoft.Graph.RecurrenceRangeType.NoEnd)
            //             {
            //                 d.Recurrence.Range.EndDate = null;
            //             }

            //             if (s.Recurrence?.Pattern.DaysOfWeek == null) return;

            //             foreach (var day in s.Recurrence.Pattern.DaysOfWeek)
            //             {
            //                 switch (day)
            //                 {
            //                     case Microsoft.Graph.DayOfWeek.Sunday: d.Recurrence.Pattern.Sunday = true; break;
            //                     case Microsoft.Graph.DayOfWeek.Monday: d.Recurrence.Pattern.Monday = true; break;
            //                     case Microsoft.Graph.DayOfWeek.Tuesday: d.Recurrence.Pattern.Tuesday = true; break;
            //                     case Microsoft.Graph.DayOfWeek.Wednesday: d.Recurrence.Pattern.Wednesday = true; break;
            //                     case Microsoft.Graph.DayOfWeek.Thursday: d.Recurrence.Pattern.Thursday = true; break;
            //                     case Microsoft.Graph.DayOfWeek.Friday: d.Recurrence.Pattern.Friday = true; break;
            //                     case Microsoft.Graph.DayOfWeek.Saturday: d.Recurrence.Pattern.Saturday = true; break;
            //                 }
            //             }
            //         }
            //     );

            // cfg.CreateMap<Calendar.Message.Recurrence, O365Recurrence>()
            //     .ForMember(dest => dest.Id, opts => opts.Ignore());

            // cfg.CreateMap<Calendar.Message.Recurrence, O365Recurrence>()
            //     .ConstructUsing((src, context) =>
            //     {
            //         var dst = new O365Recurrence();
            //         return context.Mapper.Map(src, dst);
            //     });

            // cfg.CreateMap<Calendar.Message.CalendarEvent, O365Event>()
            //     .ForMember(dest => dest.Categories, opt =>
            //         opt.MapFrom(
            //             src => src.Categories == null || src.Categories.Length < 1 ?
            //                 null :
            //                 string.Join(",", src.Categories)
            //         )
            //     )
            //     .ForMember(dest => dest.EntityId, opt => opt.Ignore())
            //     ;

            cfg.CreateMap<Microsoft.Graph.ResponseStatus, O365ResponseStatus>(MemberList.Destination)
                .ForMember(d => d.Time, o => o.MapFrom(s => s.Time.HasValue ? s.Time.Value.Date : default(DateTime?)));

            cfg.CreateMap<Microsoft.Graph.Event, O365Event>()
                .ForMember(d => d.ExternalId, o => o.MapFrom(s => s.Id))
                .ForMember(d => d.MasterExternalId, o => o.MapFrom(s => s.SeriesMasterId))
                .ForMember(d => d.Name, o => o.MapFrom(s => s.Subject))
                .ForMember(d => d.Id, o => o.MapFrom(s => Guid.NewGuid())) // generate new
                .ForMember(d => d.CreatedOn, o => o.MapFrom(s => DateTime.UtcNow))
                .ForMember(d => d.LastModifiedOn, o => o.MapFrom(s => DateTime.UtcNow))

                .ForMember(d => d.AppointmentId, o => o.Ignore())   // explicitly assinged 
                .ForMember(d => d.SeriesMasterId, o => o.Ignore())  // has to be set later so it will use the "UUID"
                .ForMember(d => d.LastActor, o => o.Ignore())       // needs context for it
                .ForMember(d => d.EntityId, o => o.Ignore())        // needs context for it
                .ForMember(d => d.AccountId, o => o.Ignore())
                .ForMember(d => d.Instances, o => o.Ignore())
                .ForMember(d => d.Description, o => o.Ignore())
                ;
        }
    }
}