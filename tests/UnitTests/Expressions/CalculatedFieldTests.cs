using System;
using System.Dynamic;
using FluentAssertions;
using Newtonsoft.Json;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using Xunit;

namespace UnitTests.Expressions;

public class CalculatedFieldTests
{
    [Fact]
    public void PathCalculation()
    {
        var obj = new
        {
            A = "Test",
            B = new
            {
                C = "Another"
            }
        };

        var context = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(obj));

        new PathCalculation().Calculate(context).Should().Be(context);
        new PathCalculation().Calculate(obj).Should().Be(obj);
        new PathCalculation
        {
            Path = "A",
        }.Calculate(context).Should().Be("Test");
        new PathCalculation
        {
            Path = "{{A}}",
        }.Calculate(context).Should().Be("Test");
        new PathCalculation
        {
            Path = "{{B.C}}",
        }.Calculate(context).Should().Be("Another");
        new PathCalculation
        {
            Path = "B|C",
        }.Calculate(context).Should().Be("Another");

        new PathCalculation
        {
            Path = "B|D",
        }.Calculate(context).Should().BeNull();
    }

    [Fact]
    public void IndexCalculation()
    {
        var obj = new
        {
            A = "Test",
            B = new[]
            {
                "First",
                "Second",
                "Last"
            }
        };

        dynamic context = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(obj));
        
        (new IndexCalculation
        {
            Index = 0,
        }.Calculate(context.B) as string).Should().Be("First");
        
        (new IndexCalculation
        {
            Index = 1,
        }.Calculate(context.B) as string).Should().Be("Second");
        
        (new IndexCalculation
        {
            Index = -2,
        }.Calculate(context.B) as string).Should().Be("Second");
        
        (new IndexCalculation
        {
            Index = -1,
        }.Calculate(context.B) as string).Should().Be("Last");
    }

    [Fact]
    public void LookupCalculation()
    {
        var obj = new
        {
            Identities = new []
            {
                new EntityIdentity
                {
                    Id = Guid.Empty,
                    IdentityProviderId = nameof(ExternalProvider.Google),
                    ExternalId = "Google",
                },
                new EntityIdentity
                {
                    Id = Guid.NewGuid(),
                    IdentityProviderId = nameof(ExternalProvider.Salesforce),
                    ExternalId = "ABCDE",
                },
                new EntityIdentity
                {
                    Id = Guid.NewGuid(),
                    IdentityProviderId = nameof(ExternalProvider.Salesforce),
                    ExternalId = "AFTER",
                }
            }
        };

        dynamic context = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(obj));
        
        (new LookupCalculation
        {
            Conditions = new []
            {
                Condition.Eq(nameof(EntityIdentity.IdentityProviderId), nameof(ExternalProvider.Salesforce)), 
            },
        }.Calculate(context.Identities).ExternalId as string).Should().Be("ABCDE");
        
        (new LookupCalculation
        {
            Conditions = new []
            {
                Condition.Eq(nameof(EntityIdentity.IdentityProviderId), nameof(ExternalProvider.Google)), 
                Condition.Eq(nameof(EntityIdentity.ExternalId), "Google")
            },
        }.Calculate(context.Identities).Id as string).Should().Be(Guid.Empty.ToString());

        (new LookupCalculation
        {
            Conditions = new []
            {
                Condition.Eq(nameof(EntityIdentity.IdentityProviderId), nameof(ExternalProvider.Salesforce)), 
                Condition.Ne(nameof(EntityIdentity.ExternalId), "ABCDE")
            },
        }.Calculate(context.Identities).ExternalId as string).Should().Be("AFTER");
    }

    [Fact]
    public void ExpressionCalculation()
    {
        var obj = new /*User*/
        {
            Identities = new[]
            {
                new EntityIdentity
                {
                    Id = Guid.Empty,
                    IdentityProviderId = nameof(ExternalProvider.Google),
                    ExternalId = "Google",
                },
                new EntityIdentity
                {
                    Id = Guid.NewGuid(),
                    IdentityProviderId = nameof(ExternalProvider.Salesforce),
                    ExternalId = "ABCDE",
                },
                new EntityIdentity
                {
                    Id = Guid.NewGuid(),
                    IdentityProviderId = nameof(ExternalProvider.Salesforce),
                    ExternalId = "AFTER",
                }
            }
        };

        var context = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(obj));

        new ExpressionCalculation
        {
            Calculations = new Calculation[]
            {
                new PathCalculation { Path = nameof(User.Identities) },
                new LookupCalculation
                {
                    Conditions = new []
                    {
                        Condition.Eq(nameof(EntityIdentity.IdentityProviderId), nameof(ExternalProvider.Salesforce)), 
                        Condition.Ne(nameof(EntityIdentity.ExternalId), "ABCDE")
                    },
                },
                new PathCalculation { Path = nameof(EntityIdentity.ExternalId)}
            }
        }.Calculate(context).Should().Be("AFTER");
        
        new ExpressionCalculation
        {
            Calculations = new Calculation[]
            {
                new PathCalculation { Path = nameof(User.Identities) },
                new IndexCalculation { Index = 1 },
                new PathCalculation { Path = nameof(EntityIdentity.ExternalId)}
            }
        }.Calculate(context).Should().Be("ABCDE");        
    }
}