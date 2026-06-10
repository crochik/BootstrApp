using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Crochik.Mongo;

namespace PI.Shared.Models.Http;

public enum Method
{
    Get,
    Put,
    Delete,
    Post,
    Head,
    Trace,
    Patch,
    Options,
}

public class Request
{
    public string Url { get; set; }
    public Method Method { get; set; }
    public Dictionary<string, string[]> Headers { get; set; }
    public string Body { get; set; }
}

public class Response
{
    public Dictionary<string, string[]> Headers { get; set; }
    public string Body { get; set; }
    public HttpStatusCode StatusCode { get; set; }
    public bool Succeeded { get; set; }
    public DateTime ReceivedOn { get; set; }

    public bool IsSuccess => (int)StatusCode >= 200 && (int)StatusCode <= 299;

    public string ContentType
    {
        get
        {
            if (Headers == null || !Headers.TryGetValue("Content-Type", out var values) || values.Length < 1) return null;
            var parts = values.FirstOrDefault()?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts?[0];
        }
    }
}

[BsonCollection("http.CallOut")]
public class HttpCallOut : Model
{
    public Request Request { get; set; }
    public Response Response { get; set; }
    public Response[] FailedAttempts { get; set; }
    public DateTime? RetryAfter { get; set; }

    public List<KeyValuePair<string, object>> Refs { get; set; }
}