using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Crochik.Dipper;

public class DipperException : Exception
{
    public DipperException(string message) : base(message)
    {
    }
}

public static class DipperExtensions
{
    public static async Task<StoredProcedure> DipperOrDefaultAsync(this MongoConnection connection, string id)
    {
        return await connection.Filter<StoredProcedure>()
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();
    }

    public static async Task<T> DipperOrDefaultAsync<T>(this MongoConnection connection, string id)
        where T : StoredProcedure
    {
        return await connection.Filter<StoredProcedure>()
                .OfType<StoredProcedure, T>()
                .Eq(x => x.Id, id)
                .FirstOrDefaultAsync()
            as T;
    }

    public static async Task<T> DipperAsync<T>(this MongoConnection connection, string id)
        where T : StoredProcedure
    {
        var sp = await connection.DipperOrDefaultAsync<T>(id);
        if (sp == null) throw new DipperException($"{id} Not found");
        return sp;
    }

    public static async Task<bool> DipperAggregateAsync(this MongoConnection connection, string name, string ns, object parameters = null)
    {
        // TODO: overload AggregateStoredProcedure.ExecuteAsync to handle aggregations that update data
        // ...
            
        var dipper = await connection.DipperAsync<AggregateStoredProcedure>($"{ns}.{name}");
        var result = await dipper.ExecuteAsync(connection, parameters.BuildDictionaryFromObjectProperties());
        return (bool)result;
    }

    public static async Task<object> DipperAsync(this MongoConnection connection, string name, string ns, object parameters = null)
    {
        string id = $"{ns}.{name}";
        connection.Logger.LogInformation("Load {storedProcedure}", id);

        var dipper = await connection.DipperOrDefaultAsync(id);
        return await dipper.ExecuteAsync(connection, parameters.BuildDictionaryFromObjectProperties());
    }

    public static async Task<List<T>> DipperAggregateAsync<T>(this MongoConnection connection, string name, string ns, object parameters = null)
    {
        var dipper = await connection.DipperAsync<AggregateStoredProcedure>($"{ns}.{name}");
        return await dipper.ExecuteAsync<T>(connection, parameters.BuildDictionaryFromObjectProperties());
    }

    public static async Task<UpdateResult> DipperUpdateAsync(this MongoConnection connection, string name, string ns, object parameters = null)
    {
        var dipper = await connection.DipperAsync<UpdateStoredProcedure>($"{ns}.{name}");
        return await dipper.ExecuteAsync(connection, parameters.BuildDictionaryFromObjectProperties()) as UpdateResult;
    }

    public static IDictionary<string, object> BuildDictionaryFromObjectProperties(this object parameters)
    {
        if (parameters == null) return null;
        if (parameters is IDictionary<string, object> dict) return dict;

        // BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance
        return parameters.GetType().GetProperties().ToDictionary
        (
            propInfo => propInfo.Name,
            propInfo => propInfo.GetValue(parameters, null)
        );
    }
}