using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace Qvinci.Models;

public enum RowType
{
    Data,
    Header,
    Total
}

public class ReportRow
{
    public string LineLocator { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public RowType RowType { get; set; }
    public Dictionary<string, decimal> Values { get; set; }
        
    [BsonIgnore]
    public string[] Levels { get; set; }

    [BsonIgnore]
    public string Code { get; set; }
}