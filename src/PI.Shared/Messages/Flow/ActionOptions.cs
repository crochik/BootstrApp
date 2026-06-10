using System;
using System.Dynamic;
using System.Linq;
using Crochik.Mongo;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Messages.Flow;

public class ActionOutput
{
    public Guid? EventId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    
    /// <summary>
    /// Color to be used (any valid "html color", e.g. "red", "#ff00ff", ...)
    /// </summary>
    public string Color { get; set; }

    public ActionOutput()
    {
    }
}

public interface IActionOptions
{
    // string EventDescription { get; }
    ActionOutput[] Output { get; }
}

public enum UpdateOperation
{
    Create,
    Update,
    Upsert,
};

[DiscriminatorWithFallback]
public class ActionOptions : IActionOptions
{
    public virtual ActionOutput[] Output { get; set; }
}

[Obsolete("use ActionOptions")]
public class SimpleActionOptions : ActionOptions
{
    public Guid? NextEventId { get; set; }
    public Guid? ErrorEventId { get; set; }
}

/// <summary>
/// Action options to be used for generic actions
/// </summary>
public class GenericActionOptions : ActionOptions
{
    private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
    {
        ContractResolver = new DefaultContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };

    public ExpandoObject Raw { get; set; }

    public T ConvertTo<T>() => Convert<ExpandoObject, T>(Raw);

    public GenericActionOptions()
    {
    }

    public GenericActionOptions(ActionOptions options)
    {
        Raw = Convert<ActionOptions, ExpandoObject>(options);
    }

    public static TOut Convert<TIn, TOut>(TIn input)
    {
        var json = JsonConvert.SerializeObject(input, Formatting.None, JsonSerializerSettings);
        return JsonConvert.DeserializeObject<TOut>(json, JsonSerializerSettings);
    }
}

public static class IActionOptionsExtensions
{
    private static ActionOutput FindOutput(this IActionOptions options, Guid eventId) => options?.Output?.FirstOrDefault(x => x.EventId == eventId);

    public static string GetEventDescription(this IActionOptions options, Guid? eventId, string message = null)
    {
        var description = eventId.HasValue ? options.FindOutput(eventId.Value)?.Description : null;
        return string.IsNullOrWhiteSpace(message) ? description :
            string.IsNullOrWhiteSpace(description) ? message : $"{description}. {message}";
    }
}