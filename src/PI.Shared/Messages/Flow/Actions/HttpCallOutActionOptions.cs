using System;
using System.Collections.Generic;
using PI.Shared.Models.Http;

namespace Messages.Flow;

public class HttpCallOutActionOptions : ActionOptions
{
    public string Url { get; set; }
    public Method Method { get; set; }
    public string Body { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public override ActionOutput[] Output { get; set; }
    public Guid? NextEventId { get; set; }
}