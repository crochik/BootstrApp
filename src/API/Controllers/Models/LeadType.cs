using System;
using AutoMapper;
using PI.Shared.Data.Models;

namespace Controllers.Models;

public class LeadType
{
    public Guid? Id { get; set; }
    public Guid EntityId { get; set; }
    public String Name { get; set; }
    public Guid? FlowId { get; set; }
}

public class LeadTypeProfile : Profile
{
    public LeadTypeProfile()
    {
        CreateMap<PI.Shared.Data.Models.LeadType, LeadType>(MemberList.Destination);
    }
}

public class EntityLeadType : LeadType
{
    public string Level { get; set; }
    public string EntityName { get; set; }
}

//public class FormFieldMapping : FormField
//{
//    public string Source { get; set; }
//}

//public class LeadTypeMapping
//{
//    public List<FieldMapperConfig> Fields { get; set; }
//    public bool RejectOnValidationError { get; set; }
//    public IEnumerable<string> PostValidation { get; set; }
//    public string EntityIdOverrideField { get; set; }
//}

public class LeadTypeToAdd
{
    public Guid? Id { get; set; }
    public string Name { get; set; }
    public LeadTypeSettings Settings { get; set; }
}