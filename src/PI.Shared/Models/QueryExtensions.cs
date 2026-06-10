using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Crochik.Extensions;
using Crochik.Mongo;
using MongoDB.Bson;
using Newtonsoft.Json.Linq;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Models;

public static class QueryExtensions
{
    public static Query<T> Filter<T>(this MongoConnection connection, IEntityContext context, bool strict = true)
        where T : IEntityOwnedModel
    {
        return connection.Filter<T>().Init(context, strict);
    }

    public static Query<TOfType> Filter<TBase, TOfType>(this MongoConnection connection, IEntityContext context, bool strict = true)
        where TOfType : TBase
        where TBase : IEntityOwnedModel
    {
        return connection.Filter<TBase, TOfType>().Init(context, strict);
    }

    public static Query<T> Init<T>(this Query<T> query, IEntityContext context, bool strict = true)
        where T : IEntityOwnedModel
    {
        query.Eq(x => x.AccountId, context.AccountId.Value);

        switch (context.Role)
        {
            case EntityRoleId.Admin:
            case EntityRoleId.Account:
                if (strict) query.Eq(x => x.EntityId, context.AccountId.Value);
                break;
            case EntityRoleId.Organization:
            case EntityRoleId.Manager:
                query.Eq(x => x.EntityId, context.OrganizationId.Value);
                break;
            case EntityRoleId.User:
                query.Eq(x => x.EntityId, context.UserId.Value);
                break;
            default:
                throw new ForbiddenException(context);
        }

        return query;
    }

    public static Query<T> AddConstraints<T>(this Query<T> query, IEntityContext context, ObjectType objectType)
    {
        if (objectType.Constraints == null)
        {
            // old behavior
            query.Eq(nameof(IFlowObject.AccountId), context.AccountId.Value);

            if (objectType.CollectionName == nameof(CustomObject))
            {
                // implicit include objectType constraint
                query.Eq(nameof(IFlowObject.ObjectType), objectType.Name);
            }

            switch (context.Role)
            {
                case EntityRoleId.Organization:
                case EntityRoleId.Manager:
                case EntityRoleId.User:
                    query.Eq(nameof(CustomObject.EntityId), context.OrganizationId.Value);
                    break;

                case EntityRoleId.Account:
                case EntityRoleId.Admin:
                case EntityRoleId.Root:
                    break;

                default:
                    throw new ForbiddenException(context);
            }

            return query;
        }

        var conditions = objectType.GetConditions(context);

        return query.AddConditions(context, conditions);
    }

    public static Query<T> AddConditions<T>(this Query<T> query, IEntityContext context, IEnumerable<Condition> conditions, IDictionary<string, object> parentObject = null)
    {
        if (conditions == null) return query;

        foreach (var constraint in conditions)
        {
            var value = constraint.ResolveValue(context, parentObject);

            var fieldName = FormField.GetPathInCollection(constraint.FieldName);
            switch (constraint.Operator)
            {
                case Operator.Eq:
                    query.Eq(fieldName, ObjectIdExtensions.BestEffortAsSerializedId(value));
                    break;

                case Operator.Ne:
                    query.Ne(fieldName, ObjectIdExtensions.BestEffortAsSerializedId(value));
                    break;

                case Operator.Gt:
                    query.Gt(fieldName, value);
                    break;

                case Operator.Gte:
                    query.Gte(fieldName, value);
                    break;

                case Operator.Lt:
                    query.Lt(fieldName, value);
                    break;

                case Operator.Lte:
                    query.Lte(fieldName, value);
                    break;

                case Operator.In:
                {
                    var objArray = ToArrayOfObjects(value);
                    query.In(fieldName, objArray.Select(ObjectIdExtensions.BestEffortAsSerializedId));
                    break;
                }

                case Operator.Nin:
                {
                    var objArray = ToArrayOfObjects(value);
                    query.Nin(fieldName, objArray.Select(ObjectIdExtensions.BestEffortAsSerializedId));
                    break;
                }

                default:
                    throw new NotImplementedException($"{constraint.Operator}: operator not supported");
            }
        }

        return query;
    }

    private static object[] ToArrayOfObjects(object value)
    {
        var objArray = value switch
        {
            JArray a => a.Select(x => x.Value<object>()),
            IEnumerable<object> a => a,
            IEnumerable<Guid> a => a.Select(x => (object)x),
            string => throw new BadRequestException($"value is expected to be array but it is a string: {value}"),
            IEnumerable e => e.ToEnumerableObject(),
            _ => throw new BadRequestException($"can't convert value to array: {value}"),
        };

        return objArray.ToArray();
    }
}