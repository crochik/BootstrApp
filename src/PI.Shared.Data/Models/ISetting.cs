namespace PI.Shared.Data.Models
{
    public interface ISetting
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
        string Type { get; }
        bool IsSystem { get; }
        bool IsUserVisible { get; }
        bool IsOrgVisible { get; }
        bool IsAccountVisible { get; }
    }
}