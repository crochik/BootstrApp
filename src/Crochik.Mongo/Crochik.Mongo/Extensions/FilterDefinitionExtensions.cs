using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;

namespace Crochik.Mongo
{
    public static class FilterDefinitionExtensions
    {
        public static FilterDefinition<TDocument> Combine<TDocument>(this FilterDefinition<TDocument> filter, FilterDefinition<TDocument> other)
        {
            return filter != null ? filter & other : other;
        }

        /// <summary>
        /// Creates an all filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>An all filter.</returns>
        public static FilterDefinition<TDocument> All<TDocument, TItem>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, IEnumerable<TItem> values)
        {
            return filter.Combine(Builders<TDocument>.Filter.All(field, values));
        }

        /// <summary>
        /// Creates an all filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>An all filter.</returns>
        public static FilterDefinition<TDocument> All<TDocument, TItem>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, IEnumerable<TItem>>> field, IEnumerable<TItem> values)
        {
            return filter.Combine(Builders<TDocument>.Filter.All(field, values));
        }

        /// <summary>
        /// Creates an and filter.
        /// </summary>
        /// <param name="filters">The filters.</param>
        /// <returns>A filter.</returns>
        public static FilterDefinition<TDocument> And<TDocument>(this FilterDefinition<TDocument> filter, params FilterDefinition<TDocument>[] filters)
        {
            return filter.Combine(Builders<TDocument>.Filter.And(filters));
        }

        /// <summary>
        /// Creates an and filter.
        /// </summary>
        /// <param name="filters">The filters.</param>
        /// <returns>An and filter.</returns>
        public static FilterDefinition<TDocument> And<TDocument>(this FilterDefinition<TDocument> filter, IEnumerable<FilterDefinition<TDocument>> filters)
        {
            return filter.Combine(Builders<TDocument>.Filter.And(filters));
        }

        /// <summary>
        /// Creates an equality filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>An equality filter.</returns>
        public static FilterDefinition<TDocument> AnyEq<TDocument, TItem>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, TItem value)
        {
            return filter.Combine(Builders<TDocument>.Filter.AnyEq(field, value));
        }

        /// <summary>
        /// Creates an equality filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>An equality filter.</returns>
        public static FilterDefinition<TDocument> AnyEq<TDocument, TItem>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, IEnumerable<TItem>>> field, TItem value)
        {
            return filter.Combine(Builders<TDocument>.Filter.AnyEq(field, value));
        }

        /// <summary>
        /// Creates a greater than filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than filter.</returns>
        public static FilterDefinition<TDocument> AnyGt<TDocument, TItem>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, TItem value)
        {
            return filter.Combine(Builders<TDocument>.Filter.AnyEq(field, value));
        }

        /// <summary>
        /// Creates a greater than filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than filter.</returns>
        public static FilterDefinition<TDocument> AnyGt<TDocument, TItem>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, IEnumerable<TItem>>> field, TItem value)
        {
            return filter.Combine(Builders<TDocument>.Filter.AnyGt(field, value));
        }

        /// <summary>
        /// Creates a greater than or equal filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than or equal filter.</returns>
        public static FilterDefinition<TDocument> AnyGte<TDocument, TItem>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, TItem value)
        {
            return filter.Combine(Builders<TDocument>.Filter.AnyGte(field, value));
        }

        /// <summary>
        /// Creates a greater than or equal filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than or equal filter.</returns>
        public static FilterDefinition<TDocument> AnyGte<TDocument, TItem>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, IEnumerable<TItem>>> field, TItem value)
        {
            return filter.Combine(Builders<TDocument>.Filter.AnyGte(field, value));
        }

        /// <summary>
        /// Creates a less than filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than filter.</returns>
        public static FilterDefinition<TDocument> AnyLt<TDocument, TItem>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, TItem value)
        {
            return filter.Combine(Builders<TDocument>.Filter.AnyLt(field, value));
        }

        /// <summary>
        /// Creates a less than filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than filter.</returns>
        public static FilterDefinition<TDocument> AnyLt<TDocument, TItem>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, IEnumerable<TItem>>> field, TItem value)
        {
            return filter.Combine(Builders<TDocument>.Filter.AnyLt(field, value));
        }

        /// <summary>
        /// Creates a less than or equal filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than or equal filter.</returns>
        public static FilterDefinition<TDocument> AnyLte<TDocument, TItem>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, TItem value)
        {
            return filter.Combine(Builders<TDocument>.Filter.AnyLte(field, value));
        }

        /// <summary>
        /// Creates a less than or equal filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than or equal filter.</returns>
        public static FilterDefinition<TDocument> AnyLte<TDocument, TItem>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, IEnumerable<TItem>>> field, TItem value)
        {
            return filter.Combine(Builders<TDocument>.Filter.AnyLte(field, value));
        }

        /// <summary>
        /// Creates an in filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>An in filter.</returns>
        public static FilterDefinition<TDocument> AnyIn<TDocument, TItem>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, IEnumerable<TItem> values)
        {
            return filter.Combine(Builders<TDocument>.Filter.AnyIn(field, values));
        }

        /// <summary>
        /// Creates an in filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>An in filter.</returns>
        public static FilterDefinition<TDocument> AnyIn<TDocument, TItem>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, IEnumerable<TItem>>> field, IEnumerable<TItem> values)
        {
            return filter.Combine(Builders<TDocument>.Filter.AnyIn(field, values));
        }

        /// <summary>
        /// Creates a not equal filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A not equal filter.</returns>
        public static FilterDefinition<TDocument> AnyNe<TDocument, TItem>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, TItem value)
        {
            return filter.Combine(Builders<TDocument>.Filter.AnyNe(field, value));
        }

        /// <summary>
        /// Creates a not equal filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A not equal filter.</returns>
        public static FilterDefinition<TDocument> AnyNe<TDocument, TItem>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, IEnumerable<TItem>>> field, TItem value)
        {
            return filter.Combine(Builders<TDocument>.Filter.AnyNe(field, value));
        }

        /// <summary>
        /// Creates a not in filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>A not in filter.</returns>
        public static FilterDefinition<TDocument> AnyNin<TDocument, TItem>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, IEnumerable<TItem> values)
        {
            return filter.Combine(Builders<TDocument>.Filter.AnyNin(field, values));
        }

        /// <summary>
        /// Creates a not in filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>A not in filter.</returns>
        public static FilterDefinition<TDocument> AnyNin<TDocument, TItem>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, IEnumerable<TItem>>> field, IEnumerable<TItem> values)
        {
            return filter.Combine(Builders<TDocument>.Filter.AnyNin(field, values));
        }

        /// <summary>
        /// Creates a bits all clear filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="bitmask">The bitmask.</param>
        /// <returns>A bits all clear filter.</returns>
        public static FilterDefinition<TDocument> BitsAllClear<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, long bitmask)
        {
            return filter.Combine(Builders<TDocument>.Filter.BitsAllClear(field, bitmask));
        }

        /// <summary>
        /// Creates a bits all clear filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="bitmask">The bitmask.</param>
        /// <returns>A bits all clear filter.</returns>
        public static FilterDefinition<TDocument> BitsAllClear<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, long bitmask)
        {
            return filter.Combine(Builders<TDocument>.Filter.BitsAllClear(field, bitmask));
        }

        /// <summary>
        /// Creates a bits all set filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="bitmask">The bitmask.</param>
        /// <returns>A bits all set filter.</returns>
        public static FilterDefinition<TDocument> BitsAllSet<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, long bitmask)
        {
            return filter.Combine(Builders<TDocument>.Filter.BitsAllSet(field, bitmask));
        }

        /// <summary>
        /// Creates a bits all set filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="bitmask">The bitmask.</param>
        /// <returns>A bits all set filter.</returns>
        public static FilterDefinition<TDocument> BitsAllSet<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, long bitmask)
        {
            return filter.Combine(Builders<TDocument>.Filter.BitsAllSet(field, bitmask));
        }

        /// <summary>
        /// Creates a bits any clear filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="bitmask">The bitmask.</param>
        /// <returns>A bits any clear filter.</returns>
        public static FilterDefinition<TDocument> BitsAnyClear<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, long bitmask)
        {
            return filter.Combine(Builders<TDocument>.Filter.BitsAnyClear(field, bitmask));
        }

        /// <summary>
        /// Creates a bits any clear filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="bitmask">The bitmask.</param>
        /// <returns>A bits any clear filter.</returns>
        public static FilterDefinition<TDocument> BitsAnyClear<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, long bitmask)
        {
            return filter.Combine(Builders<TDocument>.Filter.BitsAnyClear(field, bitmask));
        }

        /// <summary>
        /// Creates a bits any set filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="bitmask">The bitmask.</param>
        /// <returns>A bits any set filter.</returns>
        public static FilterDefinition<TDocument> BitsAnySet<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, long bitmask)
        {
            return filter.Combine(Builders<TDocument>.Filter.BitsAnySet(field, bitmask));
        }

        /// <summary>
        /// Creates a bits any set filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="bitmask">The bitmask.</param>
        /// <returns>A bits any set filter.</returns>
        public static FilterDefinition<TDocument> BitsAnySet<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, long bitmask)
        {
            return filter.Combine(Builders<TDocument>.Filter.BitsAnySet(field, bitmask));
        }

        /// <summary>
        /// Creates an element match filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="filter">The filter.</param>
        /// <returns>An element match filter.</returns>
        public static FilterDefinition<TDocument> ElemMatch<TDocument, TItem>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, FilterDefinition<TItem> filter2)
        {
            return filter.Combine(Builders<TDocument>.Filter.ElemMatch(field, filter2));
        }

        /// <summary>
        /// Creates an element match filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="filter">The filter.</param>
        /// <returns>An element match filter.</returns>
        public static FilterDefinition<TDocument> ElemMatch<TDocument, TItem>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, IEnumerable<TItem>>> field, FilterDefinition<TItem> filter2)
        {
            return filter.Combine(Builders<TDocument>.Filter.ElemMatch(field, filter2));
        }

        /// <summary>
        /// Creates an element match filter for an array field.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="filter">The filter.</param>
        /// <returns>An element match filter.</returns>
        public static FilterDefinition<TDocument> ElemMatch<TDocument, TItem>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, IEnumerable<TItem>>> field, Expression<Func<TItem, bool>> filter2)
        {
            return filter.Combine(Builders<TDocument>.Filter.ElemMatch(field, filter2));
        }

        /// <summary>
        /// Creates an equality filter.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>An equality filter.</returns>
        public static FilterDefinition<TDocument> Eq<TDocument, TField>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, TField> field, TField value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Eq(field, value));
        }

        /// <summary>
        /// Creates an equality filter.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>An equality filter.</returns>
        public static FilterDefinition<TDocument> Eq<TDocument, TField>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, TField>> field, TField value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Eq(field, value));
        }

        /// <summary>
        /// Creates an exists filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="exists">if set to <c>true</c> [exists].</param>
        /// <returns>An exists filter.</returns>
        public static FilterDefinition<TDocument> Exists<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, bool exists = true)
        {
            return filter.Combine(Builders<TDocument>.Filter.Exists(field, exists));
        }

        /// <summary>
        /// Creates an exists filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="exists">if set to <c>true</c> [exists].</param>
        /// <returns>An exists filter.</returns>
        public static FilterDefinition<TDocument> Exists<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, bool exists = true)
        {
            return filter.Combine(Builders<TDocument>.Filter.Exists(field, exists));
        }

        /// <summary>
        /// Creates a geo intersects filter.
        /// </summary>
        /// <typeparam name="TCoordinates">The type of the coordinates.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="geometry">The geometry.</param>
        /// <returns>A geo intersects filter.</returns>
        public static FilterDefinition<TDocument> GeoIntersects<TDocument, TCoordinates>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, GeoJsonGeometry<TCoordinates> geometry)
            where TCoordinates : GeoJsonCoordinates
        {
            return filter.Combine(Builders<TDocument>.Filter.GeoIntersects(field, geometry));
        }

        /// <summary>
        /// Creates a geo intersects filter.
        /// </summary>
        /// <typeparam name="TCoordinates">The type of the coordinates.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="geometry">The geometry.</param>
        /// <returns>A geo intersects filter.</returns>
        public static FilterDefinition<TDocument> GeoIntersects<TDocument, TCoordinates>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, GeoJsonGeometry<TCoordinates> geometry)
            where TCoordinates : GeoJsonCoordinates
        {
            return filter.Combine(Builders<TDocument>.Filter.GeoIntersects(field, geometry));
        }

        /// <summary>
        /// Creates a geo within filter.
        /// </summary>
        /// <typeparam name="TCoordinates">The type of the coordinates.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="geometry">The geometry.</param>
        /// <returns>A geo within filter.</returns>
        public static FilterDefinition<TDocument> GeoWithin<TDocument, TCoordinates>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, GeoJsonGeometry<TCoordinates> geometry)
            where TCoordinates : GeoJsonCoordinates
        {
            return filter.Combine(Builders<TDocument>.Filter.GeoWithin(field, geometry));
        }

        /// <summary>
        /// Creates a geo within filter.
        /// </summary>
        /// <typeparam name="TCoordinates">The type of the coordinates.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="geometry">The geometry.</param>
        /// <returns>A geo within filter.</returns>
        public static FilterDefinition<TDocument> GeoWithin<TDocument, TCoordinates>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, GeoJsonGeometry<TCoordinates> geometry)
            where TCoordinates : GeoJsonCoordinates
        {
            return filter.Combine(Builders<TDocument>.Filter.GeoWithin(field, geometry));
        }

        /// <summary>
        /// Creates a geo within box filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="lowerLeftX">The lower left x.</param>
        /// <param name="lowerLeftY">The lower left y.</param>
        /// <param name="upperRightX">The upper right x.</param>
        /// <param name="upperRightY">The upper right y.</param>
        /// <returns>A geo within box filter.</returns>
        public static FilterDefinition<TDocument> GeoWithinBox<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, double lowerLeftX, double lowerLeftY, double upperRightX, double upperRightY)
        {
            return filter.Combine(Builders<TDocument>.Filter.GeoWithinBox(field, lowerLeftX, lowerLeftY, upperRightX, upperRightY));
        }

        /// <summary>
        /// Creates a geo within box filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="lowerLeftX">The lower left x.</param>
        /// <param name="lowerLeftY">The lower left y.</param>
        /// <param name="upperRightX">The upper right x.</param>
        /// <param name="upperRightY">The upper right y.</param>
        /// <returns>A geo within box filter.</returns>
        public static FilterDefinition<TDocument> GeoWithinBox<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, double lowerLeftX, double lowerLeftY, double upperRightX, double upperRightY)
        {
            return filter.Combine(Builders<TDocument>.Filter.GeoWithinBox(field, lowerLeftX, lowerLeftY, upperRightX, upperRightY));
        }

        /// <summary>
        /// Creates a geo within center filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="radius">The radius.</param>
        /// <returns>A geo within center filter.</returns>
        public static FilterDefinition<TDocument> GeoWithinCenter<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, double x, double y, double radius)
        {
            return filter.Combine(Builders<TDocument>.Filter.GeoWithinCenter(field, x, y, radius));
        }

        /// <summary>
        /// Creates a geo within center filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="radius">The radius.</param>
        /// <returns>A geo within center filter.</returns>
        public static FilterDefinition<TDocument> GeoWithinCenter<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, double x, double y, double radius)
        {
            return filter.Combine(Builders<TDocument>.Filter.GeoWithinCenter(field, x, y, radius));
        }

        /// <summary>
        /// Creates a geo within center sphere filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="radius">The radius.</param>
        /// <returns>A geo within center sphere filter.</returns>
        public static FilterDefinition<TDocument> GeoWithinCenterSphere<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, double x, double y, double radius)
        {
            return filter.Combine(Builders<TDocument>.Filter.GeoWithinCenterSphere(field, x, y, radius));
        }

        /// <summary>
        /// Creates a geo within center sphere filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="radius">The radius.</param>
        /// <returns>A geo within center sphere filter.</returns>
        public static FilterDefinition<TDocument> GeoWithinCenterSphere<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, double x, double y, double radius)
        {
            return filter.Combine(Builders<TDocument>.Filter.GeoWithinCenterSphere(field, x, y, radius));
        }

        /// <summary>
        /// Creates a geo within polygon filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="points">The points.</param>
        /// <returns>A geo within polygon filter.</returns>
        public static FilterDefinition<TDocument> GeoWithinPolygon<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, double[,] points)
        {
            return filter.Combine(Builders<TDocument>.Filter.GeoWithinPolygon(field, points));
        }

        /// <summary>
        /// Creates a geo within polygon filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="points">The points.</param>
        /// <returns>A geo within polygon filter.</returns>
        public static FilterDefinition<TDocument> GeoWithinPolygon<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, double[,] points)
        {
            return filter.Combine(Builders<TDocument>.Filter.GeoWithinPolygon(field, points));
        }

        /// <summary>
        /// Creates a greater than filter for a UInt32 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than filter.</returns>
        public static FilterDefinition<TDocument> Gt<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, uint> field, uint value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Gt(field, value));
        }

        /// <summary>
        /// Creates a greater than filter for a UInt64 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than filter.</returns>
        public static FilterDefinition<TDocument> Gt<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, ulong> field, ulong value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Gt(field, value));
        }

        /// <summary>
        /// Creates a greater than filter.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than filter.</returns>
        public static FilterDefinition<TDocument> Gt<TDocument, TField>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, TField> field, TField value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Gt(field, value));
        }

        /// <summary>
        /// Creates a greater than filter for a UInt32 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than filter.</returns>
        public static FilterDefinition<TDocument> Gt<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, uint>> field, uint value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Gt(field, value));
        }

        /// <summary>
        /// Creates a greater than filter for a UInt64 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than filter.</returns>
        public static FilterDefinition<TDocument> Gt<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, ulong>> field, ulong value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Gt(field, value));
        }

        /// <summary>
        /// Creates a greater than filter.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than filter.</returns>
        public static FilterDefinition<TDocument> Gt<TDocument,TField>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, TField>> field, TField value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Gt(field, value));
        }

        /// <summary>
        /// Creates a greater than or equal filter for a UInt32 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than or equal filter.</returns>
        public static FilterDefinition<TDocument> Gte<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, uint> field, uint value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Gte(field, value));
        }

        /// <summary>
        /// Creates a greater than or equal filter for a UInt64 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than or equal filter.</returns>
        public static FilterDefinition<TDocument> Gte<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, ulong> field, ulong value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Gte(field, value));
        }

        /// <summary>
        /// Creates a greater than or equal filter.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than or equal filter.</returns>
        public static FilterDefinition<TDocument> Gte<TDocument, TField>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, TField> field, TField value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Gte(field, value));
        }

        /// <summary>
        /// Creates a greater than or equal filter for a UInt32 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than or equal filter.</returns>
        public static FilterDefinition<TDocument> Gte<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, uint>> field, uint value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Gte(field, value));
        }

        /// <summary>
        /// Creates a greater than or equal filter for a UInt64 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than or equal filter.</returns>
        public static FilterDefinition<TDocument> Gte<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, ulong>> field, ulong value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Gte(field, value));
        }

        /// <summary>
        /// Creates a greater than or equal filter.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A greater than or equal filter.</returns>
        public static FilterDefinition<TDocument> Gte<TDocument, TField>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, TField>> field, TField value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Gte(field, value));
        }

        /// <summary>
        /// Creates an in filter.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>An in filter.</returns>
        public static FilterDefinition<TDocument> In<TDocument, TField>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, TField> field, IEnumerable<TField> values)
        {
            return filter.Combine(Builders<TDocument>.Filter.In(field, values));
        }

        /// <summary>
        /// Creates an in filter.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>An in filter.</returns>
        public static FilterDefinition<TDocument> In<TDocument, TField>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, TField>> field, IEnumerable<TField> values)
        {
            return filter.Combine(Builders<TDocument>.Filter.In(field, values));
        }

        /// <summary>
        /// Creates a json schema filter.
        /// </summary>
        /// <param name="schema">The json validation schema.</param>
        /// <returns>A schema filter.</returns>
        public static FilterDefinition<TDocument> JsonSchema<TDocument>(this FilterDefinition<TDocument> filter, BsonDocument schema)
        {
            return filter.Combine(Builders<TDocument>.Filter.JsonSchema(schema));
        }

        /// <summary>
        /// Creates a less than filter for a UInt32 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than filter.</returns>
        public static FilterDefinition<TDocument> Lt<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, uint> field, uint value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Lt(field, value));
        }

        /// <summary>
        /// Creates a less than filter for a UInt64 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than filter.</returns>
        public static FilterDefinition<TDocument> Lt<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, ulong> field, ulong value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Lt(field, value));
        }

        /// <summary>
        /// Creates a less than filter.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than filter.</returns>
        public static FilterDefinition<TDocument> Lt<TDocument, TField>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, TField> field, TField value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Lt(field, value));
        }

        /// <summary>
        /// Creates a less than filter for a UInt32 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than filter.</returns>
        public static FilterDefinition<TDocument> Lt<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, uint>> field, uint value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Lt(field, value));
        }

        /// <summary>
        /// Creates a less than filter for a UInt64 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than filter.</returns>
        public static FilterDefinition<TDocument> Lt<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, ulong>> field, ulong value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Lt(field, value));
        }

        /// <summary>
        /// Creates a less than filter.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than filter.</returns>
        public static FilterDefinition<TDocument> Lt<TDocument, TField>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, TField>> field, TField value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Lt(field, value));
        }

        /// <summary>
        /// Creates a less than or equal filter for a UInt32 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than or equal filter.</returns>
        public static FilterDefinition<TDocument> Lte<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, uint> field, uint value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Lte(field, value));
        }

        /// <summary>
        /// Creates a less than or equal filter for a UInt64 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than or equal filter.</returns>
        public static FilterDefinition<TDocument> Lte<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, ulong> field, ulong value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Lte(field, value));
        }

        /// <summary>
        /// Creates a less than or equal filter.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than or equal filter.</returns>
        public static FilterDefinition<TDocument> Lte<TDocument, TField>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, TField> field, TField value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Lte(field, value));
        }

        /// <summary>
        /// Creates a less than or equal filter for a UInt32 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than or equal filter.</returns>
        public static FilterDefinition<TDocument> Lte<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, uint>> field, uint value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Lte(field, value));
        }

        /// <summary>
        /// Creates a less than or equal filter for a UInt64 field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than or equal filter.</returns>
        public static FilterDefinition<TDocument> Lte<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, ulong>> field, ulong value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Lte(field, value));
        }

        /// <summary>
        /// Creates a less than or equal filter.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A less than or equal filter.</returns>
        public static FilterDefinition<TDocument> Lte<TDocument, TField>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, TField>> field, TField value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Lte(field, value));
        }

        /// <summary>
        /// Creates a modulo filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="modulus">The modulus.</param>
        /// <param name="remainder">The remainder.</param>
        /// <returns>A modulo filter.</returns>
        public static FilterDefinition<TDocument> Mod<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, long modulus, long remainder)
        {
            return filter.Combine(Builders<TDocument>.Filter.Mod(field, modulus, remainder));
        }

        /// <summary>
        /// Creates a modulo filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="modulus">The modulus.</param>
        /// <param name="remainder">The remainder.</param>
        /// <returns>A modulo filter.</returns>
        public static FilterDefinition<TDocument> Mod<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, long modulus, long remainder)
        {
            return filter.Combine(Builders<TDocument>.Filter.Mod(field, modulus, remainder));
        }

        /// <summary>
        /// Creates a not equal filter.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A not equal filter.</returns>
        public static FilterDefinition<TDocument> Ne<TDocument, TField>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, TField> field, TField value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Ne(field, value));
        }

        /// <summary>
        /// Creates a not equal filter.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>A not equal filter.</returns>
        public static FilterDefinition<TDocument> Ne<TDocument, TField>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, TField>> field, TField value)
        {
            return filter.Combine(Builders<TDocument>.Filter.Ne(field, value));
        }

        /// <summary>
        /// Creates a near filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <param name="minDistance">The minimum distance.</param>
        /// <returns>A near filter.</returns>
        public static FilterDefinition<TDocument> Near<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, double x, double y, double? maxDistance = null, double? minDistance = null)
        {
            return filter.Combine(Builders<TDocument>.Filter.Near(field, x,y,maxDistance,minDistance));
        }

        /// <summary>
        /// Creates a near filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <param name="minDistance">The minimum distance.</param>
        /// <returns>A near filter.</returns>
        public static FilterDefinition<TDocument> Near<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, double x, double y, double? maxDistance = null, double? minDistance = null)
        {
            return filter.Combine(Builders<TDocument>.Filter.Near(field, x,y,maxDistance,minDistance));
        }

        /// <summary>
        /// Creates a near filter.
        /// </summary>
        /// <typeparam name="TCoordinates">The type of the coordinates.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="point">The geometry.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <param name="minDistance">The minimum distance.</param>
        /// <returns>A near filter.</returns>
        public static FilterDefinition<TDocument> Near<TDocument, TCoordinates>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, GeoJsonPoint<TCoordinates> point, double? maxDistance = null, double? minDistance = null)
            where TCoordinates : GeoJsonCoordinates
        {
            return filter.Combine(Builders<TDocument>.Filter.Near(field, point,maxDistance,minDistance));
        }

        /// <summary>
        /// Creates a near filter.
        /// </summary>
        /// <typeparam name="TCoordinates">The type of the coordinates.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="point">The geometry.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <param name="minDistance">The minimum distance.</param>
        /// <returns>A near filter.</returns>
        public static FilterDefinition<TDocument> Near<TDocument, TCoordinates>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, GeoJsonPoint<TCoordinates> point, double? maxDistance = null, double? minDistance = null)
            where TCoordinates : GeoJsonCoordinates
        {
            return filter.Combine(Builders<TDocument>.Filter.Near(field, point,maxDistance,minDistance));
        }

        /// <summary>
        /// Creates a near sphere filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <param name="minDistance">The minimum distance.</param>
        /// <returns>A near sphere filter.</returns>
        public static FilterDefinition<TDocument> NearSphere<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, double x, double y, double? maxDistance = null, double? minDistance = null)
        {
            return filter.Combine(Builders<TDocument>.Filter.NearSphere(field, x, y, maxDistance, minDistance));
        }

        /// <summary>
        /// Creates a near sphere filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <param name="minDistance">The minimum distance.</param>
        /// <returns>A near sphere filter.</returns>
        public static FilterDefinition<TDocument> NearSphere<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, double x, double y, double? maxDistance = null, double? minDistance = null)
        {
            return filter.Combine(Builders<TDocument>.Filter.NearSphere(field, x, y, maxDistance, minDistance));
        }

        /// <summary>
        /// Creates a near sphere filter.
        /// </summary>
        /// <typeparam name="TCoordinates">The type of the coordinates.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="point">The geometry.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <param name="minDistance">The minimum distance.</param>
        /// <returns>A near sphere filter.</returns>
        public static FilterDefinition<TDocument> NearSphere<TDocument, TCoordinates>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, GeoJsonPoint<TCoordinates> point, double? maxDistance = null, double? minDistance = null)
            where TCoordinates : GeoJsonCoordinates
        {
            return filter.Combine(Builders<TDocument>.Filter.NearSphere(field, point, maxDistance, minDistance));
        }

        /// <summary>
        /// Creates a near sphere filter.
        /// </summary>
        /// <typeparam name="TCoordinates">The type of the coordinates.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="point">The geometry.</param>
        /// <param name="maxDistance">The maximum distance.</param>
        /// <param name="minDistance">The minimum distance.</param>
        /// <returns>A near sphere filter.</returns>
        public static FilterDefinition<TDocument> NearSphere<TDocument, TCoordinates>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, GeoJsonPoint<TCoordinates> point, double? maxDistance = null, double? minDistance = null)
            where TCoordinates : GeoJsonCoordinates
        {
            return filter.Combine(Builders<TDocument>.Filter.NearSphere(field, point, maxDistance, minDistance));
        }

        /// <summary>
        /// Creates a not in filter.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>A not in filter.</returns>
        public static FilterDefinition<TDocument> Nin<TDocument, TField>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, TField> field, IEnumerable<TField> values)
        {
            return filter.Combine(Builders<TDocument>.Filter.Nin(field, values));
        }

        /// <summary>
        /// Creates a not in filter.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>A not in filter.</returns>
        public static FilterDefinition<TDocument> Nin<TDocument, TField>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, TField>> field, IEnumerable<TField> values)
        {
            return filter.Combine(Builders<TDocument>.Filter.Nin(field, values));
        }

        /// <summary>
        /// Creates a not filter.
        /// </summary>
        /// <param name="filter">The filter.</param>
        /// <returns>A not filter.</returns>
        public static FilterDefinition<TDocument> Not<TDocument>(this FilterDefinition<TDocument> filter, FilterDefinition<TDocument> filter2)
        {
            return filter.Combine(Builders<TDocument>.Filter.Not(filter2));
        }

        /// <summary>
        /// Creates an OfType filter that matches documents of a derived type.
        /// </summary>
        /// <typeparam name="TDerived">The type of the matching derived documents.</typeparam>
        /// <returns>An OfType filter.</returns>
        public static FilterDefinition<TDocument> OfType<TDocument, TDerived>(this FilterDefinition<TDocument> filter) where TDerived : TDocument
        {
            return filter.Combine(Builders<TDocument>.Filter.OfType<TDerived>());
        }

        /// <summary>
        /// Creates an OfType filter that matches documents of a derived type and that also match a filter on the derived document.
        /// </summary>
        /// <typeparam name="TDerived">The type of the matching derived documents.</typeparam>
        /// <param name="derivedDocumentFilter">A filter on the derived document.</param>
        /// <returns>An OfType filter.</returns>
        public static FilterDefinition<TDocument> OfType<TDocument, TDerived>(this FilterDefinition<TDocument> filter, FilterDefinition<TDerived> derivedDocumentFilter) where TDerived : TDocument
        {
            return filter.Combine(Builders<TDocument>.Filter.OfType<TDerived>(derivedDocumentFilter));
        }

        /// <summary>
        /// Creates an OfType filter that matches documents of a derived type and that also match a filter on the derived document.
        /// </summary>
        /// <typeparam name="TDerived">The type of the matching derived documents.</typeparam>
        /// <param name="derivedDocumentFilter">A filter on the derived document.</param>
        /// <returns>An OfType filter.</returns>
        public static FilterDefinition<TDocument> OfType<TDocument, TDerived>(this FilterDefinition<TDocument> filter, Expression<Func<TDerived, bool>> derivedDocumentFilter) where TDerived : TDocument
        {
            return filter.Combine(Builders<TDocument>.Filter.OfType<TDerived>(derivedDocumentFilter));
        }

        /// <summary>
        /// Creates an OfType filter that matches documents with a field of a derived typer.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <typeparam name="TDerived">The type of the matching derived field value.</typeparam>
        /// <param name="field">The field.</param>
        /// <returns>An OfType filter.</returns>
        public static FilterDefinition<TDocument> OfType<TDocument, TField, TDerived>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, TField> field) where TDerived : TField
        {
            return filter.Combine(Builders<TDocument>.Filter.OfType<TField, TDerived>(field));
        }

        /// <summary>
        /// Creates an OfType filter that matches documents with a field of a derived type and that also match a filter on the derived field.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <typeparam name="TDerived">The type of the matching derived field value.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="derivedFieldFilter">A filter on the derived field.</param>
        /// <returns>An OfType filter.</returns>
        public static FilterDefinition<TDocument> OfType<TDocument, TField, TDerived>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument, TField> field, FilterDefinition<TDerived> derivedFieldFilter) where TDerived : TField
        {
            return filter.Combine(Builders<TDocument>.Filter.OfType<TField, TDerived>(field, derivedFieldFilter));
        }

        /// <summary>
        /// Creates an OfType filter that matches documents with a field of a derived type.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <typeparam name="TDerived">The type of the matching derived field value.</typeparam>
        /// <param name="field">The field.</param>
        /// <returns>An OfType filter.</returns>
        public static FilterDefinition<TDocument> OfType<TDocument, TField, TDerived>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, TField>> field) where TDerived : TField
        {
            return filter.Combine(Builders<TDocument>.Filter.OfType<TField, TDerived>(field));
        }

        /// <summary>
        /// Creates an OfType filter that matches documents with a field of a derived type and that also match a filter on the derived field.
        /// </summary>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <typeparam name="TDerived">The type of the matching derived field value.</typeparam>
        /// <param name="field">The field.</param>
        /// <param name="derivedFieldFilter">A filter on the derived field.</param>
        /// <returns>An OfType filter.</returns>
        public static FilterDefinition<TDocument> OfType<TDocument, TField, TDerived>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, TField>> field, Expression<Func<TDerived, bool>> derivedFieldFilter) where TDerived : TField
        {
            return filter.Combine(Builders<TDocument>.Filter.OfType<TField, TDerived>(field, derivedFieldFilter));
        }

        /// <summary>
        /// Creates an or filter.
        /// </summary>
        /// <param name="filters">The filters.</param>
        /// <returns>An or filter.</returns>
        public static FilterDefinition<TDocument> Or<TDocument>(this FilterDefinition<TDocument> filter, params FilterDefinition<TDocument>[] filters)
        {
            return filter.Combine(Builders<TDocument>.Filter.Or(filters));
        }

        /// <summary>
        /// Creates an or filter.
        /// </summary>
        /// <param name="filters">The filters.</param>
        /// <returns>An or filter.</returns>
        public static FilterDefinition<TDocument> Or<TDocument>(this FilterDefinition<TDocument> filter, IEnumerable<FilterDefinition<TDocument>> filters)
        {
            return filter.Combine(Builders<TDocument>.Filter.Or(filters));
        }

        /// <summary>
        /// Creates a regular expression filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="regex">The regex.</param>
        /// <returns>A regular expression filter.</returns>
        public static FilterDefinition<TDocument> Regex<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, BsonRegularExpression regex)
        {
            return filter.Combine(Builders<TDocument>.Filter.Regex(field, regex));
        }

        /// <summary>
        /// Creates a regular expression filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="regex">The regex.</param>
        /// <returns>A regular expression filter.</returns>
        public static FilterDefinition<TDocument> Regex<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, BsonRegularExpression regex)
        {
            return filter.Combine(Builders<TDocument>.Filter.Regex(field, regex));
        }

        /// <summary>
        /// Creates a size filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size filter.</returns>
        public static FilterDefinition<TDocument> Size<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, int size)
        {
            return filter.Combine(Builders<TDocument>.Filter.Size(field, size));
        }

        /// <summary>
        /// Creates a size filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size filter.</returns>
        public static FilterDefinition<TDocument> Size<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, int size)
        {
            return filter.Combine(Builders<TDocument>.Filter.Size(field, size));
        }

        /// <summary>
        /// Creates a size greater than filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size greater than filter.</returns>
        public static FilterDefinition<TDocument> SizeGt<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, int size)
        {
            return filter.Combine(Builders<TDocument>.Filter.SizeGt(field, size));
        }

        /// <summary>
        /// Creates a size greater than filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size greater than filter.</returns>
        public static FilterDefinition<TDocument> SizeGt<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, int size)
        {
            return filter.Combine(Builders<TDocument>.Filter.SizeGt(field, size));
        }

        /// <summary>
        /// Creates a size greater than or equal filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size greater than or equal filter.</returns>
        public static FilterDefinition<TDocument> SizeGte<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, int size)
        {
            return filter.Combine(Builders<TDocument>.Filter.SizeGte(field, size));
        }

        /// <summary>
        /// Creates a size greater than or equal filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size greater than or equal filter.</returns>
        public static FilterDefinition<TDocument> SizeGte<TDocument> (this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, int size)
        {
            return filter.Combine(Builders<TDocument>.Filter.SizeGte(field, size));
        }

        /// <summary>
        /// Creates a size less than filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size less than filter.</returns>
        public static FilterDefinition<TDocument> SizeLt<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, int size)
        {
            return filter.Combine(Builders<TDocument>.Filter.SizeLt(field, size));
        }

        /// <summary>
        /// Creates a size less than filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size less than filter.</returns>
        public static FilterDefinition<TDocument> SizeLt<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, int size)
        {
            return filter.Combine(Builders<TDocument>.Filter.SizeLt(field, size));
        }

        /// <summary>
        /// Creates a size less than or equal filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size less than or equal filter.</returns>
        public static FilterDefinition<TDocument> SizeLte<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, int size)
        {
            return filter.Combine(Builders<TDocument>.Filter.SizeLte(field, size));
        }

        /// <summary>
        /// Creates a size less than or equal filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="size">The size.</param>
        /// <returns>A size less than or equal filter.</returns>
        public static FilterDefinition<TDocument> SizeLte<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, int size)
        {
            return filter.Combine(Builders<TDocument>.Filter.SizeLte(field, size));
        }

        /// <summary>
        /// Creates a text filter.
        /// </summary>
        /// <param name="search">The search.</param>
        /// <param name="options">The text search options.</param>
        /// <returns>A text filter.</returns>
        public static FilterDefinition<TDocument> Text<TDocument>(this FilterDefinition<TDocument> filter, string search, TextSearchOptions options = null)
        {
            return filter.Combine(Builders<TDocument>.Filter.Text(search, options));
        }

        /// <summary>
        /// Creates a text filter.
        /// </summary>
        /// <param name="search">The search.</param>
        /// <param name="language">The language.</param>
        /// <returns>A text filter.</returns>
        public static FilterDefinition<TDocument> Text<TDocument>(this FilterDefinition<TDocument> filter, string search, string language)
        {
            return filter.Combine(Builders<TDocument>.Filter.Text(search, language));
        }

        /// <summary>
        /// Creates a type filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="type">The type.</param>
        /// <returns>A type filter.</returns>
        public static FilterDefinition<TDocument> Type<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, BsonType type)
        {
            return filter.Combine(Builders<TDocument>.Filter.Type(field, type));
        }

        /// <summary>
        /// Creates a type filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="type">The type.</param>
        /// <returns>A type filter.</returns>
        public static FilterDefinition<TDocument> Type<TDocument>(this FilterDefinition<TDocument> filter, FieldDefinition<TDocument> field, string type)
        {
            return filter.Combine(Builders<TDocument>.Filter.Type(field, type));
        }

        /// <summary>
        /// Creates a type filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="type">The type.</param>
        /// <returns>A type filter.</returns>
        public static FilterDefinition<TDocument> Type<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, BsonType type)
        {
            return filter.Combine(Builders<TDocument>.Filter.Type(field, type));
        }
        /// <summary>
        /// Creates a type filter.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="type">The type.</param>
        /// <returns>A type filter.</returns>
        public static FilterDefinition<TDocument> Type<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, object>> field, string type)
        {
            return filter.Combine(Builders<TDocument>.Filter.Type(field, type));
        }

        /// <summary>
        /// Creates a filter based on the expression.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>An expression filter.</returns>
        public static FilterDefinition<TDocument> Where<TDocument>(this FilterDefinition<TDocument> filter, Expression<Func<TDocument, bool>> expression)
        {
            return filter.Combine(Builders<TDocument>.Filter.Where(expression));
        }
    }
}