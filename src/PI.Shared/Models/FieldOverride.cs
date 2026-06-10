using System;

namespace PI.Shared.Models;

[Flags]
public enum FieldOverride
{
    None = 0, // pass through
    Field,
    RBAC,
    Options,
    // Index,
}