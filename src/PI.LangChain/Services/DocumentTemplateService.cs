using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Crochik.Dipper;
using Crochik.Mongo;
using HandlebarsDotNet;
using Microsoft.Extensions.Logging;
using PI.LangChain.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Services;

public class DocumentTemplateService
{
    private readonly ILogger<DocumentTemplateService> _logger;
    private readonly MongoConnection _connection;

    public DocumentTemplateService(ILogger<DocumentTemplateService> logger, MongoConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    public string GetSystemPrompt(IEntityContext context, Assistant assistant, IDictionary<string, object> objectContext)
    {
        if (string.IsNullOrWhiteSpace(assistant.SystemPrompt)) return null;

        var systemPrompt = assistant.SystemPrompt;
        var handlebarsContext = objectContext;

        if (assistant.Inputs?.Count > 0)
        {
            handlebarsContext = new Dictionary<string, object>();
            if (objectContext?.Count > 0)
            {
                // add overrides
                foreach (var kvp in objectContext)
                {
                    handlebarsContext.Add(kvp.Key, kvp.Value);
                }
            }

            // add defaults
            foreach (var kvp in assistant.Inputs)
            {
                if (ExpressionEvaluatorService.TryResolve(context, objectContext, kvp.Value, out var resolved))
                {
                    handlebarsContext.TryAdd(kvp.Key, resolved);
                }
                else
                {
                    _logger.LogError("Failed to resolve {InputParameter}: {Value}", kvp.Key, kvp.Value);
                }
            }
        }

        systemPrompt = Generate(context,
            new DocumentTemplate
            {
                Template = assistant.SystemPrompt,
                StoredProcedures = assistant.StoredProcedures,
            }, handlebarsContext
        );

        return systemPrompt;
    }

    public string Generate(IEntityContext entityContext, IDocumentTemplate template, IDictionary<string, object> objectContext)
    {
        var sps = new Dictionary<string, AggregateStoredProcedure>();
        var cache = new Dictionary<string, List<ExpandoObject>>();

        var hb = Handlebars.Create();

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
        if (template.StoredProcedures != null)
        {
            foreach (var kvp in template.StoredProcedures)
            {
                hb.RegisterHelper(kvp.Key, (writer, options, context, args) =>
                {
                    if (!sps.TryGetValue(kvp.Key, out var sp))
                    {
                        sp = _connection.Filter<StoredProcedure, AggregateStoredProcedure>()
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
                        list = sp.Execute<ExpandoObject>(_connection, parms);
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

        return hb.Compile(template.Template).Invoke(objectContext);
    }
}