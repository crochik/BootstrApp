using FluentAssertions;
using MongoDB.Bson;
using Xunit;

namespace UnitTests
{
    public class ISODate
    {
        [Fact]
        public void ReplaceISODates()
        {
            var result = BsonDocument.Parse("{ \"test\": \"ISODate()\" }").ReplaceISODates();
            var str = result.ToString();
            str.Contains("ISODate(\"").Should().BeTrue();
        }
    }
}
