using System.Dynamic;

namespace Messages.Flow;

public class CreateSalesforceObjectActionOptions : ActionOptions//, IGenericActionBuilder
{
    public const string ObjectCreatedEvent = nameof(ObjectCreatedEvent);
    public const string FailedToCreateObjectEvent = nameof(FailedToCreateObjectEvent);
    
    public string ObjectType { get; set; }
    public ExpandoObject Mapping { get; set; }
}