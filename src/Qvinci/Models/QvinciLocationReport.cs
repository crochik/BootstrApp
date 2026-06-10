using System.Collections.Generic;
using Crochik.Mongo;
using PI.Shared.Models;

namespace Qvinci.Models;

public class ReportError
{
    public int StatusCode { get; set; }
    public string Status { get; set; }
    public string Body { get; set; }
}
    

[BsonCollection("qvinci.Report")]
public class QvinciLocationReport : EntityOwnedModel
{
    public string TransactionId { get; set; }
    public QvinciReport Report { get; set; }

    public int LocationId { get; set; }
    public string Location { get; set; }

    public ReportFile Raw { get; set; }
    public ReportError Error { get; set; }

    public List<ReportRow> Rows { get; set; }
}