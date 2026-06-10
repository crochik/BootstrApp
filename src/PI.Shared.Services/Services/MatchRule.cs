using Microsoft.Extensions.Logging;
using PI.Shared.Models;

namespace PI.Shared.Services
{
    public class MatchRule
    {
        public string Field { get; }
        public string Value { get; }

        public MatchRule(string field, string op, string value)
        {
            Field = field;
            Value = value;
        }

        public  bool Validate(ILogger logger, FieldMapperConfig[] fieldConfig, IIndexedProperties inflated)
        {
            // TODO: for now only assume op ==
            var value = inflated[Field];
            var result = (value == null) ?
                string.IsNullOrEmpty(Value) :
                value.Equals(Value);

            if (!result)
            {
                logger.LogInformation("Failed to validate {value} for {field}", value, Field);
            }
            
            return result;
        }
    }
}
