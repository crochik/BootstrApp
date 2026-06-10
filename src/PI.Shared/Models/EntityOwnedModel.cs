using System;

namespace PI.Shared.Models;

public class EntityOwnedModel : Model, IEntityOwnedModel
{
    public string Description { get; set; }
    public Guid EntityId { get; set; }
}