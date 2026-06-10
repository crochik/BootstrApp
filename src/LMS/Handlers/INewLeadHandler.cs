using System;
using System.Threading.Tasks;
using LMS.Models;

namespace LMS.Handlers;

public interface INewLeadHandler
{
    Func<Context, ValueTask<Response>> Build(Func<Context, ValueTask<Response>> next);
}