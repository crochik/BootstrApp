using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Crochik.Mongo;

public static class ExperimentalQueryExtensions
{
    /// <summary>
    /// Creates an element match query for an array field.
    /// with one less builder
    /// </summary>
    public static Query<TDocument> ElemMatchBuilder<TDocument, TItem>(this Query<TDocument> query, Expression<Func<TDocument, IEnumerable<TItem>>> field, Action<Query<TItem>> subQuery)
    {
        var action = new Query<TItem>();
        subQuery(action);
        return query.Combine(Builders<TDocument>.Filter.ElemMatch(field, action.Filter));
    }
        
    public static Query<TDocument> NotBuilder<TDocument>(this Query<TDocument> query, Action<Query<TDocument>> subQuery)
    {
        var action = new Query<TDocument>();
        subQuery(action);
        var filter = action.Filter;

        return query.Combine(Builders<TDocument>.Filter.Not(filter));
    }

    public static Query<TDocument> AndBuilder<TDocument>(this Query<TDocument> query, IEnumerable<Action<Query<TDocument>>> subQueries)
    {
        var filters = subQueries.Select(x =>
        {
            var action = new Query<TDocument>();
            x(action);
            return action.Filter;
        });

        return query.Combine(Builders<TDocument>.Filter.And(filters));
    }

    public static Query<TDocument> AndBuilder<TDocument>(this Query<TDocument> query, params Action<Query<TDocument>>[] subQueries)
    {
        var filters = subQueries.Select(x =>
        {
            var action = new Query<TDocument>();
            x(action);
            return action.Filter;
        });

        return query.Combine(Builders<TDocument>.Filter.And(filters));
    }

    public static Query<TDocument> OrBuilder<TDocument>(this Query<TDocument> query, params Action<Query<TDocument>>[] subQuery)
    {
        var filters = subQuery.Select(x =>
        {
            var action = new Query<TDocument>();
            x(action);
            return action.Filter;
        });

        return query.Combine(Builders<TDocument>.Filter.Or(filters));
    }

    public static Query<TDocument> OfTypeBuilder<TDocument, TField, TDerived>(this Query<TDocument> query, Expression<Func<TDocument, TField>> field, Action<Query<TDerived>> subQuery) where TDerived : TField
        => query.OfTypeBuilder<TDocument, TField, TDerived>(new ExpressionFieldDefinition<TDocument, TField>(field), subQuery);

    public static Query<TDocument> OfTypeBuilder<TDocument, TField, TDerived>(this Query<TDocument> query, FieldDefinition<TDocument, TField> field, Action<Query<TDerived>> subQuery) where TDerived : TField
    {
        var action = new Query<TDerived>();
        subQuery(action);
        return query.Combine(Builders<TDocument>.Filter.OfType<TField, TDerived>(field, action.Filter));
    }

    /// <summary>
    /// Matches document of type with sub filter 
    /// </summary>
    public static Query<TDocument> OfTypeBuilder<TDocument, TDerived>(this Query<TDocument> query, Action<Query<TDerived>> subQuery) where TDerived : TDocument
    {
        var action = new Query<TDerived>();
        subQuery(action);
        return query.Combine(Builders<TDocument>.Filter.OfType<TDerived>(action.Filter));
    }

    /// <summary>
    /// Creates an (case-insentive) equality query.
    /// </summary>
    /// <typeparam name="TField">The type of the field.</typeparam>
    /// <param name="field">The field.</param>
    /// <param name="value">The value.</param>
    /// <returns>An equality query.</returns>
    public static Query<TDocument> EqIgnoreCase<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, string value)
    {
        return query.Combine(Builders<TDocument>.Filter.Regex(field, new BsonRegularExpression($"^{Regex.Escape(value)}$", "i")));
    }

    /// <summary>
    /// Creates an (case-insentive) equality query.
    /// </summary>
    /// <typeparam name="TField">The type of the field.</typeparam>
    /// <param name="field">The field.</param>
    /// <param name="value">The value.</param>
    /// <returns>An equality query.</returns>
    public static Query<TDocument> EqIgnoreCase<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, string value)
    {
        return query.Combine(Builders<TDocument>.Filter.Regex(field, new BsonRegularExpression($"^{Regex.Escape(value)}$", "i")));
    }

    public static UpdateQuery<TDocument> PullFilterBuilder<TDocument, TItem>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, IEnumerable<TItem>>> field, Action<Query<TItem>> subQuery)
    {
        var subFilter = new Query<TItem>();
        subQuery(subFilter);
        return update.Combine(Builders<TDocument>.Update.PullFilter(field, subFilter.Filter));
    }
        
    /// <summary>
    /// Combines an existing update with a set or unset operator (if the value is null)
    /// </summary>
    /// <typeparam name="TDocument">The type of the document.</typeparam>
    /// <typeparam name="TField">The type of the field.</typeparam>
    /// <param name="update">The update.</param>
    /// <param name="field">The field.</param>
    /// <param name="value">The value.</param>
    /// <returns>
    /// A combined update.
    /// </returns>
    public static UpdateQuery<TDocument> SetOrUnset<TDocument, TField>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, TField>> field, TField value)
    {
        var builder = Builders<TDocument>.Update;
        if (value == null)
        {
            var fieldDef = new ExpressionFieldDefinition<TDocument>(field);
            return update.Combine(builder.Unset(fieldDef));
        }

        return update.Combine(builder.Set(field, value));
    }

    /// <summary>
    /// Combines an existing update with a set or unset operator (if the value is null)
    /// </summary>
    /// <typeparam name="TDocument">The type of the document.</typeparam>
    /// <typeparam name="TField">The type of the field.</typeparam>
    /// <param name="update">The update.</param>
    /// <param name="field">The field.</param>
    /// <param name="value">The value.</param>
    /// <returns>
    /// A combined update.
    /// </returns>
    public static UpdateQuery<TDocument> SetOrUnset<TDocument, TField>(this UpdateQuery<TDocument> update, string field, TField value)
    {
        var builder = Builders<TDocument>.Update;
        if (value == null)
        {
            return update.Combine(builder.Unset(field));
        }

        return update.Combine(builder.Set(field, value));
    }

    /// <summary>
    /// Add array filter
    /// TODO: allow to use query builders instead  
    /// </summary>
    public static UpdateQuery<TDocument> ArrayFilter<TDocument>(this UpdateQuery<TDocument> update, ArrayFilterDefinition filter)
    {
        update.ArrayFilters = (update.ArrayFilters ?? Enumerable.Empty<ArrayFilterDefinition>())
            .Append(filter)
            .ToArray();

        return update;
    }
    
    /// <summary>
    /// Add array filter
    /// TODO: allow to use query builders instead  
    /// </summary>
    public static UpdateQuery<TDocument> ArrayFilter<TDocument>(this UpdateQuery<TDocument> update, BsonDocument filter)
    {
        update.ArrayFilters = (update.ArrayFilters ?? Enumerable.Empty<ArrayFilterDefinition>())
            .Append(new BsonDocumentArrayFilterDefinition<BsonDocument>(filter))
            .ToArray();

        return update;
    }
    
}