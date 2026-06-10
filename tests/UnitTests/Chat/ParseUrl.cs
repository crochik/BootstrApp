using System;
using System.Web;
using FluentAssertions;
using PI.Shared.Exceptions;
using Xunit;

namespace UnitTests.Chat;

public class ParseUrl
{
    [Fact]
    public void Test()
    {
        var str = "https://chat.googleapis.com/v1/spaces/AAAAfpcIDQY/messages?key=mykey&token=secrettoken";
        var url = new Uri(str);
        if (url.Host != "chat.googleapis.com") throw new BadRequestException("Invalid host");
        var pathParts = url.AbsolutePath.Split("/", StringSplitOptions.RemoveEmptyEntries);
        pathParts.Length.Should().Be(4);
        pathParts[0].Should().Be("v1");
        pathParts[1].Should().Be("spaces");
        pathParts[3].Should().Be("messages");
        var spaceId = pathParts[2];
        url.Query.StartsWith("?").Should().BeTrue();
        var query = HttpUtility.ParseQueryString(url.Query);
        var key = query.Get("key");
        var token = query.Get("token");

        spaceId.Should().Be("AAAAfpcIDQY");
        token.Should().Be("secrettoken");
        key.Should().Be("mykey");
    }
}