namespace PI.Shared.Models;

public class UniqueIndex
{
    public string Name { get; set; }
    public string[] Fields { get; set; }

    public bool Upsert { get; set; }
}