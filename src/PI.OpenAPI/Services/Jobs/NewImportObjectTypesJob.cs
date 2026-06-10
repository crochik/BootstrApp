using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared.Diff;
using PI.Shared.Models;
using PI.Shared.Services;
using PI.Shared.Services.OpenApiGenerator;

namespace PI.OpenAPI.Services.Jobs;

public class NewImportObjectTypesJob(AccountManagementService accountManagementService) : IRunJob
{
    private string BasePath => "/Users/felipe/DEVELOPMENT/github/OTGSchema/"; // PISchema
    private bool CreateMissing => true;
    private bool DryRun => false;
    private bool PreserveIds => true;
    public string Name => "ImportObjectTypes";

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        var options = new AccountManagementService.Options
        {
            PreserveIds = PreserveIds,
            TargetAccountId = context.AccountId.Value,
            BasePath = BasePath,
            Namespaces = ["m2", "docuseal", "fcb2b", "otg"],
            UpdateObjectStatus = (dst, diff, query) => UpdateObject(dst.ObjectType, dst, diff, query),
            UpdateFlow = (dst, diff, query) => UpdateObject(dst.ObjectType, dst, diff, query),
            UpdateEventType = (dst, diff, query) => UpdateObject(dst.ObjectType, dst, diff, query),
            UpdateObjectType = (dst, diff, query) => UpdateObject(dst.ObjectType, dst, diff, query),
        };

        var result = await accountManagementService.ImportAllAsync(options, context.Actor(), stoppingToken);

        return new JobResult
        {
            Message = result.IsError ? result.Status : $"Imported {result.Value.Count} documents",
        };
    }

    private bool UpdateObject<T>(string objectType, T dst, DiffResult diff, UpdateQuery<T> query) where T : EntityOwnedModel
    {
        var modifiedObjectType = typeof(T).Name;

        if (DryRun) return false;
        if (diff == null) return CreateMissing && GetConfirmation($"New {modifiedObjectType}: \"{dst.Name}\"", $"Add {modifiedObjectType}?");
        
        // update
        var differences = diff.ToChangeList();

        if (query != null)
        {
            var filter = query.GetFilterAsBsonDocument().ToString();
            var update = query.GetUpdateAsBsonDocument().ToString();
            return GetConfirmation($"{modifiedObjectType} \"{dst.Name}\" was modified for {objectType}", differences, $"FILTER: {filter}", $"UPDATE: {update}", $"Update {modifiedObjectType}?");
        }
        
        return GetConfirmation($"{modifiedObjectType} \"{dst.Name}\" was modified for {objectType}", differences, $"Update {modifiedObjectType}?");
    }

    private bool GetConfirmation(params IEnumerable<string> messages)
    {
        Console.WriteLine("---------------------------------------------------------------------------");
        foreach (var message in messages)
        {
            Console.WriteLine(message);
        }

        Console.WriteLine("(Y)es, (N)o");
        var response = Console.ReadKey(true).Key;
        return response switch
        {
            ConsoleKey.Y => true,
            _ => false,
        };
    }
}