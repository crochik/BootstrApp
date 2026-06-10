using System;
using System.ComponentModel;
using Crochik.Dipper;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Form.Models;

namespace PI.Shared.Models
{
    public enum ReportTemplate
    {
        None,
        Monthly,
        Daily,
    }

    [BsonCollection("app.Report")]
    public class AppReport : AppProfileElement, IDataView, ISupportInitialize, ITaggable
    {
        public const string ObjectTypeFullName = "Report";
        
        public ReportTemplate Template { get; set; }

        [Obsolete("Replace with ProfileIds")]
        public EntityRoleId MinRole { get; set; } = EntityRoleId.Admin;

        public AggregateStoredProcedure StoredProcedure { get; set; }

        /// <summary>
        /// Group 
        /// </summary>
        public string Group { get; set; }

        [BsonElement("View")]
        private DataView _dataView;

        public DataView DataView { get; set; }
        public DataViewOptions Options { get; }
        public string[] Tags { get; set; }
        

        public void BeginInit() { }

        public void EndInit()
        {
            if (DataView == null)
            {
                DataView = _dataView;
                _dataView = null;
            }
        }
    }
}