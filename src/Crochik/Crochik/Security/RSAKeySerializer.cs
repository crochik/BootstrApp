using System.Reflection;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Crochik.Security
{
    public class RSAKeySerializer
    {
        private readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings { ContractResolver = new IncludeAllContractResolver() };

        private RSAParameters Deserialize(string json)
            => JsonConvert.DeserializeObject<RSAParameters>(json, _serializerSettings);

        public string Serialize(RSAParameters parameters)
            => JsonConvert.SerializeObject(parameters, _serializerSettings);

        private RsaSecurityKey GetSecurityKey(string keyId, string json)
        {
            var rsa = Deserialize(json);
            return new RsaSecurityKey(rsa) { KeyId = keyId };
        }

        public SigningCredentials GetSigningCredentials(string keyId, string json)
        {
            var securityKey = GetSecurityKey(keyId, json);
            return new SigningCredentials(securityKey, "RS256");
        }

        private class IncludeAllContractResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);
                property.Ignored = false;
                return property;
            }
        }
    }
}