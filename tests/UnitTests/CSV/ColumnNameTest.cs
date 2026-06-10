using System;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests;

public class ColumnNameTest
{
    private readonly ITestOutputHelper _testOutputHelper;

    public ColumnNameTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void Test()
    {
        for (var c = 0; c < 255; c++)
        {
            _testOutputHelper.WriteLine(ToCol(c));
        }

        string ToCol(int c)
        {
            var h = c % 26;
            var l = c / 26;
            var cl = l == 0 ? "" : $"{(char)('A' + (l-1))}";
            var ch = (char)('A' + h);
            return $"{cl}{ch}";
        }
    }
}