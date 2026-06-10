using System;
using System.Text;
using System.Security.Cryptography;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests.Clients;

public class SharedSecretTest
{
    private readonly ITestOutputHelper _testOutputHelper;

    public SharedSecretTest(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    public string ToSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);

        return Convert.ToBase64String(hash);
    }

    [Fact]
    public void GenerateSecret()
    {
        var hash = ToSha256("client secret here");
        _testOutputHelper.WriteLine(hash);
    }
}