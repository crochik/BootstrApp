using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;

namespace Crochik.Mongo
{
    public static class QueryExtensions2
    {
        public static Query<TDocument> Between<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, DateTime>> field, DateTime a, DateTime b)
        {
            if (a > b)
            {
                return query.And(
                    Builders<TDocument>.Filter.Gte(field, b),
                    Builders<TDocument>.Filter.Lte(field, a)
                );
            }
            else 
            {
                return query.And(
                    Builders<TDocument>.Filter.Gte(field, a),
                    Builders<TDocument>.Filter.Lte(field, b)
                );
            }
        }
    }

    public static class QueryExtensions
    {
        // public static Query<TDocument> Combine<TDocument>(this Query<TDocument> query, FilterDefinition<TDocument> other)
        // {
        //     query.Filter = query.Filter == null ? other : other & query.Filter;
        //     return query;
        // }

        /// <summary>
        /// Creates an all query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>An all query.</returns>
        public static Query<TDocument> All<TDocument, TItem>(this Query<TDocument> query, FieldDefinition<TDocument> field, IEnumerable<TItem> values)
        {
            return query.Combine(Builders<TDocument>.Filter.All(field, values));
        }

        /// <summary>
        /// Creates an all query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>An all query.</returns>
        public static Query<TDocument> All<TDocument, TItem>(this Query<TDocument> query, Expression<Func<TDocument, IEnumerable<TItem>>> field, IEnumerable<TItem> values)
        {
            return query.Combine(Builders<TDocument>.Filter.All(field, values));
        }

        /// <summary>
        /// Creates an and query.
        /// </summary>
        /// <param name="filters">The filters.</param>
        /// <returns>A query.</returns>
        public static Query<TDocument> And<TDocument>(this Query<TDocument> query, params FilterDefinition<TDocument>[] filters)
        {
            return query.Combine(Builders<TDocument>.Filter.And(filters));
        }

        /// <summary>
        /// Creates an and query.
        /// </summary>
        /// <param name="filters">The filters.</param>
        /// <returns>An and query.</returns>
        public static Query<TDocument> And<TDocument>(this Query<TDocument> query, IEnumerable<FilterDefinition<TDocument>> filters)
        {
            return query.Combine(Builders<TDocument>.Filter.And(filters));
        }

        /// <summary>
        /// Creates an equality query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>An equality query.</returns>
        public static Query<TDocument> AnyEq<TDocument, TItem>(this Query<TDocument> query, FieldDefinition<TDocument> field, TItem value)
        {
            return query.Combine(Builders<TDocument>.Filter.AnyEq(field, value));
        }

        /// <summary>
        /// Creates an equality query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>An equality query.</returns>
        public static Query<TDocument> AnyEq<TDocument, TItem>(this Query<TDocument> query, Expression<Func<TDocument, IEnumerable<TItem>>> field, TItem value)
        {
            return query.Combine(Builders<TDocument>.Filter.AnyEq(field, value));
        }

        /// <summary>
        /// Creates a greater than query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than query.</returns>
        public static Query<TDocument> AnyGt<TDocument, TItem>(this Query<TDocument> query, FieldDefinition<TDocument> field, TItem value)
        {
            return query.Combine(Builders<TDocument>.Filter.AnyEq(field, value));
        }

        /// <summary>
        /// Creates a greater than query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than query.</returns>
        public static Query<TDocument> AnyGt<TDocument, TItem>(this Query<TDocument> query, Expression<Func<TDocument, IEnumerable<TItem>>> field, TItem value)
        {
            return query.Combine(Builders<TDocument>.Filter.AnyGt(field, value));
        }

        /// <summary>
        /// Creates a greater than or equal query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than or equal query.</returns>
        public static Query<TDocument> AnyGte<TDocument, TItem>(this Query<TDocument> query, FieldDefinition<TDocument> field, TItem value)
        {
            return query.Combine(Builders<TDocument>.Filter.AnyGte(field, value));
        }

        /// <summary>
        /// Creates a greater than or equal query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than or equal query.</returns>
        public static Query<TDocument> AnyGte<TDocument, TItem>(this Query<TDocument> query, Expression<Func<TDocument, IEnumerable<TItem>>> field, TItem value)
        {
            return query.Combine(Builders<TDocument>.Filter.AnyGte(field, value));
        }

        /// <summary>
        /// Creates a less than query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than query.</returns>
        public static Query<TDocument> AnyLt<TDocument, TItem>(this Query<TDocument> query, FieldDefinition<TDocument> field, TItem value)
        {
            return query.Combine(Builders<TDocument>.Filter.AnyLt(field, value));
        }

        /// <summary>
        /// Creates a less than query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than query.</returns>
        public static Query<TDocument> AnyLt<TDocument, TItem>(this Query<TDocument> query, Expression<Func<TDocument, IEnumerable<TItem>>> field, TItem value)
        {
            return query.Combine(Builders<TDocument>.Filter.AnyLt(field, value));
        }

        /// <summary>
        /// Creates a less than or equal query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than or equal query.</returns>
        public static Query<TDocument> AnyLte<TDocument, TItem>(this Query<TDocument> query, FieldDefinition<TDocument> field, TItem value)
        {
            return query.Combine(Builders<TDocument>.Filter.AnyLte(field, value));
        }

        /// <summary>
        /// Creates a less than or equal query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than or equal query.</returns>
        public static Query<TDocument> AnyLte<TDocument, TItem>(this Query<TDocument> query, Expression<Func<TDocument, IEnumerable<TItem>>> field, TItem value)
        {
            return query.Combine(Builders<TDocument>.Filter.AnyLte(field, value));
        }

        /// <summary>
        /// Creates an in query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>An in query.</returns>
        public static Query<TDocument> AnyIn<TDocument, TItem>(this Query<TDocument> query, FieldDefinition<TDocument> field, IEnumerable<TItem> values)
        {
            return query.Combine(Builders<TDocument>.Filter.AnyIn(field, values));
        }

        /// <summary>
        /// Creates an in query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>An in query.</returns>
        public static Query<TDocument> AnyIn<TDocument, TItem>(this Query<TDocument> query, Expression<Func<TDocument, IEnumerable<TItem>>> field, IEnumerable<TItem> values)
        {
            return query.Combine(Builders<TDocument>.Filter.AnyIn(field, values));
        }

        /// <summary>
        /// Creates a not equal query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A not equal query.</returns>
        public static Query<TDocument> AnyNe<TDocument, TItem>(this Query<TDocument> query, FieldDefinition<TDocument> field, TItem value)
        {
            return query.Combine(Builders<TDocument>.Filter.AnyNe(field, value));
        }

        /// <summary>
        /// Creates a not equal query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A not equal query.</returns>
        public static Query<TDocument> AnyNe<TDocument, TItem>(this Query<TDocument> query, Expression<Func<TDocument, IEnumerable<TItem>>> field, TItem value)
        {
            return query.Combine(Builders<TDocument>.Filter.AnyNe(field, value));
        }

        /// <summary>
        /// Creates a not in query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>A not in query.</returns>
        public static Query<TDocument> AnyNin<TDocument, TItem>(this Query<TDocument> query, FieldDefinition<TDocument> field, IEnumerable<TItem> values)
        {
            return query.Combine(Builders<TDocument>.Filter.AnyNin(field, values));
        }

        /// <summary>
        /// Creates a not in query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>A not in query.</returns>
        public static Query<TDocument> AnyNin<TDocument, TItem>(this Query<TDocument> query, Expression<Func<TDocument, IEnumerable<TItem>>> field, IEnumerable<TItem> values)
        {
            return query.Combine(Builders<TDocument>.Filter.AnyNin(field, values));
        }

        /// <summary>
        /// Creates a bits all clear query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="bitmask">The bitmask.</param>
        /// <returns>A bits all clear query.</returns>
        public static Query<TDocument> BitsAllClear<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, long bitmask)
        {
            return query.Combine(Builders<TDocument>.Filter.BitsAllClear(field, bitmask));
        }

        /// <summary>
        /// Creates a bits all clear query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="bitmask">The bitmask.</param>
        /// <returns>A bits all clear query.</returns>
        public static Query<TDocument> BitsAllClear<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, long bitmask)
        {
            return query.Combine(Builders<TDocument>.Filter.BitsAllClear(field, bitmask));
        }

        /// <summary>
        /// Creates a bits all set query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="bitmask">The bitmask.</param>
        /// <returns>A bits all set query.</returns>
        public static Query<TDocument> BitsAllSet<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, long bitmask)
        {
            return query.Combine(Builders<TDocument>.Filter.BitsAllSet(field, bitmask));
        }

        /// <summary>
        /// Creates a bits all set query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="bitmask">The bitmask.</param>
        /// <returns>A bits all set query.</returns>
        public static Query<TDocument> BitsAllSet<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, long bitmask)
        {
            return query.Combine(Builders<TDocument>.Filter.BitsAllSet(field, bitmask));
        }

        /// <summary>
        /// Creates a bits any clear query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="bitmask">The bitmask.</param>
        /// <returns>A bits any clear query.</returns>
        public static Query<TDocument> BitsAnyClear<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, long bitmask)
        {
            return query.Combine(Builders<TDocument>.Filter.BitsAnyClear(field, bitmask));
        }

        /// <summary>
        /// Creates a bits any clear query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="bitmask">The bitmask.</param>
        /// <returns>A bits any clear query.</returns>
        public static Query<TDocument> BitsAnyClear<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, long bitmask)
        {
            return query.Combine(Builders<TDocument>.Filter.BitsAnyClear(field, bitmask));
        }

        /// <summary>
        /// Creates a bits any set query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="bitmask">The bitmask.</param>
        /// <returns>A bits any set query.</returns>
        public static Query<TDocument> BitsAnySet<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, long bitmask)
        {
            return query.Combine(Builders<TDocument>.Filter.BitsAnySet(field, bitmask));
        }

        /// <summary>
        /// Creates a bits any set query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="bitmask">The bitmask.</param>
        /// <returns>A bits any set query.</returns>
        public static Query<TDocument> BitsAnySet<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, long bitmask)
        {
            return query.Combine(Builders<TDocument>.Filter.BitsAnySet(field, bitmask));
        }

        /// <summary>
        /// Creates an element match query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="query">The query.</param>
        /// <returns>An element match query.</returns>
        public static Query<TDocument> ElemMatch<TDocument, TItem>(this Query<TDocument> query, FieldDefinition<TDocument> field, FilterDefinition<TItem> filter2)
        {
            return query.Combine(Builders<TDocument>.Filter.ElemMatch(field, filter2));
        }

        /// <summary>
        /// Creates an element match query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="query">The query.</param>
        /// <returns>An element match query.</returns>
        public static Query<TDocument> ElemMatch<TDocument, TItem>(this Query<TDocument> query, Expression<Func<TDocument, IEnumerable<TItem>>> field, FilterDefinition<TItem> filter2)
        {
            return query.Combine(Builders<TDocument>.Filter.ElemMatch(field, filter2));
        }

        /// <summary>
        /// Creates an element match query for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="query">The query.</param>
        /// <returns>An element match query.</returns>
        public static Query<TDocument> ElemMatch<TDocument, TItem>(this Query<TDocument> query, Expression<Func<TDocument, IEnumerable<TItem>>> field, Expression<Func<TItem, bool>> filter2)
        {
            return query.Combine(Builders<TDocument>.Filter.ElemMatch(field, filter2));
        }

        /// <summary>
        /// Creates an equality query.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>An equality query.</returns>
        public static Query<TDocument> Eq<TDocument, TField>(this Query<TDocument> query, FieldDefinition<TDocument, TField> field, TField value)
        {
            return query.Combine(Builders<TDocument>.Filter.Eq(field, value));
        }

        /// <summary>
        /// Creates an equality query.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>An equality query.</returns>
        public static Query<TDocument> Eq<TDocument, TField>(this Query<TDocument> query, Expression<Func<TDocument, TField>> field, TField value)
        {
            return query.Combine(Builders<TDocument>.Filter.Eq(field, value));
        }

        /// <summary>
        /// Creates an exists query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="exists">if set to <c>true</c> [exists].</param>
        /// <returns>An exists query.</returns>
        public static Query<TDocument> Exists<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, bool exists = true)
        {
            return query.Combine(Builders<TDocument>.Filter.Exists(field, exists));
        }

        /// <summary>
        /// Creates an exists query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="exists">if set to <c>true</c> [exists].</param>
        /// <returns>An exists query.</returns>
        public static Query<TDocument> Exists<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, bool exists = true)
        {
            return query.Combine(Builders<TDocument>.Filter.Exists(field, exists));
        }

        /// <summary>
        /// Creates a geo intersects query.
        /// </summary>
        /// <typeparam name="TCoordinates">The type of the coordinates.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="geometry">The geometry.</param>
        /// <returns>A geo intersects query.</returns>
        public static Query<TDocument> GeoIntersects<TDocument, TCoordinates>(this Query<TDocument> query, FieldDefinition<TDocument> field, GeoJsonGeometry<TCoordinates> geometry)
            where TCoordinates : GeoJsonCoordinates
        {
            return query.Combine(Builders<TDocument>.Filter.GeoIntersects(field, geometry));
        }

        /// <summary>
        /// Creates a geo intersects query.
        /// </summary>
        /// <typeparam name="TCoordinates">The type of the coordinates.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="geometry">The geometry.</param>
        /// <returns>A geo intersects query.</returns>
        public static Query<TDocument> GeoIntersects<TDocument, TCoordinates>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, GeoJsonGeometry<TCoordinates> geometry)
            where TCoordinates : GeoJsonCoordinates
        {
            return query.Combine(Builders<TDocument>.Filter.GeoIntersects(field, geometry));
        }

        /// <summary>
        /// Creates a geo within query.
        /// </summary>
        /// <typeparam name="TCoordinates">The type of the coordinates.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="geometry">The geometry.</param>
        /// <returns>A geo within query.</returns>
        public static Query<TDocument> GeoWithin<TDocument, TCoordinates>(this Query<TDocument> query, FieldDefinition<TDocument> field, GeoJsonGeometry<TCoordinates> geometry)
            where TCoordinates : GeoJsonCoordinates
        {
            return query.Combine(Builders<TDocument>.Filter.GeoWithin(field, geometry));
        }

        /// <summary>
        /// Creates a geo within query.
        /// </summary>
        /// <typeparam name="TCoordinates">The type of the coordinates.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="geometry">The geometry.</param>
        /// <returns>A geo within query.</returns>
        public static Query<TDocument> GeoWithin<TDocument, TCoordinates>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, GeoJsonGeometry<TCoordinates> geometry)
            where TCoordinates : GeoJsonCoordinates
        {
            return query.Combine(Builders<TDocument>.Filter.GeoWithin(field, geometry));
        }

        /// <summary>
        /// Creates a geo within box query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="lowerLeftX">The lower left x.</param>
        /// <param name="lowerLeftY">The lower left y.</param>
        /// <param name="upperRightX">The upper right x.</param>
        /// <param name="upperRightY">The upper right y.</param>
        /// <returns>A geo within box query.</returns>
        public static Query<TDocument> GeoWithinBox<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, double lowerLeftX, double lowerLeftY, double upperRightX, double upperRightY)
        {
            return query.Combine(Builders<TDocument>.Filter.GeoWithinBox(field, lowerLeftX, lowerLeftY, upperRightX, upperRightY));
        }

        /// <summary>
        /// Creates a geo within box query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="lowerLeftX">The lower left x.</param>
        /// <param name="lowerLeftY">The lower left y.</param>
        /// <param name="upperRightX">The upper right x.</param>
        /// <param name="upperRightY">The upper right y.</param>
        /// <returns>A geo within box query.</returns>
        public static Query<TDocument> GeoWithinBox<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, double lowerLeftX, double lowerLeftY, double upperRightX, double upperRightY)
        {
            return query.Combine(Builders<TDocument>.Filter.GeoWithinBox(field, lowerLeftX, lowerLeftY, upperRightX, upperRightY));
        }

        /// <summary>
        /// Creates a geo within center query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="radius">The radius.</param>
        /// <returns>A geo within center query.</returns>
        public static Query<TDocument> GeoWithinCenter<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, double x, double y, double radius)
        {
            return query.Combine(Builders<TDocument>.Filter.GeoWithinCenter(field, x, y, radius));
        }

        /// <summary>
        /// Creates a geo within center query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="radius">The radius.</param>
        /// <returns>A geo within center query.</returns>
        public static Query<TDocument> GeoWithinCenter<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, double x, double y, double radius)
        {
            return query.Combine(Builders<TDocument>.Filter.GeoWithinCenter(field, x, y, radius));
        }

        /// <summary>
        /// Creates a geo within center sphere query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="radius">The radius.</param>
        /// <returns>A geo within center sphere query.</returns>
        public static Query<TDocument> GeoWithinCenterSphere<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, double x, double y, double radius)
        {
            return query.Combine(Builders<TDocument>.Filter.GeoWithinCenterSphere(field, x, y, radius));
        }

        /// <summary>
        /// Creates a geo within center sphere query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="radius">The radius.</param>
        /// <returns>A geo within center sphere query.</returns>
        public static Query<TDocument> GeoWithinCenterSphere<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, double x, double y, double radius)
        {
            return query.Combine(Builders<TDocument>.Filter.GeoWithinCenterSphere(field, x, y, radius));
        }

        /// <summary>
        /// Creates a geo within polygon query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="points">The points.</param>
        /// <returns>A geo within polygon query.</returns>
        public static Query<TDocument> GeoWithinPolygon<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, double[,] points)
        {
            return query.Combine(Builders<TDocument>.Filter.GeoWithinPolygon(field, points));
        }

        /// <summary>
        /// Creates a geo within polygon query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="points">The points.</param>
        /// <returns>A geo within polygon query.</returns>
        public static Query<TDocument> GeoWithinPolygon<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, double[,] points)
        {
            return query.Combine(Builders<TDocument>.Filter.GeoWithinPolygon(field, points));
        }

        /// <summary>
        /// Creates a greater than query for a UInt32 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than query.</returns>
        public static Query<TDocument> Gt<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument, uint> field, uint value)
        {
            return query.Combine(Builders<TDocument>.Filter.Gt(field, value));
        }

        /// <summary>
        /// Creates a greater than query for a UInt64 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than query.</returns>
        public static Query<TDocument> Gt<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument, ulong> field, ulong value)
        {
            return query.Combine(Builders<TDocument>.Filter.Gt(field, value));
        }

        /// <summary>
        /// Creates a greater than query.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than query.</returns>
        public static Query<TDocument> Gt<TDocument, TField>(this Query<TDocument> query, FieldDefinition<TDocument, TField> field, TField value)
        {
            return query.Combine(Builders<TDocument>.Filter.Gt(field, value));
        }

        /// <summary>
        /// Creates a greater than query for a UInt32 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than query.</returns>
        public static Query<TDocument> Gt<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, uint>> field, uint value)
        {
            return query.Combine(Builders<TDocument>.Filter.Gt(field, value));
        }

        /// <summary>
        /// Creates a greater than query for a UInt64 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than query.</returns>
        public static Query<TDocument> Gt<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, ulong>> field, ulong value)
        {
            return query.Combine(Builders<TDocument>.Filter.Gt(field, value));
        }

        /// <summary>
        /// Creates a greater than query.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than query.</returns>
        public static Query<TDocument> Gt<TDocument, TField>(this Query<TDocument> query, Expression<Func<TDocument, TField>> field, TField value)
        {
            return query.Combine(Builders<TDocument>.Filter.Gt(field, value));
        }

        /// <summary>
        /// Creates a greater than or equal query for a UInt32 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than or equal query.</returns>
        public static Query<TDocument> Gte<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument, uint> field, uint value)
        {
            return query.Combine(Builders<TDocument>.Filter.Gte(field, value));
        }

        /// <summary>
        /// Creates a greater than or equal query for a UInt64 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than or equal query.</returns>
        public static Query<TDocument> Gte<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument, ulong> field, ulong value)
        {
            return query.Combine(Builders<TDocument>.Filter.Gte(field, value));
        }

        /// <summary>
        /// Creates a greater than or equal query.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than or equal query.</returns>
        public static Query<TDocument> Gte<TDocument, TField>(this Query<TDocument> query, FieldDefinition<TDocument, TField> field, TField value)
        {
            return query.Combine(Builders<TDocument>.Filter.Gte(field, value));
        }

        /// <summary>
        /// Creates a greater than or equal query for a UInt32 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than or equal query.</returns>
        public static Query<TDocument> Gte<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, uint>> field, uint value)
        {
            return query.Combine(Builders<TDocument>.Filter.Gte(field, value));
        }

        /// <summary>
        /// Creates a greater than or equal query for a UInt64 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than or equal query.</returns>
        public static Query<TDocument> Gte<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, ulong>> field, ulong value)
        {
            return query.Combine(Builders<TDocument>.Filter.Gte(field, value));
        }

        /// <summary>
        /// Creates a greater than or equal query.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than or equal query.</returns>
        public static Query<TDocument> Gte<TDocument, TField>(this Query<TDocument> query, Expression<Func<TDocument, TField>> field, TField value)
        {
            return query.Combine(Builders<TDocument>.Filter.Gte(field, value));
        }

        /// <summary>
        /// Creates an in query.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>An in query.</returns>
        public static Query<TDocument> In<TDocument, TField>(this Query<TDocument> query, FieldDefinition<TDocument, TField> field, IEnumerable<TField> values)
        {
            return query.Combine(Builders<TDocument>.Filter.In(field, values));
        }

        /// <summary>
        /// Creates an in query.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>An in query.</returns>
        public static Query<TDocument> In<TDocument, TField>(this Query<TDocument> query, Expression<Func<TDocument, TField>> field, IEnumerable<TField> values)
        {
            return query.Combine(Builders<TDocument>.Filter.In(field, values));
        }

        /// <summary>
        /// Creates a json schema query.
        /// </summary>
        /// <param name="schema">The json validation schema.</param>
        /// <returns>A schema query.</returns>
        public static Query<TDocument> JsonSchema<TDocument>(this Query<TDocument> query, BsonDocument schema)
        {
            return query.Combine(Builders<TDocument>.Filter.JsonSchema(schema));
        }

        /// <summary>
        /// Creates a less than query for a UInt32 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than query.</returns>
        public static Query<TDocument> Lt<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument, uint> field, uint value)
        {
            return query.Combine(Builders<TDocument>.Filter.Lt(field, value));
        }

        /// <summary>
        /// Creates a less than query for a UInt64 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than query.</returns>
        public static Query<TDocument> Lt<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument, ulong> field, ulong value)
        {
            return query.Combine(Builders<TDocument>.Filter.Lt(field, value));
        }

        /// <summary>
        /// Creates a less than query.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than query.</returns>
        public static Query<TDocument> Lt<TDocument, TField>(this Query<TDocument> query, FieldDefinition<TDocument, TField> field, TField value)
        {
            return query.Combine(Builders<TDocument>.Filter.Lt(field, value));
        }

        /// <summary>
        /// Creates a less than query for a UInt32 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than query.</returns>
        public static Query<TDocument> Lt<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, uint>> field, uint value)
        {
            return query.Combine(Builders<TDocument>.Filter.Lt(field, value));
        }

        /// <summary>
        /// Creates a less than query for a UInt64 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than query.</returns>
        public static Query<TDocument> Lt<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, ulong>> field, ulong value)
        {
            return query.Combine(Builders<TDocument>.Filter.Lt(field, value));
        }

        /// <summary>
        /// Creates a less than query.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than query.</returns>
        public static Query<TDocument> Lt<TDocument, TField>(this Query<TDocument> query, Expression<Func<TDocument, TField>> field, TField value)
        {
            return query.Combine(Builders<TDocument>.Filter.Lt(field, value));
        }

        /// <summary>
        /// Creates a less than or equal query for a UInt32 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than or equal query.</returns>
        public static Query<TDocument> Lte<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument, uint> field, uint value)
        {
            return query.Combine(Builders<TDocument>.Filter.Lte(field, value));
        }

        /// <summary>
        /// Creates a less than or equal query for a UInt64 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than or equal query.</returns>
        public static Query<TDocument> Lte<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument, ulong> field, ulong value)
        {
            return query.Combine(Builders<TDocument>.Filter.Lte(field, value));
        }

        /// <summary>
        /// Creates a less than or equal query.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than or equal query.</returns>
        public static Query<TDocument> Lte<TDocument, TField>(this Query<TDocument> query, FieldDefinition<TDocument, TField> field, TField value)
        {
            return query.Combine(Builders<TDocument>.Filter.Lte(field, value));
        }

        /// <summary>
        /// Creates a less than or equal query for a UInt32 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than or equal query.</returns>
        public static Query<TDocument> Lte<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, uint>> field, uint value)
        {
            return query.Combine(Builders<TDocument>.Filter.Lte(field, value));
        }

        /// <summary>
        /// Creates a less than or equal query for a UInt64 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than or equal query.</returns>
        public static Query<TDocument> Lte<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, ulong>> field, ulong value)
        {
            return query.Combine(Builders<TDocument>.Filter.Lte(field, value));
        }

        /// <summary>
        /// Creates a less than or equal query.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than or equal query.</returns>
        public static Query<TDocument> Lte<TDocument, TField>(this Query<TDocument> query, Expression<Func<TDocument, TField>> field, TField value)
        {
            return query.Combine(Builders<TDocument>.Filter.Lte(field, value));
        }

        /// <summary>
        /// Creates a modulo query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="modulus">The modulus.</param>
        /// <param name="remainder">The remainder.</param>
        /// <returns>A modulo query.</returns>
        public static Query<TDocument> Mod<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, long modulus, long remainder)
        {
            return query.Combine(Builders<TDocument>.Filter.Mod(field, modulus, remainder));
        }

        /// <summary>
        /// Creates a modulo query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="modulus">The modulus.</param>
        /// <param name="remainder">The remainder.</param>
        /// <returns>A modulo query.</returns>
        public static Query<TDocument> Mod<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, long modulus, long remainder)
        {
            return query.Combine(Builders<TDocument>.Filter.Mod(field, modulus, remainder));
        }

        /// <summary>
        /// Creates a not equal query.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A not equal query.</returns>
        public static Query<TDocument> Ne<TDocument, TField>(this Query<TDocument> query, FieldDefinition<TDocument, TField> field, TField value)
        {
            return query.Combine(Builders<TDocument>.Filter.Ne(field, value));
        }

        /// <summary>
        /// Creates a not equal query.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A not equal query.</returns>
        public static Query<TDocument> Ne<TDocument, TField>(this Query<TDocument> query, Expression<Func<TDocument, TField>> field, TField value)
        {
            return query.Combine(Builders<TDocument>.Filter.Ne(field, value));
        }

        /// <summary>
        /// Creates a near query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <param name="minDistance">The minimum distance.</param>
        /// <returns>A near query.</returns>
        public static Query<TDocument> Near<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, double x, double y, double? maxDistance = null, double? minDistance = null)
        {
            return query.Combine(Builders<TDocument>.Filter.Near(field, x, y, maxDistance, minDistance));
        }

        /// <summary>
        /// Creates a near query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <param name="minDistance">The minimum distance.</param>
        /// <returns>A near query.</returns>
        public static Query<TDocument> Near<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, double x, double y, double? maxDistance = null, double? minDistance = null)
        {
            return query.Combine(Builders<TDocument>.Filter.Near(field, x, y, maxDistance, minDistance));
        }

        /// <summary>
        /// Creates a near query.
        /// </summary>
        /// <typeparam name="TCoordinates">The type of the coordinates.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="point">The geometry.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <param name="minDistance">The minimum distance.</param>
        /// <returns>A near query.</returns>
        public static Query<TDocument> Near<TDocument, TCoordinates>(this Query<TDocument> query, FieldDefinition<TDocument> field, GeoJsonPoint<TCoordinates> point, double? maxDistance = null, double? minDistance = null)
            where TCoordinates : GeoJsonCoordinates
        {
            return query.Combine(Builders<TDocument>.Filter.Near(field, point, maxDistance, minDistance));
        }

        /// <summary>
        /// Creates a near query.
        /// </summary>
        /// <typeparam name="TCoordinates">The type of the coordinates.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="point">The geometry.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <param name="minDistance">The minimum distance.</param>
        /// <returns>A near query.</returns>
        public static Query<TDocument> Near<TDocument, TCoordinates>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, GeoJsonPoint<TCoordinates> point, double? maxDistance = null, double? minDistance = null)
            where TCoordinates : GeoJsonCoordinates
        {
            return query.Combine(Builders<TDocument>.Filter.Near(field, point, maxDistance, minDistance));
        }

        /// <summary>
        /// Creates a near sphere query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <param name="minDistance">The minimum distance.</param>
        /// <returns>A near sphere query.</returns>
        public static Query<TDocument> NearSphere<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, double x, double y, double? maxDistance = null, double? minDistance = null)
        {
            return query.Combine(Builders<TDocument>.Filter.NearSphere(field, x, y, maxDistance, minDistance));
        }

        /// <summary>
        /// Creates a near sphere query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <param name="minDistance">The minimum distance.</param>
        /// <returns>A near sphere query.</returns>
        public static Query<TDocument> NearSphere<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, double x, double y, double? maxDistance = null, double? minDistance = null)
        {
            return query.Combine(Builders<TDocument>.Filter.NearSphere(field, x, y, maxDistance, minDistance));
        }

        /// <summary>
        /// Creates a near sphere query.
        /// </summary>
        /// <typeparam name="TCoordinates">The type of the coordinates.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="point">The geometry.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <param name="minDistance">The minimum distance.</param>
        /// <returns>A near sphere query.</returns>
        public static Query<TDocument> NearSphere<TDocument, TCoordinates>(this Query<TDocument> query, FieldDefinition<TDocument> field, GeoJsonPoint<TCoordinates> point, double? maxDistance = null, double? minDistance = null)
            where TCoordinates : GeoJsonCoordinates
        {
            return query.Combine(Builders<TDocument>.Filter.NearSphere(field, point, maxDistance, minDistance));
        }

        /// <summary>
        /// Creates a near sphere query.
        /// </summary>
        /// <typeparam name="TCoordinates">The type of the coordinates.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="point">The geometry.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <param name="minDistance">The minimum distance.</param>
        /// <returns>A near sphere query.</returns>
        public static Query<TDocument> NearSphere<TDocument, TCoordinates>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, GeoJsonPoint<TCoordinates> point, double? maxDistance = null, double? minDistance = null)
            where TCoordinates : GeoJsonCoordinates
        {
            return query.Combine(Builders<TDocument>.Filter.NearSphere(field, point, maxDistance, minDistance));
        }

        /// <summary>
        /// Creates a not in query.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>A not in query.</returns>
        public static Query<TDocument> Nin<TDocument, TField>(this Query<TDocument> query, FieldDefinition<TDocument, TField> field, IEnumerable<TField> values)
        {
            return query.Combine(Builders<TDocument>.Filter.Nin(field, values));
        }

        /// <summary>
        /// Creates a not in query.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>A not in query.</returns>
        public static Query<TDocument> Nin<TDocument, TField>(this Query<TDocument> query, Expression<Func<TDocument, TField>> field, IEnumerable<TField> values)
        {
            return query.Combine(Builders<TDocument>.Filter.Nin(field, values));
        }

        /// <summary>
        /// Creates a not query.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>A not query.</returns>
        public static Query<TDocument> Not<TDocument>(this Query<TDocument> query, FilterDefinition<TDocument> filter2)
        {
            return query.Combine(Builders<TDocument>.Filter.Not(filter2));
        }

        /// <summary>
        /// Creates an OfType query that matches documents of a derived type.
        /// </summary>
        /// <typeparam name="TDerived">The type of the matching derived documents.</typeparam>
        /// <returns>An OfType query.</returns>
        public static Query<TDocument> OfType<TDocument, TDerived>(this Query<TDocument> query) where TDerived : TDocument
        {
            return query.Combine(Builders<TDocument>.Filter.OfType<TDerived>());
        }

        /// <summary>
        /// Creates an OfType query that matches documents of a derived type and that also match a query on the derived document.
        /// </summary>
        /// <typeparam name="TDerived">The type of the matching derived documents.</typeparam>
        /// <param name="derivedDocumentFilter">A query on the derived document.</param>
        /// <returns>An OfType query.</returns>
        public static Query<TDocument> OfType<TDocument, TDerived>(this Query<TDocument> query, FilterDefinition<TDerived> derivedDocumentFilter) where TDerived : TDocument
        {
            return query.Combine(Builders<TDocument>.Filter.OfType<TDerived>(derivedDocumentFilter));
        }

        /// <summary>
        /// Creates an OfType query that matches documents of a derived type and that also match a query on the derived document.
        /// </summary>
        /// <typeparam name="TDerived">The type of the matching derived documents.</typeparam>
        /// <param name="derivedDocumentFilter">A query on the derived document.</param>
        /// <returns>An OfType query.</returns>
        public static Query<TDocument> OfType<TDocument, TDerived>(this Query<TDocument> query, Expression<Func<TDerived, bool>> derivedDocumentFilter) where TDerived : TDocument
        {
            return query.Combine(Builders<TDocument>.Filter.OfType<TDerived>(derivedDocumentFilter));
        }

        /// <summary>
        /// Creates an OfType query that matches documents with a field of a derived typer.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <typeparam name="TDerived">The type of the matching derived field value.</typeparam>
        /// <param name="field">The field.</param>
        /// <returns>An OfType query.</returns>
        public static Query<TDocument> OfType<TDocument, TField, TDerived>(this Query<TDocument> query, FieldDefinition<TDocument, TField> field) where TDerived : TField
        {
            return query.Combine(Builders<TDocument>.Filter.OfType<TField, TDerived>(field));
        }

        /// <summary>
        /// Creates an OfType query that matches documents with a field of a derived type and that also match a query on the derived field.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <typeparam name="TDerived">The type of the matching derived field value.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="derivedFieldFilter">A query on the derived field.</param>
        /// <returns>An OfType query.</returns>
        public static Query<TDocument> OfType<TDocument, TField, TDerived>(this Query<TDocument> query, FieldDefinition<TDocument, TField> field, FilterDefinition<TDerived> derivedFieldFilter) where TDerived : TField
        {
            return query.Combine(Builders<TDocument>.Filter.OfType<TField, TDerived>(field, derivedFieldFilter));
        }

        /// <summary>
        /// Creates an OfType query that matches documents with a field of a derived type.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <typeparam name="TDerived">The type of the matching derived field value.</typeparam>
        /// <param name="field">The field.</param>
        /// <returns>An OfType query.</returns>
        public static Query<TDocument> OfType<TDocument, TField, TDerived>(this Query<TDocument> query, Expression<Func<TDocument, TField>> field) where TDerived : TField
        {
            return query.Combine(Builders<TDocument>.Filter.OfType<TField, TDerived>(field));
        }

        /// <summary>
        /// Creates an OfType query that matches documents with a field of a derived type and that also match a query on the derived field.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <typeparam name="TDerived">The type of the matching derived field value.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="derivedFieldFilter">A query on the derived field.</param>
        /// <returns>An OfType query.</returns>
        public static Query<TDocument> OfType<TDocument, TField, TDerived>(this Query<TDocument> query, Expression<Func<TDocument, TField>> field, Expression<Func<TDerived, bool>> derivedFieldFilter) where TDerived : TField
        {
            return query.Combine(Builders<TDocument>.Filter.OfType<TField, TDerived>(field, derivedFieldFilter));
        }

        /// <summary>
        /// Creates an or query.
        /// </summary>
        /// <param name="filters">The filters.</param>
        /// <returns>An or query.</returns>
        public static Query<TDocument> Or<TDocument>(this Query<TDocument> query, params FilterDefinition<TDocument>[] filters)
        {
            return query.Combine(Builders<TDocument>.Filter.Or(filters));
        }

        /// <summary>
        /// Creates an or query.
        /// </summary>
        /// <param name="filters">The filters.</param>
        /// <returns>An or query.</returns>
        public static Query<TDocument> Or<TDocument>(this Query<TDocument> query, IEnumerable<FilterDefinition<TDocument>> filters)
        {
            return query.Combine(Builders<TDocument>.Filter.Or(filters));
        }

        /// <summary>
        /// Creates a regular expression query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="regex">The regex.</param>
        /// <returns>A regular expression query.</returns>
        public static Query<TDocument> Regex<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, BsonRegularExpression regex)
        {
            return query.Combine(Builders<TDocument>.Filter.Regex(field, regex));
        }

        /// <summary>
        /// Creates a regular expression query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="regex">The regex.</param>
        /// <returns>A regular expression query.</returns>
        public static Query<TDocument> Regex<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, BsonRegularExpression regex)
        {
            return query.Combine(Builders<TDocument>.Filter.Regex(field, regex));
        }

        /// <summary>
        /// Creates a size query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size query.</returns>
        public static Query<TDocument> Size<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, int size)
        {
            return query.Combine(Builders<TDocument>.Filter.Size(field, size));
        }

        /// <summary>
        /// Creates a size query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size query.</returns>
        public static Query<TDocument> Size<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, int size)
        {
            return query.Combine(Builders<TDocument>.Filter.Size(field, size));
        }

        /// <summary>
        /// Creates a size greater than query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size greater than query.</returns>
        public static Query<TDocument> SizeGt<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, int size)
        {
            return query.Combine(Builders<TDocument>.Filter.SizeGt(field, size));
        }

        /// <summary>
        /// Creates a size greater than query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size greater than query.</returns>
        public static Query<TDocument> SizeGt<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, int size)
        {
            return query.Combine(Builders<TDocument>.Filter.SizeGt(field, size));
        }

        /// <summary>
        /// Creates a size greater than or equal query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size greater than or equal query.</returns>
        public static Query<TDocument> SizeGte<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, int size)
        {
            return query.Combine(Builders<TDocument>.Filter.SizeGte(field, size));
        }

        /// <summary>
        /// Creates a size greater than or equal query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size greater than or equal query.</returns>
        public static Query<TDocument> SizeGte<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, int size)
        {
            return query.Combine(Builders<TDocument>.Filter.SizeGte(field, size));
        }

        /// <summary>
        /// Creates a size less than query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size less than query.</returns>
        public static Query<TDocument> SizeLt<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, int size)
        {
            return query.Combine(Builders<TDocument>.Filter.SizeLt(field, size));
        }

        /// <summary>
        /// Creates a size less than query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size less than query.</returns>
        public static Query<TDocument> SizeLt<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, int size)
        {
            return query.Combine(Builders<TDocument>.Filter.SizeLt(field, size));
        }

        /// <summary>
        /// Creates a size less than or equal query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size less than or equal query.</returns>
        public static Query<TDocument> SizeLte<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, int size)
        {
            return query.Combine(Builders<TDocument>.Filter.SizeLte(field, size));
        }

        /// <summary>
        /// Creates a size less than or equal query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size less than or equal query.</returns>
        public static Query<TDocument> SizeLte<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, int size)
        {
            return query.Combine(Builders<TDocument>.Filter.SizeLte(field, size));
        }

        /// <summary>
        /// Creates a text query.
        /// </summary>
        /// <param name="search">The search.</param>
        /// <param name="options">The text search options.</param>
        /// <returns>A text query.</returns>
        public static Query<TDocument> Text<TDocument>(this Query<TDocument> query, string search, TextSearchOptions options = null)
        {
            return query.Combine(Builders<TDocument>.Filter.Text(search, options));
        }

        /// <summary>
        /// Creates a text query.
        /// </summary>
        /// <param name="search">The search.</param>
        /// <param name="language">The language.</param>
        /// <returns>A text query.</returns>
        public static Query<TDocument> Text<TDocument>(this Query<TDocument> query, string search, string language)
        {
            return query.Combine(Builders<TDocument>.Filter.Text(search, language));
        }

        /// <summary>
        /// Creates a type query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="type">The type.</param>
        /// <returns>A type query.</returns>
        public static Query<TDocument> Type<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, BsonType type)
        {
            return query.Combine(Builders<TDocument>.Filter.Type(field, type));
        }

        /// <summary>
        /// Creates a type query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="type">The type.</param>
        /// <returns>A type query.</returns>
        public static Query<TDocument> Type<TDocument>(this Query<TDocument> query, FieldDefinition<TDocument> field, string type)
        {
            return query.Combine(Builders<TDocument>.Filter.Type(field, type));
        }

        /// <summary>
        /// Creates a type query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="type">The type.</param>
        /// <returns>A type query.</returns>
        public static Query<TDocument> Type<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, BsonType type)
        {
            return query.Combine(Builders<TDocument>.Filter.Type(field, type));
        }
        /// <summary>
        /// Creates a type query.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="type">The type.</param>
        /// <returns>A type query.</returns>
        public static Query<TDocument> Type<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, object>> field, string type)
        {
            return query.Combine(Builders<TDocument>.Filter.Type(field, type));
        }

        /// <summary>
        /// Creates a query based on the expression.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>An expression query.</returns>
        public static Query<TDocument> Where<TDocument>(this Query<TDocument> query, Expression<Func<TDocument, bool>> expression)
        {
            return query.Combine(Builders<TDocument>.Filter.Where(expression));
        }
    }
}