using AutoMapper;
using PI.Shared.Models;
using Services;

namespace SchedulerAPI.Profiles
{
    public class Other : Profile
    {
        public Other()
        {
            // // convert into utc
            // CreateMap<CalendarEvent, Controllers.Models.CalendarEvent>()
            //     .ForMember(dst => dst.Start, opt => opt.MapFrom(src => DateTime.SpecifyKind(src.Start, DateTimeKind.Utc)))
            //     .ForMember(dst => dst.End, opt => opt.MapFrom(src => DateTime.SpecifyKind(src.End, DateTimeKind.Utc)))
            //     .ForMember(dst => dst.IsCancelled, opt => opt.Ignore()) // ???
            //     .ForMember(dst => dst.Type, opt => opt.Ignore()) //  default=SingleInstance ???
            //     ;

            CreateMap<EntityIdentity, Controllers.Models.Identity>()
                .ForMember(dst => dst.Provider, opts => opts.MapFrom(src => src.IdentityProviderId));


            // cfg.CreateMap<FieldType, string>()
            //     .ConvertUsing(type => type.ToString().ToLowerInvariant());

            // cfg.CreateMap<Field, Controllers.Models.FormFieldMapping>()
            //     .ForMember(dst => dst.Source, opts => opts.MapFrom(src => src.Mapping.Source));

            // cfg.CreateMap<Controllers.Models.FormFieldMapping, Field>()
            //     .ForMember(dst => dst.Mapping, opts => 
            //         opts.MapFrom(src => new Services.PropertyMapping(src.Source)));


            //cfg.CreateMap<FieldMapperConfig, Controllers.Models.FormFieldMapping>()
            //.ForMember(dst => dst.DefaultValue, opts => opts.Ignore())
            //// ????
            //.ForMember(dst => dst.IsRequired, opts => opts.Ignore())
            //.ForMember(dst => dst.Enable, opts => opts.Ignore())
            //.ForMember(dst => dst.Visible, opts => opts.Ignore())
            //;

            // CreateMap<IIntegrationAppointment, Controllers.Models.AppointmentIntegration>()
            //     .ForMember(dst => dst.Integration, opts => opts.MapFrom(src => CacheService.Instance.Integrations[src.IntegrationId].Name));

            CreateMap<IIntegrationLead, Controllers.Models.LeadIntegration>()
                .ForMember(dst => dst.Integration, opts => opts.MapFrom(src => CacheService.Instance.Integrations[src.IntegrationId].Name));

            //     cfg.CreateMap<Controllers.Models.EntityOpenSlots, Controllers.SchedulerController.OpenSlots>()
            //         .ConstructUsing((src) =>
            //         {
            //             // src.Slots
            //             return new Controllers.SchedulerController.OpenSlots();
            //         });

        }
    }
}
