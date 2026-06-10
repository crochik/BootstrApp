using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DevExpress.Data;

namespace Reports.Controllers
{
    public class DynReportDataSource : ITypedList, IDisplayNameProvider
    {
        public DynReportDataSource() { }

        public string GetDataSourceDisplayName() => "MagicObject";

        public string GetFieldDisplayName(string[] fieldAccessors)
        {
            return Guid.NewGuid().ToString("N");
        }

        public string GetListName(PropertyDescriptor[] listAccessors) => listAccessors == null || listAccessors.Length == 0 ? nameof(DynReportDataSource) : string.Empty;

        public PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[] listAccessors)
        {
            if (listAccessors == null || listAccessors.Length == 0)
            {
                var props = new[]
                {
                        new DynamicPropertyDescriptor<DynReportDataSource, IEnumerable<object>>("Items", (parent, propName)=>new [] {
                            new Dictionary<string, object>
                            {
                                {"First Name", "Felipe"},
                            },
                            new Dictionary<string, object>
                            {
                                {"First Name", "Cosmo"},
                                {"Last Name", "Cosmo"},
                            },
                        })
                        // new DynamicPropertyDescriptor<DynReportDataSource, IEnumerable<Person>>("Items", (parent, propName)=>Person.GetData())
                    };

                return new PropertyDescriptorCollection(props.ToArray());
            }
            else if (listAccessors.Length == 1)
            {
                var props = new[]
                {
                        new DynamicPropertyDescriptor<IDictionary<string, object>, object>("First Name", (parent, propName)=>parent.TryGetValue(propName, out var value) ? value : null),
                        new DynamicPropertyDescriptor<IDictionary<string, object>, object>("Last Name", (parent, propName)=>parent.TryGetValue(propName, out var value) ? value : null),
                    };

                return new PropertyDescriptorCollection(props.ToArray());
            }

            return PropertyDescriptorCollection.Empty;
        }
    }
}
