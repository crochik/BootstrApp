using System.Collections;
using System.Collections.Generic;
using FluentAssertions;
using PI.Shared.Diff;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests;

public class Test
{
    private readonly ITestOutputHelper _testOutputHelper;

    public Test(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void Test1()
    {
        var a = new FieldTemplate
        {
            Field = new TextField
            {
                Name = "A",
                Enable =
                [
                ],
                Visible =
                [
                    "a",
                    "b"
                ],
                TextFieldOptions = new TextFieldOptions
                {
                    Multline = true,
                    MaxLength = 1,
                    ContentType = "text/plain",
                }
            },
            RBAC =
            {
                [EntityRoleId.Account] = FieldPermission.Read,
            }
        };

        var b = new FieldTemplate
        {
            Field = new TextField
            {
                Name = "B",
                Visible =
                [
                    "a"
                ],
                TextFieldOptions = new TextFieldOptions
                {
                    MaxLength = 2,
                    ContentType = "application/json"
                }
            }
        };

        var diff = SimpleDiffer.Diff(a, b, new SimpleDiffOptions
        {
            SkipJsonIgnore = true,
        });

        _testOutputHelper.WriteLine(diff.ToString());
    }

    [Fact]
    void Crash()
    {
        var dict = new Dictionary<string, object>
        {
            { "A", "test" }
        };

        var d = (IDictionary)dict;
        d["B"].Should().BeNull();

        // will throw
        Assert.Throws<KeyNotFoundException>(() => dict["B"]);
    }
}