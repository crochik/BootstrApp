using System;

namespace Zapier.Models;

public class UserResponse
{
    public Guid? Id { get; set; }
    public string Name { get; set; }
    public Guid? OrganizationId { get; set; }
    public string RoleId { get; set; }
}