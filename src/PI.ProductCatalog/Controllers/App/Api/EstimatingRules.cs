using System;
using System.Collections.Generic;
using System.Linq;
using Crochik.Mongo;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace PI.ProductCatalog.Models;

[BsonCollection("fcb2b.EstimateRuleSet")]
public class EstimateRuleSet : EntityOwnedModel, ITaggable
{
    public ProductType? ProductType { get; set; }

    /// <summary>
    /// Rules
    /// </summary>
    public EstimateRule[] Rules { get; set; }
    
    /// <summary>
    /// Custom sections to be added automatically
    /// </summary>
    public EstimateSection[] CustomSections { get; set; }
    
    /// <summary>
    /// Filter for suitable main items
    /// </summary>
    public Criteria ItemsCriteria { get; set; }

    public string[] Tags { get; set; }
    public bool IsActive { get; set; }
}

[BsonCollection("fcb2b.EstimateRule")]
public class EstimateRule : EntityOwnedModel, ITaggable 
{
    /// <summary>
    /// Resolved item, regardless of the SKU (or based on it)
    /// </summary>
    public Guid? ItemId { get; set; }
    
    /// <summary>
    /// SKU to resolve item for zee
    /// also used to add to the context (formula) 
    /// </summary>
    public string SKU { get; set; }
    
    public EstimateInput Input { get; set; }
    public decimal? Factor { get; set; }
    public decimal? Offset { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public decimal? Modulus { get; set; }
    
    public decimal? WasteFactor { get; set; }
    
    public UnitOfMeasurement UOM { get; set; }
    public EstimateRuleCondition[] Conditions { get; set; }
    
    /// <summary>
    /// If section is missing do not add (just formula)
    /// </summary>
    public LineItemSource? Section { get; set; }
    
    public TaxCategory TaxCategory { get; set; }

    public string[] Tags { get; set; }
    
    public Guid? EstimateRuleSetId { get; set; }

    public QuantityCriteria QuantityCriteria => Input switch
    {
        EstimateInput.MainProductArea => QuantityCriteria.MainProductArea,
        EstimateInput.Perimeter => QuantityCriteria.Perimeter,
        EstimateInput.RoomArea => QuantityCriteria.RoomArea,
        _ => QuantityCriteria.Arbitrary,
    };
}

public class EstimateRuleCondition
{
    public EstimateInput Input { get; set; }
    public Operator Operator { get; set; }
    public object Value { get; set; }
}

public static class EstimateRuleExtensions
{
    public static bool IsMatch(this EstimateRule rule, Dictionary<string, object> context)
    {
        if (rule.Conditions==null) return false;
        // if (!context.TryResolvePathValue(rule.Input.ToString(), out object value))
        // {
        //     // missing input value
        //     return false;
        // }
        
        var conditions = rule.Conditions.Select(x=> Condition.New(x.Input.ToString(), x.Operator, x.Value)).ToArray();
        var match = conditions.AllTrueUsingExpressions(null, context);
        return match;
    }
}