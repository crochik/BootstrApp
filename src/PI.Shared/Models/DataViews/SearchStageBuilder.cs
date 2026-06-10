using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Models;

public class SearchStageBuilder
{
    public Index Index { get; init; }
    public Condition[] Conditions { get; init; }

    public Condition[] UnappliedConditions { get; set; }

    public BsonDocument Build()
    {
        var filterable = Index.FilterFieldNames?.ToHashSet() ?? [];
        var completable = Index.AutoCompleteFieldNames?.ToHashSet() ?? [];
        var searchable = Index.SearchFieldNames?.ToHashSet() ?? [];

        var unappliedConditions = new List<Condition>();
        var searchTerms = default(string);
        BsonArray filter = null;
        if (!(Conditions?.IsEmpty() ?? true))
        {
            filter = [];
            foreach (var condition in Conditions)
            {
                var value = condition.Value switch
                {
                    Guid uuid => uuid.AsSerializedId(),
                    _ => condition.Value,
                };


                if (condition.FieldName == Condition.FullTextSearch)
                {
                    searchTerms = value?.ToString();
                    continue;
                }

                if (condition.Operator != Operator.Eq && condition.Operator != Operator.In)
                {
                    // TODO: some other operators can be applied to filters
                    // for now leave for later (as a match)
                    // ...
                    unappliedConditions.Add(condition);
                    // throw new BadRequestException($"{condition.FieldName}: {condition.Operator} not supported in filters");
                    continue;
                }

                if (filterable.Contains(condition.FieldName) || searchable.Contains(condition.FieldName))
                {
                    var condFilter = condition.Operator switch
                    {
                        Operator.In => new BsonDocument(
                            "in", new BsonDocument
                            {
                                { "value", BsonValue.Create(value) },
                                { "path", FormField.GetPathInCollection(condition.FieldName) }
                            }
                        ),
                        Operator.Eq => new BsonDocument(
                            "equals", new BsonDocument
                            {
                                { "value", BsonValue.Create(value) },
                                { "path", FormField.GetPathInCollection(condition.FieldName) }
                            }
                        ),
                        _ => throw new BadRequestException($"{condition.Operator} not supported yet in filter"),
                    };

                    filter.Add(condFilter);
                }
                else
                {
                    // throw new BadRequestException($"{condition.FieldName} is not filterable");
                    unappliedConditions.Add(condition);
                }
            }
        }

        BsonArray should = null;
        if (!string.IsNullOrEmpty(searchTerms))
        {
            should = new BsonArray();
            foreach (var field in completable)
            {
                should.Add(new BsonDocument("autocomplete", new BsonDocument
                    {
                        { "query", searchTerms },
                        { "path", FormField.GetPathInCollection(field) },
                        { "tokenOrder", "sequential" },
                        // { "score", new BsonDocument("boost", 2) }
                    }
                ));
            }

            if (!searchable.IsEmpty())
            {
                var path = new BsonArray();
                path.AddRange(searchable.Select(FormField.GetPathInCollection));
                should.Add(new BsonDocument("text", new BsonDocument
                    {
                        { "query", searchTerms },
                        { "path", path },
                        { "matchCriteria", "all" }, // all words have to match
                    }
                ));
            }
        }

        var compound = new BsonDocument
        {
        };

        if (should != null)
        {
            compound.Add("should", should);
            compound.Add("minimumShouldMatch", 1);
        }

        if (filter != null) compound.Add("filter", filter);
        var all = searchable
            .Concat(completable)
            .Select(FormField.GetPathInCollection)
            .ToArray();

        var search = new BsonDocument
        {
            { "index", Index.Name },
            {
                "highlight", new BsonDocument
                {
                    { "path", BsonArray.Create(all) },
                    { "maxCharsToExamine", 500 },
                }
            },
            { "compound", compound },
        };

        UnappliedConditions = unappliedConditions.ToArray();

        return search;
    }
}