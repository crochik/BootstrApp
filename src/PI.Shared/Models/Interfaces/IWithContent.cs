namespace PI.Shared.Models.Interfaces;

public interface IWithContent
{
    /// <summary>
    /// mime content type
    /// </summary>
    public string ContentType { get; set; }
 
    public string Content { get; set; }
}