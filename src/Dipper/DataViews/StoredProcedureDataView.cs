using PI.Shared.Form.Models;

namespace DataViews
{
    public class StoredProcedureDataView : DataView
    {
        public StoredProcedureDataView()
        {
            Name = "StoredProcedure";
            Title = "Stored Procedures";
            KeyField = "id";
            Detail = new DataViewDetail
            {
                Page = "StoredProcedure"
            };
            Filter = new[] {
                "id",
                "collection",
                "type"
            };
            Fields = new FormField[]
            {
                new HiddenField {
                    Name = "id",
                    Label = "Id"
                },
                new TextField {
                    Name = "collection",
                    Label = "Collection"
                },
                new TextField {
                    Name = "name",
                    Label = "Name"
                },
                new TextField {
                    Name = "namespace",
                    Label = "Namespace"
                },
                new TextField {
                    Name = "type",
                    Label = "Type"
                }
            };
        }
    }
}