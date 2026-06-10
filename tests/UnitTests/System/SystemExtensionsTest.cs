using System;
using FluentAssertions;
using PI.Shared.Form.Models;
using Xunit;

namespace UnitTests;

public class SystemExtensionsTest
{
    [Fact]
    public void Test()
    {
        var label = (FormField)new LabelField
        {
            Visible =
            [
                "test"
            ]
        };

        var copy = label.Copy();
        copy.Visible[0].Equals("test").Should().BeTrue();
        
    }
}