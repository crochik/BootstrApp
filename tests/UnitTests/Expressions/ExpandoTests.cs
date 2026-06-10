using System;
using System.Collections.Generic;
using System.Dynamic;
using FluentAssertions;
using Newtonsoft.Json;
using PI.Shared.Extensions;
using Xunit;

namespace UnitTests.Expressions;

public class ExpandoTests
{
    [Fact]
    public void Deserialize()
    {
        var obj = new
        {
            A = new
            {
                B = "b",
                C = "c"
            },
            D = new
            {
                E = new
                {
                    F = "f",
                    G = 0,
                    H = DateTime.UtcNow
                }
            }
        };

        var json = JsonConvert.SerializeObject(obj);
        var deserialized = JsonConvert.DeserializeObject<ExpandoObject>(json);
        var dict = deserialized.ToDictionaryObject();
    }
}