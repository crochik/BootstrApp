using System.Dynamic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests.Mongo;

public class QueryTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public QueryTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void NotAll()
    {
        var query = new Query<ExpandoObject>();
        query.NotBuilder(q => q.All("test", ["a", "b"]));
        
        // 3. Get the default serializer registry
        var serializerRegistry = BsonSerializer.SerializerRegistry;
        
        // 4. Get the serializer for your document type
        var documentSerializer = serializerRegistry.GetSerializer<ExpandoObject>();

        var bson = query.Filter.Render(documentSerializer, serializerRegistry);
        
        _testOutputHelper.WriteLine(bson.ToString());
    }
}