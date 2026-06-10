using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Requests;

namespace Controllers;

public interface IFormInterceptor
{
    string ObjectTypeName { get; }
    string[] FormNames { get; }
    string[] ActionNames { get; }
}

public interface IInterceptBefore : IFormInterceptor
{
    ValueTask<Result<DataFormActionRequest>> ValidateRequestAsync(IEntityContext context, string objectTypeName, Guid? objectId, string formName, DataFormActionRequest request);
}

public interface IInterceptAfter : IFormInterceptor
{
    ValueTask<Result<DataFormActionResponse>> ProcessResponseAsync(IEntityContext context, string objectTypeName, Guid? objectId, string formName, DataFormActionRequest request, DataFormActionResponse response);
}

public interface IInterceptPrepareForm : IFormInterceptor
{
    ValueTask<Result<Form>> PrepareFormAsync(IEntityContext context, string formName, Form form, HttpRequest request);
}
