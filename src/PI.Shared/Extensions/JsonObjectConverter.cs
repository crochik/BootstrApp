using Newtonsoft.Json;
using PI.Shared.ContractResolvers;

namespace PI.Shared.Extensions;

public static class JsonObjectConverter
{
    private static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        ContractResolver = new AlwaysUseUnderlyingPropertyNameContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };

    public static TOut Convert<TOut>(object input)
    {
        var json = SerializeObject(input);
        return DeserializeObject<TOut>(json);
    }

    public static string SerializeObject(object input) => JsonConvert.SerializeObject(input, Formatting.None, JsonSerializerSettings);
    public static T DeserializeObject<T>(string input) => JsonConvert.DeserializeObject<T>(input, JsonSerializerSettings);
}