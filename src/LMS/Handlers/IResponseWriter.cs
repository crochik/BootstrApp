using LMS.Models;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Handlers;

public interface IResponseWriter
{
    ActionResult Write(Context context, Response result);
}