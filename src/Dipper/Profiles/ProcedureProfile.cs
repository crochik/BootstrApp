using AutoMapper;
using Crochik.Dipper;

namespace Models
{
    public class ProcedureProfile : Profile
    {
        public ProcedureProfile()
        {
            CreateMap<StoredProcedure, Procedure>(MemberList.Destination);
                // .ForMember(d => d.Name, o => o.MapFrom(s => s.Id.Substring(s.Namespace.Length + 1)));

            CreateMap<StoredProcedure, BasicProcedure>(MemberList.Destination)
                // .ForMember(d => d.Name, o => o.MapFrom(s => s.Id.Substring(s.Namespace.Length + 1)))
                .ForMember(d => d.Type, o => o.MapFrom(s => s.GetFriendlyTypeName()));

            CreateMap<Macro, MacroStoredProcedure>(MemberList.Source)
                .ForSourceMember(d => d.Body, o => o.DoNotValidate())
                .ForSourceMember(d => d.Namespace, o => o.DoNotValidate())
                .ForSourceMember(d => d.Name, o => o.DoNotValidate())
                .ForSourceMember(d => d.Id, o => o.DoNotValidate()) // will calculate based on namespace/name
                .ForMember(d => d.Id, o => o.MapFrom(s => $"{s.Namespace}.{s.Name}"))
                ;

            CreateMap<UpdateProcedure, UpdateStoredProcedure>(MemberList.Source)
                .ForSourceMember(d => d.Body, o => o.DoNotValidate())
                .ForSourceMember(d => d.Namespace, o => o.DoNotValidate())
                .ForSourceMember(d => d.Name, o => o.DoNotValidate())
                .ForSourceMember(d => d.Id, o => o.DoNotValidate()) // will calculate based on namespace/name
                .ForMember(d => d.Id, o => o.MapFrom(s => $"{s.Namespace}.{s.Name}"))
                ;
        }
    }
}