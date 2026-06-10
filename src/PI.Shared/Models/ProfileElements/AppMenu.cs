using Crochik.Mongo;
using PI.Shared.Form.Models;

namespace PI.Shared.Models
{
    [BsonCollection("app.Menu.1")]
    public class AppMenu : AppProfileElement
    {
        public Menu Menu { get; set; }
    }
}