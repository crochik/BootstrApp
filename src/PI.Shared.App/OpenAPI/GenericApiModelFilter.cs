// using System;
// using System.Linq;
// using System.Reflection;
// using Microsoft.AspNetCore.Mvc.Controllers;
// using Microsoft.OpenApi.Models;
// using Swashbuckle.AspNetCore.Swagger;
// using Swashbuckle.AspNetCore.SwaggerGen;

// namespace OpenAPI
// {
//     class GenericApiModelFilter : IOperationFilter
//     {
//         public void Apply(OpenApiOperation operation, OperationFilterContext context)
//         {
//             if (!(context.ApiDescription.ActionDescriptor is ControllerActionDescriptor controllerDescriptor))
//             {
//                 return;
//             }

//             if (controllerDescriptor.ControllerTypeInfo.BaseType?.IsGenericType != true ||
//                 controllerDescriptor.ControllerTypeInfo.BaseType.GenericTypeArguments.Length != 3)
//             {
//                 return;
//             }

//             var name = controllerDescriptor.ControllerTypeInfo.FullName;
//             var baseType = controllerDescriptor.ControllerTypeInfo.BaseType;
//             var args = baseType.GenericTypeArguments;

//             var attrib = context.MethodInfo.GetCustomAttribute<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>();
//             if (attrib == null) return;
//             switch (attrib.HttpMethods.FirstOrDefault())
//             {
//                 case "GET":
//                 case "DELETE":
//                 case "PUT":
//                     if (string.Equals(attrib.Template, "/api/v1/[controller]({id})"))
//                     {
//                         var typeParam = args[2];
//                         AddOrUpdateSuccessfulResponse(operation, context, typeParam);
//                     }
//                     else if (string.Equals(attrib.Template, "/api/v1/[controller]"))
//                     {
//                         // TODO: have to figure out how to make an array
//                         var typeParam = args[2];
//                         AddOrUpdateSuccessfulResponse(operation, context, typeParam);
//                     }
//                     break;

//                 case "POST":
//                     if (string.Equals(attrib.Template, "/api/v1/[controller]"))
//                     {
//                         var typeParam = args[2];
//                         AddOrUpdateSuccessfulResponse(operation, context, typeParam);
//                     }
//                     break;
//             }
//         }

//         private static void AddOrUpdateSuccessfulResponse(OpenApiOperation operation, OperationFilterContext context, System.Type typeParam)
//         {
//             // Get the schema of the generic type. In case it's not there, you will have to create a schema for that model
//             // yourself, because Swagger may not have added it, because the type was not declared on any of the models
//             string typeParamFriendlyId = typeParam.FriendlyId();

//             if (!context.SchemaRegistry.Definitions.TryGetValue(typeParamFriendlyId, out Schema typeParamSchema))
//             {
//                 // Schema doesn't exist, you need to create it yourself, i.e. add properties for each property of your model.
//                 // See OpenAPI/Swagger Specifications
//                 typeParamSchema = context.SchemaRegistry.GetOrRegister(typeParam);

//                 // add properties here, without it you won't have a model description for this type
//             }

//             var schema = new Schema { Ref = $"#/definitions/{typeParamFriendlyId}" };

//             if (operation.Responses.TryGetValue("200", out var response))
//             {
//                 if (response.Schema?.Items != null)
//                 {
//                     response.Schema.Items = schema;
//                 }
//                 else
//                 {
//                     response.Schema = schema;
//                 }
//             }
//             else
//             {
//                 throw new NotImplementedException("not prepared to start schema fresh");
//                 // // for any get operation for which no 200 response exist yet in the document
//                 // operation.Responses.Add("200", new Response
//                 // {
//                 //     Description = "Success",
//                 //     Schema = schema,
//                 // });
//             }
//         }
//     }
// }
