using System;
using Crochik.Mongo;

namespace PI.Shared.Models;

[BsonCollection("app.Profile")]
public class AppProfile : AppElement
{
    public string InitialPage { get; set; }
    public string InitialMenu { get; set; }
    
    /// <summary>
    /// if set, this profile only applies to this client
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// If set, this profile only applies to this role
    /// </summary>
    public EntityRoleId? RoleId { get; set; }

    /// <summary>
    /// hierarchy of profile ids, order matter 
    /// </summary>
    public Guid[] OtherProfileIds { get; set; }
}