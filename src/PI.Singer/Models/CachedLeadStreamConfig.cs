using AutoMapper;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace Models
{
    public class CachedLeadStreamConfig : LeadStreamConfig
    {
        public LeadType LeadType { get; set; }
        public IEntityContext Context { get; set; }
    }

    public class CachedAppointmentStreamConfig : AppointmentStreamConfig
    {
        public LeadType LeadType { get; set; }
    }

    public class CachedStreamConfigProfile : Profile
    {
        public CachedStreamConfigProfile()
        {
            CreateMap<LeadStreamConfig, CachedLeadStreamConfig>()
                .ForMember(d => d.LeadType, o => o.Ignore())
                .ForMember(d => d.Context, o => o.Ignore())
                ;

            CreateMap<AppointmentStreamConfig, CachedAppointmentStreamConfig>()
                .ForMember(d => d.LeadType, o => o.Ignore())
                ;
        }
    }
}