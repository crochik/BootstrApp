namespace PI.Shared.Data.Models
{
    public class Setting : ISetting
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public bool IsSystem { get; set; }
        public bool IsUserVisible { get; set; }
        public bool IsOrgVisible { get; set; }
        public bool IsAccountVisible { get; set; }
    }
}