using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models
{
    [BsonCollection("filebox.Spreadsheet")]
    [UseObjectId]
    public class Spreadsheet : FlowObjectModel
    {
        public static readonly Guid CreatedStatusId = Guid.Parse("fb8cc92e-f9e7-4674-9360-e768bfce299b");
        public static readonly Guid ConvertedStatusId = Guid.Parse("d50aa39a-bd6a-496a-a92a-38d76ba2cfb9");
        public static readonly Guid MergedStatusId = Guid.Parse("dca167c7-d7a3-4ab2-a886-37b78b5cf883");
        public static readonly Guid MergingStatusId = Guid.Parse("42a41fb3-0b90-4f17-99f9-766ba395e8b8");
        public static readonly Guid RejectedStatusId = Guid.Parse("cb03f9fc-9437-47bc-a0da-486c5fdb7d71");

        [BsonSerializer(typeof(MagicGuidSerializer))]
        public Guid ParentId { get; set; }
        public int RowsCount { get; set; }
        public Dictionary<string, string> Columns { get; set; }
        public int ErrorsCount { get; set; }

        public bool IsConverted() => ObjectStatusId != CreatedStatusId;
    }

    [BsonCollection("filebox.SpreadsheetRow")]
    [UseObjectId]
    public class SpreadsheetRow : EntityOwnedModel
    {
        [BsonSerializer(typeof(MagicGuidSerializer))]
        public Guid ParentId { get; set; }
        public int Row { get; set; }
        public Dictionary<string, object> Columns { get; set; }
        public string[] Errors { get; set; }
    }
}