using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Models
{
    public class SingerLoadingLog
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("CreatedOn")]
        public DateTime CreatedOn => DateTime.UtcNow;

        public Guid ConfigId { get; set; }
        public Guid JobId { get; set; }
        public string Stream { get; set; }
        public string Message { get; set; }

        [BsonElement("References")]
        public string[] References => _references?.ToArray();
        public SingerLoadResult Result { get; set; }
        public string Outcome { get; set; }
        public bool IsSuccessful => Result == SingerLoadResult.Added || Result == SingerLoadResult.Updated;

        private HashSet<string> _references = null;
        public void AddReference(object id)
        {
            if (id == null) return;

            _references ??= new HashSet<string>();
            _references.Add(id is string str ? str : id.ToString());
        }
        public void AddReferences(IEnumerable<string> ids)
        {
            if (ids == null) return;

            _references ??= new HashSet<string>();
            foreach (var id in ids)
            {
                if (!string.IsNullOrEmpty(id)) _references.Add(id);
            }
        }
        public void AddReferences(params string[] ids) => AddReference((IEnumerable<string>)ids);
    }
}