using System.Collections.Generic;
using System;
using System.Dynamic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests.Expressions;

public class ExpressionEvaluatorServiceTests
{
    private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
    {
        ContractResolver = new DefaultContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };
    
    private readonly ITestOutputHelper _testOutputHelper;

    private ExpressionEvaluatorService Service { get; } = new();

    private ExpressionEvaluatorService.Context Context
    {
        get
        {
            dynamic expando = new ExpandoObject();
            expando.Test = "Felipe Crochik";
            expando.Input = JsonConvert.DeserializeObject<ExpandoObject>(
                JsonConvert.SerializeObject(
                    new
                    {
                        Name = "Felipe Crochik",
                        First = "Felipe",
                        Last = "Crochik",
                        Phone = "9196503958",
                        Email = "FELIPE@CROCHIK.com",
                        PostalCode = "1234",
                        FullPostalCode = "27513-121",
                        Canada = new
                        {
                            PostalCode = "a3b 1c4"
                        },
                        Array = new object[]
                        {
                            new
                            {
                                Key = "a",
                                Value = "test",
                            },
                            new
                            {
                                Key = "b",
                                Value = "test2",
                            },
                            new
                            {
                                Key = " not __ - !?! . S4f3 321 key!",
                                Value = "value doesn't matter",
                            }
                        },
                        PPA = true,
                        URL = "https://fcicloudmatrix2.s3.amazonaws.com/InspireNet Mobile/001UJ00000J3aIXYAZ/%40mobi%2A%2A09d-90b0-4055-9bbf-d77d4489e2d0/measure_square/measure_square.pdf"
                    }
                ), JsonSerializerSettings);

            var context = new ExpressionEvaluatorService.Context
            {
                EntityContext = new AccountContext(AccountIds.CSS),
                Logger = new Logger(_testOutputHelper),
                Data = expando,
            };

            return context;
        }
    }

    public ExpressionEvaluatorServiceTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("{{firstName Input.Name}}", "Felipe")]
    [InlineData("{{lastName Input.Name}}", "Crochik")]
    [InlineData("{{concatenate Input.First Input.Last}}", "Felipe Crochik")]
    [InlineData("{{concatenate Input.First Input.Middle? Input.Last}}", "Felipe Crochik")]
    [InlineData("{{normalizeEmail Input.Email}}", "felipe@crochik.com")]
    [InlineData("{{normalizePhone Input.Phone}}", "+1 919-650-3958")]
    [InlineData("{{postalCodeLookup Input.PostalCode}}", "01234")]
    [InlineData("{{postalCodeLookup Input.Canada.PostalCode}}", "A3B")]
    [InlineData("{{postalCodeLookup Input.FullPostalCode}}", "27513")]
    [InlineData("Hi {{Input.First}}, Mr. {{Input.Last}}", "Hi Felipe, Mr. Crochik")]
    [InlineData("Hi {{firstName Input.Name}}, Mr. {{lastName Input.Name}}", "Hi Felipe, Mr. Crochik")]
    [InlineData("Hi {{upper firstName Input.Name}}, Mr. {{lower lastName Input.Name}}", "Hi FELIPE, Mr. crochik")]
    [InlineData("Hi {{upper \"Input.Name\"}}", "Hi INPUT.NAME")]
    public void Strings(string input, string expected)
    {
        Service.TryResolve(Context, input, out var firstName).Should().BeTrue();
        (firstName is string).Should().BeTrue();
        var str = firstName as string;
        str.Should().Be(expected);
    }

    [Theory]
    [InlineData("{{firstName Input.NameMissing!}}")]
    [InlineData("{{concatenate Input.First Input.Middle! Input.Last}}")]
    public void ShouldFail(string input)
    {
        Service.TryResolve(Context, input, out var firstName).Should().BeFalse();
    }

    [Fact]
    public void UUID()
    {
        Service.TryResolve(Context, "{{new UUID}}", out var uid).Should().BeTrue();
        (uid is Guid).Should().BeTrue();
    }

    [Fact]
    public void ObjectId_as_Guid()
    {
        Service.TryResolve(Context, "{{new ObjectId}}", out var uid).Should().BeTrue();
        (uid is Guid).Should().BeTrue();
    }

    [Fact]
    public void AccountContext()
    {
        Service.TryResolve(Context, "{{context \"AccountId\"}}", out var uid).Should().BeTrue();
        (uid is Guid).Should().BeTrue();
        (uid as Guid?).Value.Should().Be(AccountIds.CSS);

        Service.TryResolve(Context, "{{context \"OrganizationId\"}}", out uid).Should().BeTrue();
        (uid is Guid).Should().BeTrue();
        (uid as Guid?).Value.Should().Be(AccountIds.CSS);

        Service.TryResolve(Context, "{{context \"EntityId\"}}", out uid).Should().BeTrue();
        (uid is Guid).Should().BeTrue();
        (uid as Guid?).Value.Should().Be(AccountIds.CSS);

        Service.TryResolve(Context, "{{context \"UserId\"}}", out uid).Should().BeTrue();
        uid.Should().BeNull();

        Service.TryResolve(Context, "{{context \"ProfileId\"}}", out uid).Should().BeTrue();
        uid.Should().BeNull();

        Service.TryResolve(Context, "{{context \"Actor\"}}", out uid).Should().BeTrue();
        uid.Should().BeNull();

        Service.TryResolve(Context, "{{context \"SomethingElse\"}}", out uid).Should().BeFalse();
    }

    [Fact]
    public void Date()
    {
        Service.TryResolve(Context, "{{new Date}}", out var uid).Should().BeTrue();
        (uid is DateTime).Should().BeTrue();
    }

    [Fact]
    public void ToDate()
    {
        Service.TryResolve(Context, "{{toDate \"2024-06-01\"}}", out var date).Should().BeTrue();
        (date is DateTime).Should().BeTrue();
    }

    [Fact]
    public void ThirtyDaysAgo()
    {
        Service.TryResolve(Context, "{{toISODate dateAdd -30 \"day\" new \"Date\"}}", out var date).Should().BeTrue();
        _testOutputHelper.WriteLine($"Date: {date} ({date.GetType().FullName})");
        (date is BsonDateTime).Should().BeTrue();
    }
    
    [Fact]
    public void DateAdd()
    {
        Service.TryResolve(Context, "{{dateAdd -1 day toDate \"2024-06-01\"}}", out var dayBefore).Should().BeTrue();
        (dayBefore is DateTime).Should().BeTrue();
        
        Service.TryResolve(Context, "{{dateAdd \"days\" \"1\" toDate \"2024-06-01\"}}", out var dayAfter).Should().BeTrue();
        (dayAfter is DateTime).Should().BeTrue();

        if (dayBefore is DateTime d1 && dayAfter is DateTime d2)
        {
            (d2 - d1).TotalDays.Should().Be(2);
        }
    }

    [Fact]
    public void ToISODate()
    {
        Service.TryResolve(Context, "{{toISODate new \"Date\"}}", out var date).Should().BeTrue();
        (date is BsonDateTime).Should().BeTrue();
        
        Service.TryResolve(Context, "{{toISODate dateAdd -30 days new \"Date\"}}", out date).Should().BeTrue();
        (date is BsonDateTime).Should().BeTrue();

        Service.TryResolve(Context, "{{toISODate dateAdd -1 month new \"Date\"}}", out date).Should().BeTrue();
        (date is BsonDateTime).Should().BeTrue();

    }

    [Fact]
    public void ArrayToObject()
    {
        Service.TryResolve(Context, "{{arrayToObject .Key Input.Array!}}", out var dict).Should().BeTrue();
        (dict is IDictionary<string, object>).Should().BeTrue();
        if (dict is IDictionary<string, object> d)
        {
            d.Keys.Count.Should().Be(3);
        }
    }

    [Fact]
    public void ArrayToDictionary()
    {
        Service.TryResolve(Context, "{{arrayToDictionary .Key .Value Input.Array!}}", out var dict).Should().BeTrue();
        (dict is IDictionary<string, object>).Should().BeTrue();
        if (dict is not IDictionary<string, object> d) return;
        d.Keys.Count.Should().Be(3);
        d["A"].Should().Be("test");
        d["B"].Should().Be("test2");
        d["NotS4f3321Key"].Should().Be("value doesn't matter");
    }

    [Fact]
    public void ToArray()
    {
        Service.TryResolve(Context, "{{toArray Input.First! Input.Middle? Input.Last!}}", out var dict).Should().BeTrue();
        (dict is IEnumerable<object>).Should().BeTrue();
        if (dict is not IEnumerable<object> e) return;
        var a = e.ToArray();
        a.Length.Should().Be(2);
        a[0].Should().Be("Felipe");
        a[1].Should().Be("Crochik");
    }

    [Theory]
    [InlineData("bmxJUWKB8UHo", "Bmxjuwkb8uho")]
    public void SafeName(string input, string output)
    {
        FunctionExtensions.ToSafeKey(input).Should().Be(output);

    }

    [Fact]
    public void TestDoubleDouble()
    {
        Service.TryResolve(Context, "{{\"{{Test}}\"}}", out var result).Should().BeTrue();
        result.Should().Be("{{Test}}");
    }

    [Fact]
    public void TestRnd()
    {
        Service.TryResolve(Context, "{{rndStr 62 10}}", out var result).Should().BeTrue();
        if (result is string str)
        {
            str.Length.Should().Be(10);
            _testOutputHelper.WriteLine(str);
        }
        else
        {
            false.Should().BeTrue();
        }
    }

    [Fact]
    public void TestShortLink()
    {
        Service.TryResolve(Context, "YourFCI.com/{{toSafeKey Input.First Input.Last}}_{{rndStr 62 3}}", out var value).Should().BeTrue();
        _testOutputHelper.WriteLine(value?.ToString());
        
        Service.TryResolve(Context, "YourFCI.com/{{rndStr 10 4}}-{{toSafeKey Input.First Input.Last}}", out  value).Should().BeTrue();
        _testOutputHelper.WriteLine(value?.ToString());
    }
    
    [Fact]
    public void TestFileExtension()
    {
        Service.TryResolve(Context, "{{getFileExtension Input.URL}}", out var value).Should().BeTrue();
        _testOutputHelper.WriteLine(value?.ToString());
    }
    
    private class Logger : ILogger
    {
        private readonly ITestOutputHelper _testOutputHelper;

        private class Scope : IDisposable
        {
            private readonly object _state;

            public Scope(object state)
            {
                _state = state;
            }

            public void Dispose()
            {
            }
        }

        public Logger(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            _testOutputHelper.WriteLine($"{logLevel}: {eventId}: {formatter(state, exception)}");
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => new Scope(state);
    }
}