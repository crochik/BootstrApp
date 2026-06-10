using PI.Shared.Data.Models;

namespace Controllers.Models
{
    public class Setting : ISetting
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsAccountVisible { get; set; }
        public bool IsOrgVisible { get; set; }
        public bool IsSystem { get; set; }
        public bool IsUserVisible { get; set; }
        public object AccountValue { get; set; }
        public object OrgValue { get; set; }
        public object UserValue { get; set; }
        public object Value { get; set; }
        public string Description { get; set; }
    }
}