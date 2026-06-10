using System;

namespace PI.Shared.Models.Interfaces;

public interface IWithAuthor
{
    public Guid? CreatorId { get; set; }
}