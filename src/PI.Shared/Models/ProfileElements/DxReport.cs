using System;
using Crochik.Dipper;
using Crochik.Mongo;
using PI.Shared.Form.Models;

namespace PI.Shared.Models
{
    [BsonCollection("dx.Report")]
    public class DxReport : EntityOwnedModel, IDataView, IProfileElement
    {
        public AggregateStoredProcedure StoredProcedure { get; set; }

        public DataView DataView { get; set; }
        public DataViewOptions Options { get; }

        public string Layout { get; set; }

        public Guid[] ProfileIds { get; set; }

        public EntityRoleId? Role { get; set; }
        public bool IsActive { get; set; }
    }
}