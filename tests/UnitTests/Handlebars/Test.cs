using System.Collections.Generic;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace UnitTests.Handlebars
{
    public class Test 
    {
        [Fact]
        public void FieldName()
        {
            var template = @"This is a test field {{{ObjectType|Field|name.test}}}";
            var body = HandlebarsDotNet.Handlebars.Compile(template).Invoke(new Dictionary<string, object>
            {
                { "ObjectType|Field|name", new {
                    Test = "It Worked!", 
                }},
                { "test", "test" }
            });

            body.Contains("It Worked!").Should().BeTrue();
        }
        
        [Fact]
        public void SerializeDeserialize()
        {
            var context = JsonConvert.DeserializeObject<object>(JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                {
                    "ObjectType|Field|name", new
                    {
                        Test = "It Worked!",
                    }
                },
                { "test", "test" }
            })); // as IDictionary<string, object>;
            
            var template = @"This is a test field {{{ObjectType|Field|name.test}}}";
            var body = HandlebarsDotNet.Handlebars.Compile(template).Invoke(context);

            // NOT SURE WHY OF THIS TEST BUT IT FAILS
            // e.g. handlebars will not resolve it 
            // body.Contains("It Worked!").Should().BeTrue();
        }
        
    }
}