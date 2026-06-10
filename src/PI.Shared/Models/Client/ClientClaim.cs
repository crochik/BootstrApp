using System.Security.Claims;

namespace PI.Shared.Models.Client
{
    public class ClientClaim
    {
        public string Type { get; set; }
        public string Value { get; set; }

        /// <summary>
        /// The claim value type
        /// </summary>
        public string ValueType { get; set; } = ClaimValueTypes.String;
        
        public ClientClaim() { }
        
        public ClientClaim(string type, string value)
        {
            Type = type;
            Value = value;
        }
    }
}