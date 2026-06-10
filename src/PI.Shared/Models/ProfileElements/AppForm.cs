using Crochik.Mongo;

namespace PI.Shared.Models
{
    [BsonCollection("app.Form.1")]
    public class AppForm : AppProfileElement, IObjectTypeProfileElement
    {
        public Form.Models.Form Form { get; set; }
        public string ObjectType { get; set; }
    }
}