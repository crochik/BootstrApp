using System;

namespace PI.Shared.Form.Models;

public interface IDataResponse
{
    public Guid? Id { get; set; }
    public string ObjectType { get; set; }
    // public Guid? ObjectId { get; set; }
}