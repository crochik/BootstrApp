using AutoMapper;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo
{
    public class LeadProfile : Profile
    {
        public LeadProfile()
        {
            // CreateMap<IntegrationLead, LeadIntegration>(MemberList.Source);

            // CreateMap<Lead, Lead>(MemberList.Source)
            //     .ForMember(d => d.RawBodies, o => o.Ignore()) // aftermap
            //     .ForMember(d => d.Properties, o => o.Ignore()) // aftermap
            //     .ForMember(d => d.Context, o => o.Ignore()) // calculated
            //     .ForMember(d => d.ObjectType, o => o.Ignore()) // calculated
            //     .ForMember(d => d.SerializedBody, o => o.Ignore()) // calculated
            //     .ForMember(d => d.Email, o => o.Ignore()) // calculated
            //     .ForMember(d => d.Integrations, o => o.Ignore()) // prevent mapper to try to map GetIntegrations() to Integrations
            //     .ForMember(d => d.ObjectStatusId, o => o.Ignore()) // leadstatus id will set it
            //     .AfterMap((s, d) =>
            //     {
            //         d.Properties = new Dictionary<string, object>(s.AllProperties());
            //         if (!string.IsNullOrWhiteSpace(s.SerializedBody)) d.RawBodies = new[] { s.SerializedBody };

            //         // ?!?!? 
            //         if (string.IsNullOrEmpty(s.Email)) d.AddIfMissing("email", s.Email);
            //         d.AddIfMissing("firstName", s.GetFirstName());
            //         d.AddIfMissing("lastName", s.GetLastName());
            //     });

            // TODO: should not be hardcoded
            // ...
            CreateMap<Lead, LeadSearchResult>()
                .ForMember(d => d.Email, o => o.MapFrom(s => s[Lead.PropertyName_Email]))
                .ForMember(d => d.Phone, o => o.MapFrom(s => s[Lead.PropertyName_Phone]))
                .ForMember(d => d.Address, o => o.MapFrom(s => s[Lead.PropertyName_Address]))
                .ForMember(d => d.City, o => o.MapFrom(s => s[Lead.PropertyName_City]))
                .ForMember(d => d.State, o => o.MapFrom(s => s[Lead.PropertyName_State]))
                .ForMember(d => d.PostalCode, o => o.MapFrom(s => s[Lead.PropertyName_PostalCode]))
                .ForMember(d => d.Country, o => o.MapFrom(s => s[Lead.PropertyName_Country]))
                .ForMember(d => d.LeadStatusId, o => o.MapFrom(s => s.ObjectStatusId))
                ;
        }
    }
}