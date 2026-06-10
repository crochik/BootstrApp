using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using PI.Shared.Constants;

namespace Messages.Flow;

public class ExportToSalesforceActionOptions : ActionOptions
{
    public Guid? NextEventId { get; set; }
    public Guid? ErrorEventId { get; set; }

    public string ObjectType { get; set; }
    public bool MapAllFields { get; set; } = true;

    /// <summary>
    /// original version of mapping, key is the salesforce property name, value is the PI (old) "mapping expression"
    /// kept around to convert into PropertiesMapping
    /// </summary>
    [Obsolete("replaced by PropertiesMapping")]
    public List<KeyValuePair<string, string>> Mapping
    {
        get => null;
        set
        {
            if (value != null && !value.IsEmpty())
            {
                _propertiesMapping = value
                    .DistinctBy(x => x.Key)
                    .ToDictionary(x => x.Key.Replace('.', '|'), x => x.Value);
            }
        }
    }

    private Dictionary<string, string> _propertiesMapping;

    /// <summary>
    /// New representation of the properties mapping
    /// key is the salesforce property name, value is the PI (old) "mapping expression"
    /// </summary>
    public Dictionary<string, string> PropertiesMapping
    {
        get => _propertiesMapping;
        set
        {
            if (value != null)
            {
                _propertiesMapping = value;
            }
        }
    }

    public bool ForcePlainPhoneNumber { get; set; } = true;
    public override ActionOutput[] Output { get; set; }
}

public class ExportToSalesforceAction : FlowAction<ExportToSalesforceActionOptions, ExportToSalesforceAction.Message>
{
    public override Guid Id => ActionIds.ExportToSalesforce;

    public class Message : SimpleActionMessage<ExportToSalesforceActionOptions>
    {
        public Message()
        {
        }

        public Message(FlowEvent evt, IActionOptions options) : base(evt, options)
        {
        }
    }
}