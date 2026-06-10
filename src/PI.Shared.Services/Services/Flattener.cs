using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;

namespace PI.Shared.Services;

public delegate object ValueMapper(FieldMapperConfig config, object body, IIndexedProperties lead);
public delegate bool Validator(ILogger logger, FieldMapperConfig[] fieldConfig, IIndexedProperties inflated);

public class Flattener
{
    protected const string FORMULA_METHOD = @"^=(?<cmd>\w+)\( ((?<arg>[^,\s]+),?\s?)* \)$";
    protected const string MATCH_CONDITION = @"^(?<field>\w+) \s* (?<op>[!=]=) \s* '(?<value>[^']*)'$";

    // protected static IValueMapperService _valueMapperService;
    // public static void Register(IValueMapperService valueMapperService)
    // {
    //     _valueMapperService = valueMapperService;
    // }

    protected static bool IsFormula(FieldMapperConfig field)
    {
        return field.Source.StartsWith("=", StringComparison.Ordinal);
    }

    protected static readonly Dictionary<string, ValueMapper> _formulae = new()
    {
        {"=firstName+' '+lastName", NameFromParts.Calculate},
        {"='{{firstName}} {{lastName}}'", NameFromParts.Calculate},
    };

    protected static readonly Dictionary<string, Validator> _validationRules = new()
    {
        {"requiredFields()", RequiredFieldsRule.Validate}
    };

    protected static Field[] ParseMapping(FieldMapperConfig[] config, IValueMapperService valueMapperService)
    {
        var mapping = new List<Field>();
        var formulae = new List<FieldMapperConfig>();
        foreach (var field in config)
        {
            if (IsFormula(field))
            {
                formulae.Add(field);
                continue;
            }

            mapping.Add(new Field
            {
                Config = field,
                Mapper = FieldMapper.Map
            });
        }

        // and then apply formulae
        foreach (var field in formulae)
        {
            if (_formulae.TryGetValue(field.Source, out var mapper))
            {
                mapping.Add(new Field
                {
                    Config = field,
                    Mapper = mapper
                });
                continue;
            }

            var matches = Regex.Matches(field.Source, FORMULA_METHOD, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);
            if (matches.Count == 1)
            {
                var cmd = matches[0].Groups[1].Captures[0].Value;
                var args = matches[0].Groups[2].Captures.ToList().ConvertAll((c) => c.Value);
                switch (cmd)
                {
                    case "map":
                        var fieldMapper = valueMapperService.CreateMaper(field, cmd, args.ToArray());
                        if (fieldMapper != null)
                        {
                            mapping.Add(new Field
                            {
                                Config = field,
                                Mapper = fieldMapper
                            });
                        }
                        else
                        {
                            // ...
                        }
                        break;

                    default:
                        // ...
                        break;
                }
                continue;
            }

            // error?
            // ...
        }

        return mapping.Count == 0 ? null : mapping.ToArray();
    }

    protected static ValidationRule[] ParseRules(IEnumerable<string> rules)
    {
        var list = new List<ValidationRule>();
        foreach (var str in rules)
        {
            if (_validationRules.TryGetValue(str, out var rule))
            {
                list.Add(new ValidationRule
                {
                    Condition = str,
                    Validate = rule
                });
                continue;
            }

            var matches = Regex.Matches(str, MATCH_CONDITION, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);
            if (matches.Count == 1)
            {
                var field = matches[0].Groups[1].Captures[0].Value;
                var op = matches[0].Groups[2].Captures[0].Value;
                var value = matches[0].Groups[3].Captures[0].Value;
                list.Add(new ValidationRule
                {
                    Condition = str,
                    Validate = new MatchRule(field, op, value).Validate
                });
            }

            // TODO: handle rule not found????
            // ...
        }

        return list.Count == 0 ? null : list.ToArray();
    }

    // public IEnumerable<FieldMapperConfig> Fields {get;}
    public IEnumerable<ValidationRule> ValidationRules { get; protected set; }
    public IEnumerable<Field> Mapping { get; protected set; }

    public class Field
    {
        public ValueMapper Mapper { get; set; }
        public FieldMapperConfig Config { get; set; }
    }

    public class ValidationRule
    {
        public string Condition { get; set; }
        public Validator Validate { get; set; }
    }
}