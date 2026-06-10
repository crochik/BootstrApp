using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Models;

namespace Qvinci.Models;

public class QvinciRow : EntityOwnedModel
{
    public string Year { get; set; }
    public string Code { get; set; }

    public RowType RowType { get; set; }
    public string[] Levels { get; set; }
        
    [BsonElement] public string Level1 => Levels?.Length > 0 ? Levels[0] : null;
    [BsonElement] public string Level2 => Levels?.Length > 1 ? Levels[1] : null;
    [BsonElement] public string Level3 => Levels?.Length > 2 ? Levels[2] : null;
    [BsonElement] public string Level4 => Levels?.Length > 3 ? Levels[3] : null;
    [BsonElement] public string Level5 => Levels?.Length > 4 ? Levels[4] : null;
    [BsonElement] public string Level6 => Levels?.Length > 5 ? Levels[5] : null;
        
    public Dictionary<string, decimal> Months { get; set; }
}

[BsonCollection("qvinci.PNL")]
public class QvinciPNL : QvinciRow
{
}

[BsonCollection("qvinci.Balance")]
public class QvinciBalance : QvinciRow
{
}
