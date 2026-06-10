namespace Models;

public class ExportResult
{
    public Models.VerseLead Lead { get; set; }
    public PI.Shared.Models.Result<Models.VerseResponse> Response { get; set; }
}