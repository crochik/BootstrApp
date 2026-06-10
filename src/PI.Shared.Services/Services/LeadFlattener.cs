using System.Collections.Generic;
using PI.Shared.Data.Models;
using PI.Shared.Models;

namespace PI.Shared.Services;

public class LeadFlattener : Flattener
{
    public IEnumerable<FieldMapperConfig> Fields => Settings.Fields;

    public LeadTypeSettings Settings { get; }

    public LeadFlattener(LeadTypeSettings settings, IValueMapperService valueMapperService)
    {
        Settings = settings;

        Mapping = ParseMapping(settings, valueMapperService);
        ValidationRules = ParseRules(settings);
    }

    private static Field[] ParseMapping(LeadTypeSettings settings, IValueMapperService valueMapperService)
    {
        if (settings?.Fields == null) return null;
        return ParseMapping(settings.Fields, valueMapperService);
    }

    private static ValidationRule[] ParseRules(LeadTypeSettings settings)
    {
        if (settings?.PostValidation == null)
        {
            return null;
        }
        return ParseRules(settings.PostValidation);
    }
}