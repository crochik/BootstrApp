using AutoMapper;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo;

public class AppointmentTypeProfile : Profile
{
    public AppointmentTypeProfile()
    {
        // hack (will leave Level and EntityName null)
        CreateMap<AppointmentType, EntityAppointmentType>(MemberList.Source);

        CreateMap<AppointmentTypeIntegration, AppointmentTypeIntegration>()
            .ForMember(d => d.AppointmentTypeId, o => o.Ignore());
    }
}