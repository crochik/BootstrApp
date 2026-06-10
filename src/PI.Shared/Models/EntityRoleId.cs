using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PI.Shared.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum EntityRoleId {
    User,
    Manager, 
    Admin,
    Root,
    Disabled,
    Organization,
    Account,
    Integration,
    Contact,
    Profile,
};