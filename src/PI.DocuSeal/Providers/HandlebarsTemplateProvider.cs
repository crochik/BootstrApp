using System.Dynamic;
using Crochik.Dipper;
using Crochik.Mongo;
using HandlebarsDotNet;
using MongoDB.Bson;
using PI.DocuSeal.Models;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using Providers;

namespace PI.DocuSeal.Providers;

public class HandlebarsTemplateProvider(ILogger<HandlebarsTemplateProvider> logger, MongoConnection connection) : ITemplateProvider
{
    private readonly IHandlebars _handlebars = Handlebars.Create();
    private readonly Dictionary<string, AggregateStoredProcedure> sps = new();
    private readonly Dictionary<string, List<ExpandoObject>> cache = new();

    public TemplateEngine SupportedEngine => TemplateEngine.Handlebars;

    public Task<string?> RenderTemplateAsync(IEntityContext context, DocumentTemplate config, IDictionary<string, object>? objectContext)
    {
        try
        {
            objectContext ??= new Dictionary<string, object>();

            if (config.Inputs != null)
            {
                foreach (var input in config.Inputs)
                {
                    if (!ExpressionEvaluatorService.TryResolve(context, objectContext, input.Value, out var value))
                    {
                        throw new BadRequestException($"Could not resolve input value: {input.Key}");
                    }

                    objectContext[input.Key] = value;
                }
            }

            RegisterHelpers(context, config, objectContext);
            var template = _handlebars.Compile(config.Template);
            var result = template(objectContext);

            return Task.FromResult<string?>(result);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to render Handlebars template: {ex.Message}", ex);
        }
    }

    private void RegisterHelpers(IEntityContext entityContext, DocumentTemplate config, IDictionary<string, object> objectContext)
    {
        // Currency formatting helper
        RegisterHelper("currency", (writer, context, parameters) =>
        {
            
            if (parameters.Length ==1)
            {
                var amount = parameters[0] switch
                {
                    decimal d => d,
                    Decimal128 d => (decimal)d,
                    _ => default(decimal?),
                };

                if (amount.HasValue)
                {
                    writer.WriteSafeString($"${amount:F2}");
                }
                else
                {
                    logger.LogError("Can't convert number to currency: {Value} {Type}", parameters[0], parameters[0]?.GetType().FullName);
                    writer.WriteSafeString("$!!!,!!!.!!");
                }
            }
        });

        // Date formatting helper
        RegisterHelper("formatDate", (writer, context, parameters) =>
        {
            if (parameters.Length > 0 && parameters[0] is DateTime date)
            {
                writer.WriteSafeString(date.ToString("MMM dd, yyyy"));
            }
        });

        // // Status styling helper
        // _handlebars.RegisterHelper("statusClass", (writer, context, parameters) =>
        // {
        //     if (parameters.Length > 0 && parameters[0] is InvoiceStatus status)
        //     {
        //         var cssClass = status.ToString().ToLower();
        //         writer.WriteSafeString(cssClass);
        //     }
        // });

        // Conditional equality helper
        RegisterHelper("eq", (writer, options, context, parameters) =>
        {
            if (parameters.Length >= 2 && parameters[0]?.ToString() == parameters[1]?.ToString())
            {
                options.Template(writer, context);
            }
            else
            {
                options.Inverse(writer, context);
            }
        });

        // Greater than helper
        RegisterHelper("gt", (writer, options, context, parameters) =>
        {
            if (parameters.Length >= 2 &&
                decimal.TryParse(parameters[0]?.ToString(), out var val1) &&
                decimal.TryParse(parameters[1]?.ToString(), out var val2) &&
                val1 > val2)
            {
                options.Template(writer, context);
            }
            else
            {
                options.Inverse(writer, context);
            }
        });

        // // evaluation service 
        // hb.RegisterHelper("evaluateExpression", (writer, options, context, args) =>
        // {
        //    if (args.Length!=1) throw new Exception("Expected 1 argument");
        //    if (ExpressionEvaluatorService.TryResolve(entityContext, objectContext, "{{" + args[0] + "}}", out var result))
        //    {
        //        options.Template(writer, result);
        //    }
        //    else
        //    {
        //        options.Inverse(writer, context);
        //    }
        // });

        // stored procedures
        if (config.StoredProcedures != null)
        {
            foreach (var kvp in config.StoredProcedures)
            {
                RegisterHelper(kvp.Key, (writer, options, context, args) =>
                {
                    if (!sps.TryGetValue(kvp.Key, out var sp))
                    {
                        sp = connection.Filter<StoredProcedure, AggregateStoredProcedure>()
                            .Eq(x => x.AccountId, entityContext.AccountId)
                            .Eq(x => x.Id, kvp.Value)
                            .FirstOrDefault();

                        if (sp == null) throw new Exception($"{kvp.Key}: {kvp.Value} not found");

                        sps.Add(kvp.Key, sp);
                    }

                    var parms = default(Dictionary<string, object>);
                    if (sp.Parameters != null)
                    {
                        if (args.Length != sp.Parameters.Length)
                        {
                            // ...
                            throw new Exception("Arguments count mismatch");
                        }

                        parms = new Dictionary<string, object>(sp.Parameters.Length);
                        for (var c = 0; c < sp.Parameters.Length; c++)
                        {
                            parms[sp.Parameters[c].Name] = args.At<string>(c);
                        }
                    }

                    var hash = kvp.Key;
                    if (parms != null) hash += string.Join("-|-", sp.Parameters.Select(x => parms[x.Name]));
                    if (!cache.TryGetValue(hash, out var list))
                    {
                        list = sp.Execute<ExpandoObject>(connection, parms);
                        cache[hash] = list;
                    }

                    foreach (var item in list)
                    {
                        options.Template(writer, item);
                    }

                    if (list.IsEmpty())
                    {
                        options.Inverse(writer, context);
                    }
                });
            }
        }
    }

    public void RegisterHelper(string helperName, HandlebarsHelper helper)
    {
        _handlebars.RegisterHelper(helperName, helper);
    }
    
    public void RegisterHelper(string helperName, HandlebarsBlockHelper helper)
    {
        _handlebars.RegisterHelper(helperName, helper);
    }
}