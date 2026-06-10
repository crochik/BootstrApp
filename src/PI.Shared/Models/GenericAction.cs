using System;
using System.Collections.Generic;
using Crochik.Mongo;

namespace PI.Shared.Models;

[BsonCollection("FlowAction")]
public class GenericAction : AppProfileElement
{
    public Guid ActionId { get; set; }
    public string IconName { get; set; }
    public string[] InputObjectTypes { get; set; }
    public string ActionOptionsObjectType { get; set; }

    public Dictionary<string, string> Outputs { get; set; }
    
    public GenericAction()
    {
    }
}