using System;
using FluentAssertions;
using IdentityModel;
using MongoDB.Bson;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests;

public class ObjectIdExtensions
{
    private readonly ITestOutputHelper _testOutputHelper;

    public ObjectIdExtensions(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void ConversionString()
    {
        var objectId = ObjectId.GenerateNewId();
        var str = objectId.ToString();
            
        var after = ObjectId.Parse(str);
        after.Equals(objectId).Should().BeTrue();
    }

    [Fact]
    public void ConversionGuid()
    {
        var objectId = ObjectId.GenerateNewId();
        var guid = objectId.ToGuid();
            
        var after = guid.ToObjectId();
        after.Equals(objectId).Should().BeTrue();
    }        

    [Fact]
    public void ConversionStrGuid()
    {
        var objectId = ObjectId.GenerateNewId();
        var str = objectId.ToString();
        var guid = str.ToObjectId();

        var after = guid.ToObjectId();
        after.Equals(objectId).Should().BeTrue();
    }

    [Fact]
    public void ShortUUID()
    {
        var objectId = ObjectId.GenerateNewId();
        var str = Base64Url.Encode(objectId.ToByteArray());

        _testOutputHelper.WriteLine($"{objectId}: {str}");
        
        var result = new ObjectId(Base64Url.Decode(str));
        objectId.Equals(result).Should().BeTrue();
    }
}