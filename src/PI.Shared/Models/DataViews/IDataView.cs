using System;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Mongo;
using PI.Shared.Form.Models;
using PI.Shared.Models.Layout;

namespace PI.Shared.Models;

public interface IDataView
{
    AggregateStoredProcedure StoredProcedure { get; }
    DataView DataView { get; }
    DataViewOptions Options { get; }
}

public enum Projection
{
    Fields,
    Lookup,
    TopValues,
    [Obsolete("Unsafe!")]
    All 
}

public static class IDataViewExtension
{
    /// <summary>
    /// Default render for dataviews
    /// Shouldn't use for ObjectType 
    /// </summary>
    public static async Task<DataViewResponse> GetAsync(
        this IDataView config,
        IEntityContext context,
        MongoConnection connection,
        DataViewRequest request
    )
    {
        return await DataViewResponseBuilder.New(connection, context, request, config)
                .With(Projection.Fields)
                .BuildAsync()
            ;
    }

    /// <summary>
    /// Calculate response DataViewOptions based on request/data produced
    /// </summary>
    public static DataViewOptions CalculateDataViewOptions(this IDataView dataView, DataViewRequest request)
    {
        var options = dataView.Options;
        switch (options?.Type ?? DataViewComponent.Auto)
        {
            case DataViewComponent.Card:
            {
                if (shouldShowMap())
                {
                    // default to map
                    return new MapViewOptions();
                }

                // there is a requested card format
                if (options is CardDataViewOptions cardOptions && cardOptions.FormLayout is GridFormLayout gridFormLayout && gridFormLayout.Rows != null)
                {
                    // check where additional fields were requested and include them if so 
                    var stdFields = (cardOptions.Fields ?? Enumerable.Empty<Form.Models.FormField>())
                        .Select(x => x.Name)
                        .Distinct()
                        .ToHashSet();

                    var additionalFields = request.Fields.Where(x => !stdFields.Contains(x)).ToArray();
                    if (additionalFields.Length > 0)
                    {
                        // if not specified, add labels 
                        cardOptions.ShowLabels ??= true;

                        // add additional fields to list
                        gridFormLayout.Rows = (gridFormLayout.Rows)
                            .Concat(additionalFields.Select(x => new GridFormRowLayout
                            {
                                Fields = new[]
                                {
                                    new GridFormFieldLayout
                                    {
                                        Name = x,
                                        Width = 1
                                    }
                                }
                            }))
                            .ToArray();
                    }
                }

                return options;
            }

            case DataViewComponent.Grid:
            {
                if (shouldShowMap())
                {
                    // default to map
                    return new MapViewOptions();
                }

                return options;
            }

            case DataViewComponent.Auto:
            {
                if (shouldShowMap())
                {
                    // default to map
                    return new MapViewOptions();
                }

                // check based on resolution
                break;
            }

            case DataViewComponent.Map:
            {
                if (shouldShowMap())
                {
                    // default to map
                    return options;
                }

                // check based on resolution
                break;
            }

            case DataViewComponent.Calendar:
            case DataViewComponent.Chart:
            case DataViewComponent.ImageGallery:
                // ???
                return options;
        }

        return request.Breakpoint switch
        {
            ScreenBreakpoint.ExtraSmall => CardDataViewOptions.Default,
            _ => DataViewOptions.Default,
        };

        bool shouldShowMap()
        {
            return dataView.DataView.Fields.Any(x => x is LocationField);
        }
    }
}