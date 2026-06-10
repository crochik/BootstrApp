using Microsoft.Extensions.Logging;
using PI.Shared.Models;

namespace PI.Shared.Services
{
    public static class RequiredFieldsRule
    {
        public static bool Validate(ILogger logger, FieldMapperConfig[] fieldConfig, IIndexedProperties inflated)
        {
            bool valid = true;
            foreach (var field in fieldConfig)
            {
                if (!field.IsRequired) continue;
                
                var value = inflated[field.Name];
                if (string.IsNullOrEmpty(value))
                {
                    logger.LogInformation("Missing required {field}", field.Name);
                    valid = false;
                }
            }

            return valid;
        }
    }
}
