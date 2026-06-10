using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Crochik.Dipper;
using Crochik.Mongo;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using Xunit;
using Xunit.Abstractions;
using ValueType = PI.Shared.Models.ValueType;

public class AppDataViewPipelineBuilderTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    IEntityContext Context => new AccountContext(AccountIds.FCI);

    FieldRBAC RBAC => new FieldRBAC
    {
        Permissions = new Dictionary<string, FieldPermission> { { nameof(EntityRoleId.Account), FieldPermission.Read }, }
    };

    // cache object type created during execution
    private ObjectType _objectType;
    ObjectType ObjectType => _objectType ??= new ObjectType
    {
        Constraints = new Dictionary<string, Criteria>
        {
            {
                nameof(EntityRoleId.Account), new Criteria
                {
                    Conditions = [
                        Condition.Eq(nameof(Appointment.AccountId), AccountIds.FCI)
                    ],
                }
            }  
        },
        Fields = new Dictionary<string, FieldTemplate>
        {
            {
                nameof(Appointment.CancelledOn), new FieldTemplate
                {
                    Field = new DateTimeField
                    {
                        Name = nameof(Appointment.CancelledOn),
                    },
                    RBAC = RBAC,
                }
            },
            {
                nameof(Appointment.Start), new FieldTemplate
                {
                    Field = new DateTimeField
                    {
                        Name = nameof(Appointment.Start),
                    },
                    RBAC = RBAC
                }
            },
            {
                nameof(Appointment.FlowId), new FieldTemplate
                {
                    Field = new ReferenceField
                    {
                        Name = nameof(Appointment.FlowId),
                        ReferenceFieldOptions = new ReferenceFieldOptions
                        {
                            ValueType = ValueType.String,
                        },
                    },
                    RBAC = RBAC
                }
            },
            {
                nameof(Appointment.ObjectStatusId), new FieldTemplate
                {
                    Field = new ReferenceField
                    {
                        Name = nameof(Appointment.ObjectStatusId),
                        ReferenceFieldOptions = new ReferenceFieldOptions
                        {
                            ValueType = ValueType.String,
                        },
                    },
                    RBAC = RBAC
                }
            },
            {
                "ObjectIdProperty", new FieldTemplate
                {
                    Field = new ReferenceField
                    {
                        Name = "ObjectIdProperty",
                        ReferenceFieldOptions = new ReferenceFieldOptions
                        {
                            ValueType = ValueType.ObjectId,
                        },
                    },
                    RBAC = RBAC
                }
            },
            {
                "UnknownValueTypeId", new FieldTemplate
                {
                    Field = new TextField
                    {
                        Name = "UnknownValueTypeId",
                    },
                    RBAC = RBAC
                }
            },
        }
    };

    private AppDataViewPipelineBuilder GetBuilder(Condition[] criteria) => AppDataViewPipelineBuilder.New(
        null /*connection*/,
        Context,
        new AppDataView
        {
            ObjectType = ObjectType.Name,
            Criteria = new Criteria
            {
                Conditions = criteria.ToArray(),
            },
            Fields =
            [
                Model.IdFieldName,
                nameof(Model.Name),
                nameof(FlowObjectModel.ObjectStatusId),
                nameof(FlowObjectModel.FlowId)
            ],
            OrderBy = Model.IdFieldName,
        },
        ObjectType
    );

    /// <summary>
    /// Hacked version of the AppDataViewPipelineBuilder.BuildMatch 
    /// </summary>
    private BsonDocument FakeBuildMatch(AppDataViewPipelineBuilder builder)
    {
        var parameters = new Dictionary<string, Parameter>();
        
        // important parts from builder.BuildMatch
        builder.CalculateMatchCriteria();
        // var fields = GetIndexedFields();
        var fields = ObjectType.Fields.ToDictionary(x => x.Key, x => x.Value.Field); // assumes they are all indexed
        // var query = _connection.Filter<ExpandoObject>(Collection);
        var query = new Query<ExpandoObject>();
        builder.ApplyDefaultConstraints(query, parameters);
        if (builder.MatchCriteria?.Conditions != null)
        {
            builder.ApplyConditionsToMatchQuery(builder.MatchCriteria.Conditions, query, parameters, fields);
        }
        // var filter = query.GetFilterAsBsonDocument();
        var serializerRegistry = BsonSerializer.SerializerRegistry;
        var documentSerializer = serializerRegistry.GetSerializer<ExpandoObject>();
        var filter = query.Filter.Render(documentSerializer, serializerRegistry);
        
        _testOutputHelper.WriteLine(filter.ToString());
        
        return filter;
    }
    
    public AppDataViewPipelineBuilderTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }
    
    /// <summary>
    /// Simulate Task Scheduler Service Query for Appointments cancelled
    /// </summary>
    [Fact]
    void CanParseGuidArray()
    {
        Guid[] flowIds =
        [
            Guid.NewGuid(),
            Guid.NewGuid(),
        ];

        Condition[] criteria =
        [
            // only one that really matters
            Condition.New(nameof(Appointment.FlowId), Operator.In, flowIds),
            // others
            Condition.Ne(nameof(Appointment.CancelledOn), "{{NULL}}"),
            Condition.New(nameof(Appointment.Start), Operator.Gt, "2024-07-01"),
            Condition.Eq(nameof(Appointment.ObjectStatusId), Guid.NewGuid()),
        ];
        
        var value = criteria[0].GetSerializableValue(ObjectType);
        var array = value as object[];
        array.Should().NotBeNull();
        array.Length.Should().Be(2);
        array[0].Should().Be(flowIds[0].ToString());
        array[1].Should().Be(flowIds[1].ToString());
        
        var builder = GetBuilder(criteria);
        var filter = FakeBuildMatch(builder);
        // assert?
    }
    
    [Fact]
    void JsonArray()
    {
        var flowIds = new Guid[]
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
        };

        Condition[] criteria =
        [
            // only one that really matters
            Condition.New(nameof(Appointment.FlowId), Operator.In, JsonConvert.DeserializeObject(JsonConvert.SerializeObject(flowIds))),
            // others
            Condition.Ne(nameof(Appointment.CancelledOn), "{{NULL}}"),
            Condition.New(nameof(Appointment.Start), Operator.Gt, "2024-07-01"),
            Condition.Eq(nameof(Appointment.ObjectStatusId), Guid.NewGuid()),
        ];

        var value = criteria[0].GetSerializableValue(ObjectType);
        var array = value as object[];
        array.Should().NotBeNull();
        array.Length.Should().Be(2);
        array[0].Should().Be(flowIds[0].ToString());
        array[1].Should().Be(flowIds[1].ToString());

        var builder = GetBuilder(criteria);
        var filter = FakeBuildMatch(builder);
        // assert
    }
    
    [Fact]
    void CanParseObjectArray()
    {
        object[] flowIds =
        [
            Guid.NewGuid(),
            Guid.NewGuid(),
        ];

        Condition[] criteria =
        [
            // only one that really matters
            Condition.New(nameof(Appointment.FlowId), Operator.In, flowIds),
            // others
            Condition.Ne(nameof(Appointment.CancelledOn), "{{NULL}}"),
            Condition.New(nameof(Appointment.Start), Operator.Gt, "2024-07-01"),
            Condition.Eq(nameof(Appointment.ObjectStatusId), Guid.NewGuid()),
        ];

        var value = criteria[0].GetSerializableValue(ObjectType);
        var array = value as object[];
        array.Should().NotBeNull();
        array.Length.Should().Be(2);
        array[0].Should().Be(flowIds[0].ToString());
        array[1].Should().Be(flowIds[1].ToString());

        var builder = GetBuilder(criteria);
        var filter = FakeBuildMatch(builder);
        // assert
    }

    [Fact]
    void DateNotNull()
    {
        Condition[] criteria =
        [
            Condition.Ne(nameof(Appointment.CancelledOn), "{{NULL}}"),
        ];
        
        var builder = GetBuilder(criteria);
        var filter = FakeBuildMatch(builder);
        filter.ToString().Should().Be("{ \"CancelledOn\" : { \"$ne\" : null }, \"AccountId\" : \"fc100000-0000-0000-0000-000000000000\" }");
    }

    [Fact]
    void DateEqNull()
    {
        Condition[] criteria =
        [
            Condition.Eq(nameof(Appointment.CancelledOn), null),
        ];
        
        var builder = GetBuilder(criteria);
        var filter = FakeBuildMatch(builder);
        filter.ToString().Should().Be("{ \"CancelledOn\" : null, \"AccountId\" : \"fc100000-0000-0000-0000-000000000000\" }");
    }

    [Fact]
    void ObjectIds_ArrayOfObjects()
    {        
        object[] flowIds =
        [
            ObjectId.GenerateNewId(),
            ObjectId.GenerateNewId(),
        ];
        
        var condition = Condition.In("IdAsObjectId", flowIds);
        var value = condition.GetSerializableValue(ObjectType);
        var array = value as object[];
        array.Should().NotBeNull();
        array.Length.Should().Be(2);
        array[0].Should().BeOfType<ObjectId>();
        array[1].Should().BeOfType<ObjectId>();
        array[0].Should().Be(flowIds[0]);
        array[1].Should().Be(flowIds[1]);
    }
    
    [Fact]
    void ObjectIds_Enumerable()
    {        
        ObjectId[] flowIds =
        [
            ObjectId.GenerateNewId(),
            ObjectId.GenerateNewId(),
        ];
        
        var condition = Condition.In("ObjectIdProperty", Enumerable.Empty<ObjectId>().Append(flowIds[0]).Append(flowIds[1]));
        var value = condition.GetSerializableValue(ObjectType);
        var array = value as object[];
        array.Should().NotBeNull();
        array.Length.Should().Be(2);
        array[0].Should().BeOfType<ObjectId>();
        array[1].Should().BeOfType<ObjectId>();
        array[0].Should().Be(flowIds[0]);
        array[1].Should().Be(flowIds[1]);
    }   
    
    [Fact]
    void UnknownValueType_ObjectIds_Enumerable()
    {        
        ObjectId[] flowIds =
        [
            ObjectId.GenerateNewId(),
            ObjectId.GenerateNewId(),
        ];
        
        var condition = Condition.In("UnknownValueTypeId", Enumerable.Empty<ObjectId>().Append(flowIds[0]).Append(flowIds[1]));
        var value = condition.GetSerializableValue(ObjectType);
        var array = value as object[];
        array.Should().NotBeNull();
        array.Length.Should().Be(2);
        array[0].Should().BeOfType<ObjectId>();
        array[1].Should().BeOfType<ObjectId>();
        array[0].Should().Be(flowIds[0]);
        array[1].Should().Be(flowIds[1]);
    }

}