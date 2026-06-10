using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

/// <summary>
/// complex field used for a Location (GeoJSONObject)
/// TODO: could be an extension of ObjectField
/// ...
/// </summary>
public class LocationField : FormField, IComplexFieldValue
{
    [JsonIgnore]
    [BsonIgnore]
    public LocationFieldOptions LocationFieldOptions
    {
        get => Options as LocationFieldOptions;
        set => Options = value;
    }

    public override BackingType GetBackingType() => new ObjectBackingType
    {
        IsArray = false,
        IsDictionary = false,
        ObjectType = "GeoJSONPoint",
    };

    public override object AutoConvert(object value)
    {
        switch (value)
        {
            case PI.Shared.Models.GeoJSON.Point pt:
                return pt;
            
            case JObject jObject:
            {
                // geo json
                var dict = jObject.Properties().ToDictionary();
                if (dict.TryGetStrParam("type", out var type) && type != "Point") throw new BadRequestException("Invalid location type");
                if (!dict.TryGetValue("coordinates", out var coordinatesObj) || coordinatesObj is not IEnumerable<object> eC ) throw new BadRequestException("Missing coordinates");
                var coordinates = eC.ToArray();
                if (coordinates.Length!=2) throw new BadRequestException("Unexpected coordinates");
                return new PI.Shared.Models.GeoJSON.Point
                {
                    Coordinates =
                    [
                        toDecimal(coordinates[0]),
                        toDecimal(coordinates[1])
                    ],
                };
            }

            case IDictionary<string, object> dict:
            {
                if (dict.TryGetValue("coordinates", out var coordinatesObj) )
                {
                    // GeoJson Pt, assume pt
                    if (dict.TryGetStrParam("type", out var type) && type != "Point") throw new BadRequestException("Invalid location type");
                } 
                else if (dict.TryGetValue("Coordinates", out coordinatesObj))
                {
                    // be nice and accept with Upper case
                    // only validate type if it is provided
                    if (dict.TryGetStrParam("Type", out var type) && type != "Point") throw new BadRequestException("Invalid location type");    
                }
                else
                {
                    // object of unexpected format
                    throw new BadRequestException("Invalid location type");
                }
                
                if (coordinatesObj is not IEnumerable<object> eC ) throw new BadRequestException("Missing coordinates");
                var coordinates = eC.ToArray();
                if (coordinates.Length!=2) throw new BadRequestException("Unexpected coordinates");
                return new PI.Shared.Models.GeoJSON.Point
                {
                    Coordinates = new decimal[]
                    {
                        toDecimal(coordinates[0]),
                        toDecimal(coordinates[1]),
                    },
                };
            }

            case string str:
            {
                var parts = str.Split(",");
                if (parts.Length != 2) return value;
                return new[]
                {
                    decimal.Parse(parts[0]),
                    decimal.Parse(parts[1]),
                };
            }
            
            // TODO: array? 
            // ...
        }
        
        return base.AutoConvert(value);

        decimal toDecimal(object obj)
        {
            return obj switch
            {
                float f => (decimal)f,
                double d => (decimal)d,
                decimal d => d,
                string str => decimal.TryParse(str, out var d) ? d : throw new BadRequestException("Invalid format"),
                _ => throw new BadRequestException("Invalid format")
            };
        }
    }
    
    public override IEnumerable<string> GetDependencies(bool forCalculation = false, bool requiredOutput = false)
    {
        if (requiredOutput)
        {
            if (!string.IsNullOrWhiteSpace(LocationFieldOptions?.IconFieldName)) yield return LocationFieldOptions?.IconFieldName;
        }
    }    
}