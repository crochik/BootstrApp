using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Microsoft.OpenApi;
using Xunit.Abstractions;

namespace UnitTests.OpenAI;

public class GeneratorTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public GeneratorTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    async Task SecurityTests()
    {
        var doc = new OpenApiDocument
        {
            Components = new OpenApiComponents(),
            Security = new List<OpenApiSecurityRequirement>(),
        };

        var scheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OpenIdConnect,
            Name = "openIdConnect",
            Description = "OpenID Connect",
            OpenIdConnectUrl = new Uri($"https://host/.well-known/openid-configuration"),
        };
        
        doc.AddComponent(scheme.Name, scheme);
        
        var refScheme = new OpenApiSecuritySchemeReference(scheme.Name, doc);
        
        doc.Security.Add(new OpenApiSecurityRequirement
            {
                { refScheme, ["rest"] }
            }
        );
        
        var outputString = await doc.SerializeAsYamlAsync(OpenApiSpecVersion.OpenApi3_1);
        _testOutputHelper.WriteLine(outputString);
    }
}