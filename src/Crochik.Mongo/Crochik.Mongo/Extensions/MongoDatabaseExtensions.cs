using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Crochik.Mongo;

public class CollectionNames
{
    public readonly static CollectionNames Resolver = new CollectionNames();

    private ConcurrentDictionary<Type, string> _collectionNames = new ConcurrentDictionary<Type, string>();

    public CollectionNames Register<T>(string name) => Register(typeof(T), name);
    public CollectionNames Register(Type type, string name)
    {
        _collectionNames.TryAdd(type, name);
        return this;
    }

    public string Resolve<T>() => Resolve(typeof(T));
    public string Resolve(Type type)
    {
        string name;
        if (!_collectionNames.TryGetValue(type, out name))
        {
            var attrib = type.GetCustomAttributes(true).OfType<BsonCollectionAttribute>().FirstOrDefault();

            name = attrib?.Name ?? type.Name;
            if (name.EndsWith("DAO", StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - 3);
            }

            Register(type, name);
        }

        return name;
    }
}

public static class MongoDatabaseExtensions
{
    public static IMongoCollection<T> GetCollection<T>(this IMongoDatabase database, Type type)
    {
        return database.GetCollection<T>(database.GetCollectionName(type));
    }

    public static IMongoCollection<T> GetCollection<T>(this IMongoDatabase database)
    {
        return database.GetCollection<T>(database.GetCollectionName<T>());
    }

    public static IMongoCollection<T> GetCollection<TBase, T>(this IMongoDatabase database)
        where T : TBase
    {
        return database.GetCollection<T>(database.GetCollectionName<TBase>());
    }

    public static string GetCollectionName<T>(this IMongoDatabase database) => CollectionNames.Resolver.Resolve<T>();
    public static string GetCollectionName(this IMongoDatabase database, Type type) => CollectionNames.Resolver.Resolve(type);

    public static Query<T> Filter<T>(this IMongoDatabase database) => new(database);

    public static BsonDocument ToBsonDocument<T>(this IMongoDatabase database, ProjectionDefinition<T> projection)
        => database.Settings.ToBsonDocument(projection);

    public static BsonDocument ToBsonDocument<T>(this IMongoDatabase database, FilterDefinition<T> filter)
        => database.Settings.ToBsonDocument(filter);

    public static BsonDocument ToBsonDocument<T>(this IMongoDatabase database, SortDefinition<T> sortDefinition)
        => database.Settings.ToBsonDocument(sortDefinition);

    public static BsonDocument ToBsonDocument<T>(this MongoDatabaseSettings settings, ProjectionDefinition<T> projection)
        => projection.Render(settings.SerializerRegistry.GetSerializer<T>(), settings.SerializerRegistry);

    public static BsonDocument ToBsonDocument<T>(this MongoDatabaseSettings settings, FilterDefinition<T> filter)
        => filter.Render(settings.SerializerRegistry.GetSerializer<T>(), settings.SerializerRegistry);

    public static BsonDocument ToBsonDocument<T>(this MongoDatabaseSettings settings, SortDefinition<T> sortDefinition)
        => sortDefinition.Render(settings.SerializerRegistry.GetSerializer<T>(), settings.SerializerRegistry);

    public static BsonDocument ToBsonDocument<T>(this IMongoDatabase database, UpdateDefinition<T> update)
        => database.Settings.ToBsonDocument(update);

    public static BsonDocument ToBsonDocument<T>(this MongoDatabaseSettings settings, UpdateDefinition<T> update)
        => update.Render(settings.SerializerRegistry.GetSerializer<T>(), settings.SerializerRegistry) as BsonDocument;

    public static IEnumerable<BsonDocument> AppendStage(this IEnumerable<BsonDocument> pipeline, string stage)
        => AppendStage(pipeline, BsonDocument.Parse(stage));

    public static IEnumerable<BsonDocument> AppendStage(this IEnumerable<BsonDocument> pipeline, BsonDocument stage)
        => pipeline.Append(stage);
}