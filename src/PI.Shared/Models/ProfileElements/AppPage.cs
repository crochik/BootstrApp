using Crochik.Mongo;
using PI.Shared.Form.Models;

namespace PI.Shared.Models;

[BsonCollection("app.Page.1")]
public class AppPage : AppProfileElement, IObjectTypeProfileElement
{
    public Page Page { get; set; }
    
    /// <summary>
    /// Object Type (optional) 
    /// </summary>
    public string ObjectType { get; set; }
    
    // related objects to load?
    // ...
}