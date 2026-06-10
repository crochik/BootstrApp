namespace PI.DocuSeal.Models;

public class DocuSealSubmitter
{
    public string? Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Role { get; set; }

    /// <summary>
    /// UUID created for submitter
    /// </summary>
    public int? ExternalId { get; set; }

    public string? Slug { get; set; }
}