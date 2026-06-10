using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using MongoDB.Driver;

namespace Crochik.Mongo
{
    public static class UpdateQueryExtensions
    {
        /// <summary>
        /// Combines an existing update with an add to set operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> AddToSet<TDocument, TItem>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument> field, TItem value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.AddToSet<TItem>(field, value));
        }

        /// <summary>
        /// Combines an existing update with an add to set operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> AddToSet<TDocument, TItem>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, IEnumerable<TItem>>> field, TItem value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.AddToSet<TItem>(field, value));
        }

        /// <summary>
        /// Combines an existing update with an add to set operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> AddToSetEach<TDocument, TItem>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument> field, IEnumerable<TItem> values)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.AddToSetEach<TItem>(field, values));
        }

        /// <summary>
        /// Combines an existing update with an add to set operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> AddToSetEach<TDocument, TItem>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, IEnumerable<TItem>>> field, IEnumerable<TItem> values)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.AddToSetEach<TItem>(field, values));
        }

        /// <summary>
        /// Combines an existing update with a bitwise and operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> BitwiseAnd<TDocument, TField>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument, TField> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.BitwiseAnd(field, value));
        }

        /// <summary>
        /// Combines an existing update with a bitwise and operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> BitwiseAnd<TDocument, TField>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, TField>> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.BitwiseAnd(field, value));
        }

        /// <summary>
        /// Combines an existing update with a bitwise or operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> BitwiseOr<TDocument, TField>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument, TField> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.BitwiseOr(field, value));
        }

        /// <summary>
        /// Combines an existing update with a bitwise or operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> BitwiseOr<TDocument, TField>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, TField>> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.BitwiseOr(field, value));
        }

        /// <summary>
        /// Combines an existing update with a bitwise xor operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> BitwiseXor<TDocument, TField>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument, TField> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.BitwiseXor(field, value));
        }

        /// <summary>
        /// Combines an existing update with a bitwise xor operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> BitwiseXor<TDocument, TField>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, TField>> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.BitwiseXor(field, value));
        }

        /// <summary>
        /// Combines an existing update with a current date operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="type">The type.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> CurrentDate<TDocument>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument> field, UpdateDefinitionCurrentDateType? type = null)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.CurrentDate(field, type));
        }

        /// <summary>
        /// Combines an existing update with a current date operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="type">The type.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> CurrentDate<TDocument>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, object>> field, UpdateDefinitionCurrentDateType? type = null)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.CurrentDate(field, type));
        }

        /// <summary>
        /// Combines an existing update with an increment operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Inc<TDocument, TField>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument, TField> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Inc(field, value));
        }

        /// <summary>
        /// Combines an existing update with an increment operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Inc<TDocument, TField>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, TField>> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Inc(field, value));
        }

        /// <summary>
        /// Combines an existing update with a max operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Max<TDocument, TField>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument, TField> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Max(field, value));
        }

        /// <summary>
        /// Combines an existing update with a max operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Max<TDocument, TField>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, TField>> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Max(field, value));
        }

        /// <summary>
        /// Combines an existing update with a min operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Min<TDocument, TField>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument, TField> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Min(field, value));
        }

        /// <summary>
        /// Combines an existing update with a min operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Min<TDocument, TField>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, TField>> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Min(field, value));
        }

        /// <summary>
        /// Combines an existing update with a multiply operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Mul<TDocument, TField>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument, TField> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Mul(field, value));
        }

        /// <summary>
        /// Combines an existing update with a multiply operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Mul<TDocument, TField>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, TField>> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Mul(field, value));
        }

        /// <summary>
        /// Combines an existing update with a pop operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> PopFirst<TDocument>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument> field)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.PopFirst(field));
        }

        /// <summary>
        /// Combines an existing update with a pop operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> PopFirst<TDocument>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, object>> field)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.PopFirst(field));
        }

        /// <summary>
        /// Combines an existing update with a pop operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> PopLast<TDocument>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument> field)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.PopLast(field));
        }

        /// <summary>
        /// Combines an existing update with a pop operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> PopLast<TDocument>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, object>> field)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.PopLast(field));
        }

        /// <summary>
        /// Combines an existing update with a pull operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Pull<TDocument, TItem>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument> field, TItem value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Pull(field, value));
        }

        /// <summary>
        /// Combines an existing update with a pull operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Pull<TDocument, TItem>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, IEnumerable<TItem>>> field, TItem value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Pull(field, value));
        }

        /// <summary>
        /// Combines an existing update with a pull operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> PullAll<TDocument, TItem>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument> field, IEnumerable<TItem> values)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.PullAll(field, values));
        }

        /// <summary>
        /// Combines an existing update with a pull operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> PullAll<TDocument, TItem>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, IEnumerable<TItem>>> field, IEnumerable<TItem> values)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.PullAll(field, values));
        }

        /// <summary>
        /// Combines an existing update with a pull operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="filter">The filter.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> PullFilter<TDocument, TItem>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument> field, FilterDefinition<TItem> filter)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.PullFilter(field, filter));
        }

        /// <summary>
        /// Combines an existing update with a pull operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="filter">The filter.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> PullFilter<TDocument, TItem>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, IEnumerable<TItem>>> field, FilterDefinition<TItem> filter)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.PullFilter(field, filter));
        }

        /// <summary>
        /// Combines an existing update with a pull operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="filter">The filter.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> PullFilter<TDocument, TItem>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, IEnumerable<TItem>>> field, Expression<Func<TItem, bool>> filter)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.PullFilter(field, filter));
        }

        /// <summary>
        /// Combines an existing update with a push operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Push<TDocument, TItem>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument> field, TItem value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Push(field, value));
        }

        /// <summary>
        /// Combines an existing update with a push operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Push<TDocument, TItem>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, IEnumerable<TItem>>> field, TItem value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Push(field, value));
        }

        /// <summary>
        /// Combines an existing update with a push operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <param name="slice">The slice.</param>
        /// <param name="position">The position.</param>
        /// <param name="sort">The sort.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> PushEach<TDocument, TItem>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument> field, IEnumerable<TItem> values, int? slice = null, int? position = null, SortDefinition<TItem> sort = null)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.PushEach(field, values, slice, position, sort));
        }

        /// <summary>
        /// Combines an existing update with a push operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="values">The values.</param>
        /// <param name="slice">The slice.</param>
        /// <param name="position">The position.</param>
        /// <param name="sort">The sort.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> PushEach<TDocument, TItem>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, IEnumerable<TItem>>> field, IEnumerable<TItem> values, int? slice = null, int? position = null, SortDefinition<TItem> sort = null)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.PushEach(field, values, slice, position, sort));
        }

        /// <summary>
        /// Combines an existing update with a field renaming operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="newName">The new name.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Rename<TDocument>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument> field, string newName)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Rename(field, newName));
        }

        /// <summary>
        /// Combines an existing update with a field renaming operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="newName">The new name.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Rename<TDocument>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, object>> field, string newName)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Rename(field, newName));
        }

        /// <summary>
        /// Combines an existing update with a set operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Set<TDocument, TField>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument, TField> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Set(field, value));
        }

        /// <summary>
        /// Combines an existing update with a set operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Set<TDocument, TField>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, TField>> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Set(field, value));
        }
        
        /// <summary>
        /// Combines an existing update with a set on insert operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> SetOnInsert<TDocument, TField>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument, TField> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.SetOnInsert(field, value));
        }

        /// <summary>
        /// Combines an existing update with a set on insert operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <typeparam name="TField">The type of the field.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> SetOnInsert<TDocument, TField>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, TField>> field, TField value)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.SetOnInsert(field, value));
        }

        /// <summary>
        /// Combines an existing update with an unset operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Unset<TDocument>(this UpdateQuery<TDocument> update, FieldDefinition<TDocument> field)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Unset(field));
        }

        /// <summary>
        /// Combines an existing update with an unset operator.
        /// </summary>
        /// <typeparam name="TDocument">The type of the document.</typeparam>
        /// <param name="update">The update.</param>
        /// <param name="field">The field.</param>
        /// <returns>
        /// A combined update.
        /// </returns>
        public static UpdateQuery<TDocument> Unset<TDocument>(this UpdateQuery<TDocument> update, Expression<Func<TDocument, object>> field)
        {
            var builder = Builders<TDocument>.Update;
            return update.Combine(builder.Unset(field));
        }
    }
}
