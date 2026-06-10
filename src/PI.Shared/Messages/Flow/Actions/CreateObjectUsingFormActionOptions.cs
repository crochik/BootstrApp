using System.Dynamic;

namespace Messages.Flow;

public class CreateObjectUsingFormActionOptions : ActionOptions
{
    public const string ObjectCreatedEvent = "ObjectCreated";
    public const string FailToCreateObjectEvent = "FailToCreateObject";
    
    public string ObjectType { get; set; }
    public ExpandoObject Object { get; set; }
    public string Alias { get; set; }
}