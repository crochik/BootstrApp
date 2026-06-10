using System.Net;
using LMS.Models;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Handlers;

public class HttpStatusResponseWriter : IResponseWriter
{
    public ActionResult Write(Context context, Response result) => new OkObjectResult(new
    {
        result.Success,
        result.Reason,
        LeadId = result.Lead?.Id,
        RequestId = context.Request.Id,
    })
    {
        StatusCode = (int)(result.Success ? HttpStatusCode.Created : HttpStatusCode.BadRequest),
    };
}