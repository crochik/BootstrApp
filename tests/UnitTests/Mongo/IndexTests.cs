using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using Xunit;
using Xunit.Abstractions;
using Index = PI.Shared.Models.Index;

namespace UnitTests.Mongo;

public class IndexTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public IndexTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void Search()
    {
        // { "$text" : { "$search" : "solid" }, "Material.Type" : "Carpet", "Tags" : "Product Selector", "_t" : "Style", "EntityId" : "fc100000-0000-0000-0000-000000000000", "AccountId" : "fc100000-0000-0000-0000-000000000000" }
        var index = new Index
        {
            Name = "search",
            AutoCompleteFieldNames = ["ExternalId", "Properties|StyleName", "Name"],
            SearchFieldNames =
            [
                "Description", "Properties|CarpetType", "Properties|CollectionName",
                "Properties|ColorType", "Properties|Fiber", "Properties|LaminatesType", "Properties|PatternMatch",
                "Properties|Texture", "Properties|Vendor", "Properties|WoodSpecies", "Properties|WoodType",
                "Tags"
            ], // "ExternalId", "Properties|StyleName", "Name", 
            FilterFieldNames = ["AccountId", "EntityId", "IsActive", "Material|Type", "_t"]
        };

        var result = new SearchStageBuilder
        {
            Index = index,
            Conditions =
            [
                Condition.Eq("Material|Type", "Carpet"),
                Condition.Eq("Tags", "Product Selector"),
                Condition.Eq("_t", "Style"),
                Condition.Eq("EntityId", "fc100000-0000-0000-0000-000000000000"),
                Condition.Eq("AccountId", "fc100000-0000-0000-0000-000000000000"),
                Condition.Eq("IsActive", true),
                Condition.Eq(Condition.FullTextSearch, "NAT"),
            ]
        }.Build();

        _testOutputHelper.WriteLine(result.ToString());
    }
}