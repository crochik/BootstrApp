using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace PI.Shared.Models.Expressions;

public interface IFunction
{
    string Name { get; }
    bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value);
}

public class FailedToResolveExpressionException : Exception
{
    public object Expression { get; set; }

    public FailedToResolveExpressionException(object expression) : base($"Couldn't resolve {expression}")
    {
        Expression = expression;
    }
}

public class ExpressionEvaluatorService
{
    private static ExpressionEvaluatorService Instance { get; } = new();
    private const string RegExStr = @"({{[_""\sa-zA-Z\.\|0-9\?!]+}})";

    private IFunction[] Functions { get; }
    private Dictionary<string, IFunction> NamedFunctions { get; }
    private IFunction[] NoArgumentFunctions { get; }

    public class Context
    {
        public IEntityContext EntityContext { get; init; }
        public IDictionary<string, object> Data { get; init; }
        public ILogger Logger { get; init; }
    }

    private static IEnumerable<IFunction> GetFunctions()
    {
        yield return new ContextFunction();
        yield return new NewFunction();
        yield return new FirstNameFunction();
        yield return new ToStringFunction();
        yield return new UpperFunction();
        yield return new LowerFunction();
        yield return new LastNameFunction();
        yield return new ToISODateFunction();
        yield return new ToObjectIdFunction();
        yield return new ToDateFunction();
        yield return new DateAddFunction();
        yield return new NormalizeEmailFunction();
        yield return new NormalizePhoneFunction();
        yield return new PostalCodeLookupFunction();
        yield return new ConcatenateFunction();
        yield return new UrlEncodeFunction();
        yield return new UriEscapeDataStringFunction();
        yield return new JoinFunction();
        yield return new ToDecimalFunction();
        yield return new CoalesceFunction();
        yield return new ArrayToObjectFunction();
        yield return new ArrayToDictionaryFunction();
        yield return new ToArrayFunction();
        yield return new GetFileExtensionFunction();
        yield return new ToSafeKeyFunction();
        yield return new RandomStringFunction();

        // values
        yield return new ContextValueFunction();
        yield return new ConstantValueFunction();
        yield return new DataValueFunction();
        yield return new StringArgumentValueFunction();
    }

    public ExpressionEvaluatorService()
    {
        Functions = GetFunctions().Reverse().ToArray();
        NamedFunctions = Functions.ToDictionary(x => x.Name);
        foreach (var f in Functions)
        {
            if (!f.Name.StartsWith('@')) NamedFunctions.Add($"@{f.Name}", f);
        }

        NoArgumentFunctions = Functions.Where(x => x.Name.StartsWith('@')).ToArray();
    }

    public static bool TryResolve(IEntityContext context, IDictionary<string, object> objectContext, object input, out object value)
    {
        value = input;

        return input switch
        {
            string str => TryResolve(context, objectContext, str, out value),
            IEnumerable<object> en => TryResolve(context, objectContext, en, out value),
            _ => true,
        };
    }

    private static Result<object> TryResolveValueRecursively(IEntityContext context, IDictionary<string, object> objectContext, object input)
    {
        if (input is IDictionary<string, object> dict)
        {
            return TryResolveRecursively(context, objectContext, dict).ConvertTo<object>();
        }
        
        if (input is IEnumerable<object> array)
        {
            var list = new List<object>();
            foreach (var item in array)
            {
                var childResult = TryResolveValueRecursively(context, objectContext, item);
                if (childResult.IsError) return Result.Error<object>($"{childResult.Status}");
                list.Add(childResult.Value);
            }

            return Result.Success<object>(list);
        }

        if (input is not string str)
        {
            // nothing to do ?
            return Result.Success(input);
        }
        
        if (!TryResolve(context, objectContext, str, out var value))
        {
            return Result.Error<object>($"Failed to resolve: {str}");
        }
 
        return Result.Success(value);
    }

    public static Result<Dictionary<string, object>> TryResolveRecursively(IEntityContext context, IDictionary<string, object> objectContext, IDictionary<string, object> input)
    {
        var output = new Dictionary<string, object>();

        foreach (var kvp in input)
        {
            if (!TryResolve(context, objectContext, kvp.Key, out var keyObj) || keyObj is not string key)
            {
                key = kvp.Key;
            }

            var valueResult = TryResolveValueRecursively(context, objectContext, kvp.Value);
            if (valueResult.IsError)
            {
                return Result.Error<Dictionary<string, object>>($"{kvp.Key}: {valueResult.Status}");
            }
            
            if (valueResult.Value != null)
            {
                output.Add(key, valueResult.Value);
            }

            // if (kvp.Value is IDictionary<string, object> dict)
            // {
            //     var childResult = TryResolveRecursively(context, objectContext, dict);
            //     if (childResult.IsError) return Result.Error<Dictionary<string, object>>($"{kvp.Key}: {childResult.Status}");
            //     output.Add(key, childResult.Value);
            //     continue;
            // }
            //
            // if (kvp.Value is not string str)
            // {
            //     if (kvp.Value != null)
            //     {
            //         output.Add(key, kvp.Value);
            //     }
            //
            //     continue;
            // }
            //
            // if (!TryResolve(context, objectContext, str, out var value))
            // {
            //     return Result.Error<Dictionary<string, object>>($"Failed to resolve {kvp.Key}: {str}");
            // }
            //
            // if (value != null)
            // {
            //     output.Add(key, value);
            // }
        }

        return Result.Success(output);
    }

    public static bool TryResolve(IEntityContext context, IDictionary<string, object> objectContext, string str, out object value)
        => Instance.TryResolve(new Context
            {
                EntityContext = context,
                Data = objectContext,
            },
            str,
            out value
        );

    private static bool TryResolve(IEntityContext context, IDictionary<string, object> objectContext, IEnumerable<object> en, out object value)
    {
        var changed = false;
        var result = Enumerable.Empty<object>();
        foreach (var item in en)
        {
            if (item is string str)
            {
                if (!TryResolve(context, objectContext, str, out var resolved))
                {
                    value = null;
                    return false;
                }

                result = result.Append(resolved);
                changed = true;
                continue;
            }

            result = result.Append(item);
        }

        value = changed ? result.ToArray() : en;
        return true;
    }

    public bool TryResolve(Context context, string str, out object value)
    {
        if (str == null || !str.Contains("{{") || !str.Contains("}}"))
        {
            value = str;
            return true;
        }

        var matches = Regex.Matches(str, RegExStr);
        if (matches.Count > 1 || !str.StartsWith("{{") || !str.EndsWith("}}"))
        {
            value = str;
            var newStr = str;
            foreach (var m in matches)
            {
                var expr = m.ToString();
                if (expr == null) return false;

                var argument = expr[2..^2];
                if (!TryResolveExpression(context, argument, out var arg))
                {
                    return false;
                }

                if (arg == null)
                {
                    newStr = newStr.Replace(expr, string.Empty);
                    continue;
                }

                if (arg is not string argStr) argStr = arg?.ToString();

                newStr = newStr.Replace(expr, argStr);
            }

            value = newStr;
            return true;
        }

        if (!TryResolveExpression(context, str[2..^2], out value))
        {
            return false;
        }

        context.Logger?.LogInformation("Resolved {Expression}: {Resolved}", str, value);
        return true;
    }

    private bool TryResolveExpression(Context context, string str, out object value)
    {
        var parts = str.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var argList = new List<object>();
        for (var c = parts.Length - 1; c >= 0; c--)
        {
            var token = parts[c];
            var required = token.EndsWith('!');
            var optional = token.EndsWith('?');
            token = optional || required ? token[..^1] : token;

            if (TryResolveSingleValue(context, token, out var singleValue))
            {
                if (c == 0)
                {
                    context.Logger?.LogInformation("Resolved {Expression}: {Resolved}", str, singleValue);
                    value = singleValue;
                    return true;
                }

                context.Logger?.LogInformation("Resolved {Token}: {Resolved}", token, singleValue);
                argList.Add(singleValue);
                continue;
            }

            if (required)
            {
                context.Logger?.LogError("Required {Token} not found, abort expression", token);
                value = str;
                return false;
            }

            if (optional)
            {
                context.Logger?.LogInformation("Optional {Token} not found, use NULL", token);

                if (parts.Length == 1)
                {
                    value = null;
                    return true;
                }

                argList.Add(null);
                continue;
            }

            if (argList.Count == 0)
            {
                // can't be a named function
                context.Logger?.LogInformation("Didn't Resolved {Token}, use as it is", token);
                argList.Add(token);
                continue;
            }

            // try function
            if (!NamedFunctions.TryGetValue(token.StartsWith("@") ? token[1..] : token, out var f))
            {
                context.Logger?.LogInformation("Didn't Resolved {Token}, use as it is", token);
                argList.Add(token);
                continue;
            }

            var args = argList.Select(x => x).Reverse().ToArray();
            if (f.TryEvaluate(context, args, out value))
            {
                if (c == 0)
                {
                    context.Logger?.LogInformation("{Function}: Resolved Expression into {Resolved}", token, value);
                    return true;
                }

                context.Logger?.LogInformation("{Function} evaluated to {Resolved}", token, value);
                argList.Clear();
                argList.Add(value);
                continue;
            }

            context.Logger?.LogInformation("{Function} fail to evaluate, may be just an argument", token);
            argList.Add(token);
        }

        context.Logger?.LogError("Couldn't resolve {Expression}", str);

        value = str;
        return false;
    }

    private bool TryResolveSingleValue(Context context, string str, out object value)
    {
        var args = new object[] { str };
        foreach (var f in NoArgumentFunctions)
        {
            if (f.TryEvaluate(context, args, out value)) return true;
        }

        value = str;
        return false;
    }
}

public class ContextFunction : IFunction
{
    public virtual string Name => "context";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (context.EntityContext == null) return false;
        if (arguments.Length != 1 || arguments[0] is not string str) return false;

        switch (str)
        {
            case "AccountId":
                value = context.EntityContext.AccountId;
                return true;
            case "OrganizationId":
                value = context.EntityContext.Role switch
                {
                    // the organization for "an account/admin" is the account itself
                    EntityRoleId.Account => context.EntityContext.AccountId,
                    EntityRoleId.Admin => context.EntityContext.AccountId,
                    // users 
                    EntityRoleId.Manager => context.EntityContext.OrganizationId,
                    EntityRoleId.Organization => context.EntityContext.OrganizationId,
                    EntityRoleId.User => context.EntityContext.OrganizationId,
                    // others
                    _ => null,
                };
                return true;
            case "UserId":
                value = context.EntityContext.UserId;
                return true;
            case "EntityId":
                value = context.EntityContext.EntityId;
                return true;
            case "ProfileId":
                value = context.EntityContext.ProfileId;
                return true;
            case "AllProfileIds":
                value = context.EntityContext.AllProfileIds;
                return true;
            case "ClientId":
                value = context.EntityContext.ClientId;
                return true;
            case "Actor":
                value = context.EntityContext.Actor();
                return true;

            case "AllUserIds":
                value = context.EntityContext.GetAllUserIds().Select(x => (object)x).ToArray();
                return true;
            case "AllOrganizationIds":
                value = context.EntityContext.GetAllOrganizationIds().Select(x => (object)x).ToArray();
                return true;
            case "AllEntityIds":
                value = context.EntityContext.GetAllEntityIds().Select(x => (object)x).ToArray();
                return true;
        }

        if (Name.StartsWith('@'))
        {
            // stop bleeding, do not add more "naked" context options
            return false;
        }

        if (str.StartsWith("Claims."))
        {
            var claim = str["Claims.".Length..];
            if (context.EntityContext?.Claims == null) return false;
            if (!context.EntityContext.Claims.TryGetValue(claim, out var strValue))
            {
                strValue = null;
            }

            value = strValue;
            return true;
        }

        return false;
    }
}

public class ContextValueFunction : ContextFunction
{
    public override string Name => "@contextValue";
}

public class NewFunction : IFunction
{
    public string Name => "new";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1 || arguments[0] is not string str) return false;
        switch (str)
        {
            case "Date":
                value = DateTime.UtcNow;
                return true;

            case "UUID":
                value = Model.NewGuid();
                return true;

            case "ObjectId":
                value = Model.NewObjectId();
                return true;

            default:
                return false;
        }
    }
}

public class ConstantValueFunction : IFunction
{
    public string Name => "@constant";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1 || arguments[0] is not string str) return false;
        switch (str)
        {
            case "NULL":
                return true;

            case "TRUE":
                value = true;
                return true;

            case "FALSE":
                value = false;
                return true;

            default:
                return false;
        }
    }
}

public class DataValueFunction : IFunction
{
    public string Name => "@data";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1 || arguments[0] is not string str || context.Data == null) return false;

        // if (str.EndsWith("?"))
        // {
        //     // optional, will always resolve 
        //     if (!context.Data.TryResolveValue(str[..^1].Split('.'), out value))
        //     {
        //         value = null;
        //     }
        //
        //     return true;
        // }

        return context.Data.TryResolveValue(str.Split('.'), out value);
    }
}

public class RandomStringFunction : IFunction
{
    private const string Base62 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const string Base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Base16 = "0123456789ABCDEF";
    private const string Base10 = "0123456789";

    public string Name => "rndStr";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 2) return false;
        if (!tryGetInt(arguments[0], out int baseValue)) return false;
        if (!tryGetInt(arguments[1], out int length)) return false;

        var dict = baseValue switch
        {
            10 => Base10,
            16 => Base16,
            36 => Base36,
            62 => Base62,
            _ => Base16,
        };

        var str = "";
        for (var c = 0; c < length; c++)
        {
            var rnd = Random.Shared.Next(dict.Length);
            str += dict[rnd];
        }

        value = str;

        return true;

        bool tryGetInt(object val, out int i)
        {
            var resolved = val switch
            {
                int iValue => iValue,
                decimal d => (int)d,
                long l => (int)l,
                string str => int.TryParse(str, out int parsed) ? parsed : default(int?),
                _ => default(int?),
            };

            if (resolved.HasValue)
            {
                i = resolved.Value;
                return true;
            }

            i = default;
            return false;
        }
    }
}

public class ToSafeKeyFunction : IFunction
{
    public string Name => "toSafeKey";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length < 1) return false;
        var tokens = arguments.Where(x => x != null).Select(x => FunctionExtensions.ToSafeKey(x.ToString()));

        value = string.Join(string.Empty, tokens);

        return true;
    }
}

public class FirstNameFunction : IFunction
{
    public string Name => "firstName";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1 || arguments[0] is not string str) return false;
        if (!PersonName.TryParse(str, out var parsed)) return false;

        value = parsed.FirstName;
        return true;
    }
}

public class LastNameFunction : IFunction
{
    public string Name => "lastName";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1 || arguments[0] is not string str) return false;
        if (!PersonName.TryParse(str, out var parsed)) return false;

        value = parsed.LastName;
        return true;
    }
}

public class ToDateFunction : IFunction
{
    public string Name => "toDate";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1) return false;
        switch (arguments[0])
        {
            case DateTime dt:
            {
                value = dt;
                return true;
            }
            case string str:
            {
                if (DateTime.TryParse(str, out var dt))
                {
                    value = dt;
                    return true;
                }

                break;
            }
        }

        return false;
    }
}

public class ToISODateFunction : IFunction
{
    public string Name => "toISODate";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1) return false;
        switch (arguments[0])
        {
            case DateTime dt:
            {
                value = BsonDateTime.Create(dt);
                return true;
            }
            case string str:
            {
                if (DateTime.TryParse(str, out var dt))
                {
                    value = BsonDateTime.Create(dt);
                    return true;
                }

                break;
            }
            case BsonDateTime bdt:
                value = bdt;
                return true;
        }

        return false;
    }
}

public class ToObjectIdFunction : IFunction
{
    public string Name => "toObjectId";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1) return false;
        switch (arguments[0])
        {
            case ObjectId id:
            {
                value = id;
                return true;
            }
            case string str:
            {
                if (ObjectId.TryParse(str, out var objectId))
                {
                    value = objectId;
                    return true;
                }

                if (Guid.TryParse(str, out var guid) && guid.TryGetObjectId(out objectId))
                {
                    value = objectId;
                    return true;
                }

                break;
            }
            case Guid guid:
            {
                if (guid.TryGetObjectId(out var objectId))
                {
                    value = objectId;
                    return true;
                }

                break;
            }
        }

        return false;
    }
}

public class NormalizeEmailFunction : IFunction
{
    public string Name => "normalizeEmail";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1 || arguments[0] is not string str) return false;
        value = Lead.GetNormalizedEmail(str);
        return true;
    }
}

public class NormalizePhoneFunction : IFunction
{
    public string Name => "normalizePhone";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1 || arguments[0] is not string str) return false;
        value = Lead.GetNormalizedPhoneNumber(str);
        return true;
    }
}

public class ConcatenateFunction : IFunction
{
    public string Name => "concatenate";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length < 1) return false;
        value = string.Join(' ', arguments.Select(x => x?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)));
        return true;
    }
}

public class UrlEncodeFunction : IFunction
{
    public string Name => "urlEncode";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1 || arguments[0] is not string str) return false;
        value = WebUtility.UrlEncode(str);
        return true;
    }
}

public class UriEscapeDataStringFunction : IFunction
{
    public string Name => "uriEscapeDataString";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1 || arguments[0] is not string str) return false;
        value = Uri.EscapeDataString(str);
        return true;
    }
}

public class JoinFunction : IFunction
{
    public string Name => "join";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length < 2) return false;
        if (arguments[0] is not string joinStr) return false;

        value = string.Join(joinStr, arguments.Skip(1).Select(x => x?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)));
        return true;
    }
}

public class DateAddFunction : IFunction
{
    public string Name => "dateAdd";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length < 2) return false;

        var date = default(DateTime?);
        var unit = default(string);
        var amount = default(int?);
        foreach (var arg in arguments)
        {
            switch (arg)
            {
                case string str:
                {
                    if (!date.HasValue && DateTime.TryParse(str, out var dt))
                    {
                        date = dt;
                        break;
                    }

                    if (!amount.HasValue && int.TryParse(str, out var qtty))
                    {
                        amount = qtty;
                        break;
                    }

                    if (unit == null)
                    {
                        switch (str)
                        {
                            case "day":
                            case "days":
                                unit = "day";
                                break;
                            case "month":
                            case "months":
                                unit = "month";
                                break;
                            case "hour":
                            case "hours":
                                unit = "hour";
                                break;
                            case "minute":
                            case "minutes":
                                unit = "minute";
                                break;

                            default:
                                return false;
                        }
                    }

                    break;
                }

                case DateTime dt:
                {
                    if (date.HasValue) return false;
                    date = dt;
                    break;
                }

                case int i:
                {
                    if (amount.HasValue) return false;
                    amount = i;
                    break;
                }

                default:
                    return false;
            }
        }

        if (!date.HasValue || unit == null || !amount.HasValue) return false;

        value = unit switch
        {
            "day" => date.Value.AddDays(amount.Value),
            "month" => date.Value.AddMonths(amount.Value),
            "hour" => date.Value.AddHours(amount.Value),
            "minute" => date.Value.AddMinutes(amount.Value),
            _ => null,
        };

        return true;
    }
}

public class PostalCodeLookupFunction : IFunction
{
    public string Name => "postalCodeLookup";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1 || arguments[0] is not string str) return false;
        value = GetPostalCodeForLookup(str);
        return true;
    }

    private static string GetPostalCodeForLookup(string postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode)) return postalCode;
        if (postalCode[0] >= '0' && postalCode[0] <= '9')
        {
            switch (postalCode.Length)
            {
                case < 4: return postalCode;
                case > 5:
                    postalCode = postalCode[..5];
                    break;
            }

            if (!int.TryParse(postalCode, out var num)) return postalCode;
            postalCode = num.ToString();
            if (postalCode.Length < 4) return postalCode;
            return postalCode.Length == 4 ? "0" + postalCode : postalCode;
        }

        return postalCode.Length < 3 ? postalCode : postalCode[..3].ToUpperInvariant();
    }
}

public class ToStringFunction : IFunction
{
    public string Name => "toString";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1) return false;
        value = arguments[0].ToString();
        return true;
    }
}

public class UpperFunction : IFunction
{
    public string Name => "upper";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1 || arguments[0] is not string str) return false;
        value = str.ToUpperInvariant();
        return true;
    }
}

public class LowerFunction : IFunction
{
    public string Name => "lower";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1 || arguments[0] is not string str) return false;
        value = str.ToLowerInvariant();
        return true;
    }
}

public class StringArgumentValueFunction : IFunction
{
    public string Name => "@stringArgument";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1 || arguments[0] is not string str) return false;
        if ((str.StartsWith('"') && str.EndsWith('"')) || (str.StartsWith('\'') && str.EndsWith('\'')))
        {
            value = str[1..^1];
            return true;
        }

        return false;
    }
}

public class ToDecimalFunction : IFunction
{
    public string Name => "toDecimal";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1) return false;

        switch (arguments[0])
        {
            case int i:
                value = (decimal)i;
                break;
            case decimal d:
                value = d;
                break;
            case long l:
                value = (decimal)l;
                break;
            case string str:
                if (!decimal.TryParse(str, out var dec)) return false;
                value = dec;
                break;
            default:
                return false;
        }

        return true;
    }
}

public class CoalesceFunction : IFunction
{
    public string Name => "coalesce";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length < 1) return false;
        foreach (var i in arguments)
        {
            switch (i)
            {
                case null:
                case string str when string.IsNullOrWhiteSpace(str):
                    continue;

                default:
                    value = i;
                    return true;
            }
        }

        return false;
    }
}

public class ArrayToObjectFunction : IFunction
{
    public string Name => "arrayToObject";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 2) return false;
        if (arguments[0] is not string keyPath || !keyPath.StartsWith(".")) return false;
        if (arguments[1] is not IEnumerable a) return false;

        var parts = keyPath[1..].Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var dict = new Dictionary<string, object>();
        foreach (var child in a)
        {
            if (child is not IDictionary<string, object> kvp) continue;
            if (!kvp.TryResolveValue(parts, out var v) || v == null) continue;
            dict[FunctionExtensions.ToSafeKey(v.ToString())] = kvp;
        }

        value = dict;
        return true;
    }
}

public static class FunctionExtensions
{
    public static string ToSafeKey(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;

        return new string(chars().ToArray());

        IEnumerable<char> chars()
        {
            var upper = true;
            foreach (var c in str)
            {
                if (c is >= 'a' and <= 'z')
                {
                    if (upper)
                    {
                        yield return (char)(c + 'A' - 'a');
                    }
                    else
                    {
                        yield return c;
                    }

                    upper = false;
                    continue;
                }

                if (c is >= 'A' and <= 'Z')
                {
                    if (!upper)
                    {
                        yield return (char)(c + 'a' - 'A');
                    }
                    else
                    {
                        yield return c;
                    }

                    upper = false;
                    continue;
                }

                if (c is >= '0' and <= '9')
                {
                    yield return c;
                    upper = false;
                    continue;
                }

                upper = true;
            }
        }
    }
}

public class ArrayToDictionaryFunction : IFunction
{
    public string Name => "arrayToDictionary";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 3) return false;
        if (arguments[0] is not string keyPath || !keyPath.StartsWith(".")) return false;
        if (arguments[1] is not string valuePath || !valuePath.StartsWith(".")) return false;
        if (arguments[2] is not IEnumerable a) return false;

        var keyParts = keyPath[1..].Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var valueParts = valuePath[1..]
            .Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var dict = new Dictionary<string, object>();
        foreach (var child in a)
        {
            if (child is not IDictionary<string, object> kvp) continue;
            if (!kvp.TryResolveValue(keyParts, out var k) || k == null) continue;
            if (!kvp.TryResolveValue(valueParts, out var v) || v == null) continue;
            dict[FunctionExtensions.ToSafeKey(k.ToString())] = v;
        }

        value = dict;
        return true;
    }
}

public class ToArrayFunction : IFunction
{
    public string Name => "toArray";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length < 1) return false;

        value = getNonNull().ToArray();
        return true;

        IEnumerable<object> getNonNull()
        {
            foreach (var i in arguments)
            {
                switch (i)
                {
                    case null:
                    case string str when string.IsNullOrWhiteSpace(str):
                        break;

                    default:
                        yield return i;
                        break;
                }
            }
        }
    }
}

public class GetFileExtensionFunction : IFunction
{
    public string Name => "getFileExtension";

    public bool TryEvaluate(ExpressionEvaluatorService.Context context, object[] arguments, out object value)
    {
        value = null;
        if (arguments.Length != 1 || arguments[0] is not string str) return false;

        var index = str.LastIndexOf('.');
        value = index < 0 ? string.Empty : str[(index + 1)..].ToLowerInvariant();
        return true;
    }
}