using AutoMapper;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo
{

    public class AppointmentSnapshotProfile : Profile
    {
        public AppointmentSnapshotProfile()
        {
            CreateMap<AppointmentSnapshot, AppointmentSearchResult>();
        }
    }

    public class AppointmentProfile : Profile
    {
        public AppointmentProfile()
        {
            CreateMap<IIntegrationAppointment, AppointmentIntegration>()
                .ForMember(d => d.SerializedData, o => o.Ignore());
        }
    }
}