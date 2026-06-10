namespace Qvinci.Models;

public class LocationSearch
{
    public int TotalCount { get; set; }
    public string Url { get; set; }
    public QvinciLocation[] Items { get; set; }
}