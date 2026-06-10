namespace PI.Shared.Models
{
    public interface IIndexedProperties
    {
        string this[string key] { get; }
    }
}