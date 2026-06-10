using System;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PI.Shared.OpenAPI
{
    class OperationIdFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // context.MethodInfo.Name
            // "GetAsync"
            // context.MethodInfo.DeclaringType.FullName
            // "Controllers.AppointmentTypeController"
            // context.MethodInfo.DeclaringType.Name
            // "AppointmentTypeController"
            // context.ApiDescription.RelativePath
            // "api/v1/AppointmentType"

            if (operation.OperationId == null)
            {
                operation.OperationId = context.MethodInfo.Name;
            }

            if (operation.OperationId.StartsWith("ApiV1", System.StringComparison.Ordinal))
            {
                operation.OperationId = operation.OperationId.Substring(5);
            }

            if (operation.OperationId.EndsWith("Async", System.StringComparison.Ordinal))
            {
                operation.OperationId = operation.OperationId.Substring(0, operation.OperationId.Length - 5);
            }

            if (context.ApiDescription.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor controllerActionDescriptor)
            {
                operation.OperationId = $"{controllerActionDescriptor.ControllerName}{operation.OperationId}";
            }
            else
            {
                var controller = context.MethodInfo.DeclaringType?.Name;
                if (controller?.EndsWith("Controller") ?? false)
                {
                    controller = controller[..^10];
                    operation.OperationId = $"{controller}{operation.OperationId}";
                }
                else
                {
                    Console.WriteLine(controller);
                }
            }
        }
    }
}
