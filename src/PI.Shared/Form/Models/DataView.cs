using System;
using System.Collections.Generic;
using System.Linq;
using Crochik.Mongo;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Form.Models
{
    public class DataViewDetail
    {
        // type: 'page' | 'datagrid' | 'dataform';
        // dataView?: string;
        public string Page { get; set; }
    }

    /// <summary>
    /// Component to be used when rendering the dataView
    /// </summary>
    public static class DataViewComponent
    {
        public const string Auto = nameof(Auto);
        public const string Grid = nameof(Grid);
        public const string Chart = nameof(Chart);
        public const string Card = nameof(Card);
        public const string Calendar = nameof(Calendar);
        public const string Map = nameof(Map);
        public const string ImageGallery = nameof(ImageGallery);
    }

    public class DataView
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public FormField[] Fields { get; set; }

        public string DefaultSort { get; set; }
        public string KeyField { get; set; }
        public bool IsSelectable { get; set; }

        /// <summary>
        /// Actions (partially supported, show action under the grid as buttons and uses to find the "edit action")
        /// </summary>
        [Obsolete("discontinue use as it doesn't make that much sense")]
        public FormAction[] Actions { get; set; }

        public DataViewDetail Detail { get; set; }

        /// <summary>
        /// fields that can be used to filter/sort
        /// </summary>
        public string[] Filter { get; set; } // ???

        /// <summary>
        /// Special flag to tell client to create local lookups for the "Filter"able fields
        /// </summary>
        public bool IsFilterableLocally { get; set; }

        /// <summary>
        /// Filter (form)
        /// </summary>
        public Form FilterForm { get; set; }

        /// <summary>
        /// Whether there is a #text index
        /// </summary>
        public bool? Searchable { get; set; }

        public int? PageSize { get; set; }

        public Menu Menu { get; set; }
    }

    public static class DataViewExtensions
    {
        public static DataView SetDefaultValue(this DataView view, string fieldName, object defaultValue)
        {
            view?.Fields.SetDefaultValue(fieldName, defaultValue);
            return view;
        }
    }

    public static class FormExtensions
    {
        public static Form SetDefaultValue(this Form form, string fieldName, object defaultValue)
        {
            form?.Fields.SetDefaultValue(fieldName, defaultValue);
            return form;
        }

        public static Form AddAction(this Form form, params FormAction[] actions)
        {
            form.Actions ??= Array.Empty<FormAction>();
            form.Actions = form.Actions.Concat(actions).ToArray();
            return form;
        }
    }

    public static class FormFieldExtensions
    {
        public static bool SetDefaultValue(this FormField[] fields, string fieldName, object defaultValue)
        {
            if (fields == null) return false;
            var field = fields.FirstOrDefault(x => string.Equals(x.Name, fieldName, System.StringComparison.OrdinalIgnoreCase));
            if (field == null) return false;

            field.DefaultValue = defaultValue;
            return true;
        }
    }

    public static class QueryExtensions
    {
        public static Query<T> WithPageFrom<T>(this Query<T> query, DataView view, DataViewRequest request)
        {
            if (request.ContentType != "text/csv")
            {
                if (view.PageSize.HasValue && view.PageSize.Value != request.Top)
                {
                    request.Top = view.PageSize.Value;
                    request.Skip = 0;
                }

                if (request.Top > 0) query.Skip(request.Skip).Limit(request.Top);
            }

            request.OrderBy ??= view.DefaultSort;

            if (!string.IsNullOrEmpty(request.OrderBy))
            {
                var reverseOrder = request.OrderBy.StartsWith('-');
                var orderBy = reverseOrder ? request.OrderBy[1..] : request.OrderBy;
                if (reverseOrder)
                {
                    query.SortDesc(orderBy);
                }
                else
                {
                    query.SortAsc(orderBy);
                }
            }

            return query;
        }
    }
}