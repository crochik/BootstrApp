namespace PI.Shared.Models;

public class EmailAttachment
{
    public string Filename { get; set; }
    public string ContentType { get; set; }
    public string URL { get; set; }
    public int Size { get; set; }
    public string Error { get; set; }
}