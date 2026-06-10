using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PI.Shared.Data.Models
{
    public class SerializationSettings  {
        public static JsonSerializerSettings Default = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DefaultValueHandling = DefaultValueHandling.Ignore,
        };

        public static JsonSerializerSettings WithDiscriminator = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DefaultValueHandling = DefaultValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            // TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple
        };

        // public static JsonMediaTypeFormatter DefaultFormatter = new JsonMediaTypeFormatter
        // {
        //     SerializerSettings = Default
        // };

        // public static JsonMediaTypeFormatter WithDiscriminatorFormatter = new JsonMediaTypeFormatter
        // {
        //     SerializerSettings = WithDiscriminator
        // };

    }
}