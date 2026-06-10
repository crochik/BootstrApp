using System;
using System.Collections.Generic;
using Crochik.Dipper;
using Crochik.Mongo;

namespace PI.Shared.Models;

public class XLSTransform
{
    public StoredProcedure StoredProcedure { get; set; }
}

[BsonCollection("filebox.EmailInbox")]
public class EmailInbox : FlowObjectModel
{
    public Dictionary<string, Guid> InitialObjectFlowId { get; set; }
    public Dictionary<string, Guid> InitialObjectStatusId { get; set; }
}

public class UserEmailAddress
{
    public string Name { get; set; }
    public string EmailAddress { get; set; }
}

[BsonCollection("filebox.EmailReceived")]
public class EmailReceived : FlowObjectModel
{
    public string Plain { get; set; }
    public UserEmailAddress From { get; set; }
    public UserEmailAddress[] To { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public EmailAttachment[] Attachments { get; set; }
    public Guid ParentId { get; set; }
}