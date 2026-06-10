using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace Crochik.Dipper;

[BsonDiscriminator("Macro")]
public class MacroStoredProcedure : StoredProcedure
{
    public string[] Steps { get; set; }

    public override string Body => JsonConvert.SerializeObject(Steps, Formatting.Indented);

    public override string ToString(IDictionary<string, object> parameters) => Body;

    private async Task<StoredProcedure[]> GetAndValidateAsync(MongoConnection connection, IDictionary<string, object> parameters)
    {
        if (!HasAllParameters(parameters)) throw new DipperException("Missing Parameter");

        var stepIds = Steps.Distinct().Select(x => $"{Namespace}.{x}").ToArray();

        var steps = await connection.Filter<StoredProcedure>()
            .In(x => x.Id, stepIds)
            .FindAsync();

        if (steps.Count != stepIds.Length)
        {
            var missing = string.Join(",", stepIds.Except(steps.Select(x => x.Id)));
            throw new DipperException($"Missing Step(s): {missing}");
        }

        var dict = steps.ToDictionary(x => x.Id);

        var list = new List<StoredProcedure>();
        foreach (var stepId in stepIds)
        {
            var step = dict[stepId];
            if (!step.HasAllParameters(parameters)) throw new DipperException("Missing Parameter");

            list.Add(step);
        }

        return list.ToArray();
    }

    public override async Task<object> ExecuteAsync(MongoConnection connection, IDictionary<string, object> parameters)
    {
        connection.Logger.LogInformation("Execute: {storedProcedure}", Id);

        var steps = await GetAndValidateAsync(connection, parameters);
        var dict = new Dictionary<string, object>();
        foreach (var step in steps)
        {
            var result = await step.ExecuteAsync(connection, parameters);
            if (result != null) dict.Add(step.Id, result);
        }

        return dict;
    }

    public async Task<string> GenerateAsync(MongoConnection connection, IDictionary<string, object> parameters)
    {
        var steps = await GetAndValidateAsync(connection, parameters);
        var list = new List<string>();
        foreach (var step in steps)
        {
            var cmd = step.ToString(parameters);
            list.Add(cmd);
        }

        return string.Join("\n", list);
    }
}