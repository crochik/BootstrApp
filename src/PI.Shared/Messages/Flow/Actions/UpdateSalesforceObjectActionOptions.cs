using System.Dynamic;

namespace Messages.Flow;

public class UpdateSalesforceObjectActionOptions : ActionOptions //, IGenericActionBuilder
{
    public const string ObjectUpdatedEvent = nameof(ObjectUpdatedEvent);
    public const string FailedToUpdateObjectEvent = nameof(FailedToUpdateObjectEvent);

    public string ObjectType { get; set; }
    public ExpandoObject Mapping { get; set; }

    /// <summary>
    /// Expression
    /// </summary>
    public string ObjectId { get; set; }
}