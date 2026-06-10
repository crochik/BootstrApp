using AutoMapper;

namespace Controllers.Models;

public class ExtendedAppointment : PI.Shared.Models.Appointment
{
    public string CreatedByName { get; set; }
    public string CancelledByName { get; set; }
    public string EntityName { get; set; }
    public string LeadName { get; set; }
}

public class AppointmentProfile : Profile
{
    public AppointmentProfile()
    {
        CreateMap<PI.Shared.Models.Appointment, Controllers.Models.ExtendedAppointment>()
            .ForMember(x=>x.CreatedByName, o=>o.Ignore())
            .ForMember(x=>x.CancelledByName, o=>o.Ignore())
            .ForMember(x=>x.EntityName, o=>o.Ignore())
            .ForMember(x=>x.LeadName, o=>o.Ignore())
            // omit 
            .ForMember(x=>x.Integrations, o=>o.Ignore())
            ;
    }
}