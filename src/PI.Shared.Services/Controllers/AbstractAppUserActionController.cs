using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using PI.Shared.Attributes;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Requests;
using PI.Shared.Services;
using JsonConverter = System.Text.Json.Serialization.JsonConverter;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace PI.Shared.Controllers;

public abstract class AbstractAppUserActionController(
    ILogger<AbstractAppUserActionController> logger,
    MongoConnection connection,
    UserActionService service,
    ObjectTypeService objectTypeService
) : AbstractUserActionController(logger, connection, service, objectTypeService)
{
    /// <summary>
    /// json single line using property names ("api names") / converters 
    /// </summary>
    private static readonly JsonSerializerOptions _sseJsonOptions = new()
    {
        WriteIndented = false, // This is the default, but being explicit helps
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null, // This is the key line
        Converters =
        {
            new FlagsEnumConverterFactory(), // before JsonStringEnumConverter or will result in strings not numbers
            new JsonStringEnumConverter(),
            new Decimal128Converter(),
            new ObjectIdConverter(),
        },
    };

    /// <summary>
    /// SSE Event from object
    /// </summary>
    private static string SerializeSseEvent<T>(T data, string eventName = null)
    {
        var json = JsonSerializer.Serialize(data, _sseJsonOptions);
        return eventName != null
            ? $"event: {eventName}\ndata: {json}\n\n"
            : $"data: {json}\n\n";
    }

    /// <summary>
    /// Get Form triggered by User Action (potentially for multiple objects)
    /// </summary>
    [HttpGet("{objectTypeName}/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public Task<Form.Models.Form> GetActionAsync([FromRoute] string objectTypeName, [FromRoute] Guid eventId)
    {
        return BuildActionFormForEventAsync(objectTypeName, eventId);
    }

    /// <summary>
    /// Execute user action with custom request and response payload
    ///     * for entity object types, if selected id is missing, will infer from the context 
    /// </summary>
    [HttpPost("{objectTypeName}({id})/CustomAction({eventId})")]
    [UseApiNames]
    public async IAsyncEnumerable<string> RunCustomActionAsync([FromRoute] string objectTypeName, [FromRoute] Guid id, [FromRoute] Guid eventId, [FromBody] Dictionary<string, object> request, [EnumeratorCancellation] CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var channel = Channel.CreateUnbounded<IResult>();
        try
        {
            // Fire and forget the execution or run it in a Task.Run 
            // if it doesn't internally handle async concurrency.
            _ = Task.Run(async () => 
            {
                try 
                {
                    var result = await ExecuteCustomActionAsync(objectTypeName, id, eventId, request, channel, ct);
                    if (result.IsError)
                    {
                        await channel.Writer.WriteAsync(result, ct);
                    }
                }
                finally 
                {
                    channel.Writer.TryComplete(); 
                }
            }, ct);

            // Stream the results as they arrive
            await foreach (var item in channel.Reader.ReadAllAsync(ct))
            {
                if (item.IsError)
                {
                    yield return SerializeSseEvent(new { error = item.Status }, "error");
                }
                else if (item.IsUnknown)
                {
                    yield return SerializeSseEvent(new { status = item.Status }, "status");
                }
                else
                {
                    yield return SerializeSseEvent(item.ObjectValue, item.ObjectValue.GetType().Name);
                }
            }
        }
        finally
        {
            // Ensure cleanup if the consumer stops listening early
            channel.Writer.TryComplete();
        }
        
        // await foreach (var item in result.Value.WithCancellation(ct))
        // {
        //     if (item.IsError)
        //     {
        //         yield return SerializeSseEvent(new { error = item.Status }, "error");
        //     }
        //     else if (item.IsUnknown)
        //     {
        //         yield return SerializeSseEvent(new { status = item.Status }, "status");
        //     }
        //     else
        //     {
        //         yield return SerializeSseEvent(item.ObjectValue, item.ObjectValue.GetType().Name);
        //     }
        // }
    }
    
    /// <summary>
    /// Execute user action
    ///     * for entity object types, if selected id is missing, will infer from the context 
    /// </summary>
    [HttpPost("{objectTypeName}/UserAction({eventId})/DataForm")]
    [HttpPost("{objectTypeName}/UserAction({eventId})")]
    [UseApiNames]
    public Task<DataFormActionResponse> RunActionAsync([FromRoute] string objectTypeName, [FromRoute] Guid eventId, [FromBody] DataFormActionRequest request)
    {
        return RunUserActionAsync(objectTypeName, eventId, request);
    }

    /// <summary>
    /// Get Form for a specific object so it can use its data to seed fields 
    /// </summary>
    [HttpGet("{objectTypeName}({objectId})/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public Task<Form.Models.Form> GetActionForExistingObjectAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromRoute] Guid eventId)
    {
        var runContext = BuildRunContext();
        return _service.BuildActionFormForObjectAsync(Context, objectTypeName, objectId, eventId, runContext);
    }

    /// <summary>
    /// Run action triggered by User Action (for one object)
    /// </summary>
    [HttpPost("{objectTypeName}({objectId})/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public Task<DataFormActionResponse> RunActionForObjectAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromRoute] Guid eventId, [FromBody] DataFormActionRequest request)
    {
        return _service.ExecuteForObjectAsync(Context, objectTypeName, objectId, eventId, request);
    }

    /// <summary>
    /// Start action (body is not a DataFormActionRequest)
    /// </summary>
    [HttpPost("{objectTypeName}({objectId})/UserAction({eventId})")]
    [UseApiNames]
    public Task<DataFormActionResponse> StartActionForObjectAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromRoute] Guid eventId, [FromBody] Dictionary<string, object> body)
    {
        var actionRequest = new DataFormActionRequest
        {
            Action = null, // it will be set using the event later on
            Parameters = body,
            SelectedIds = [objectId],
            View = null,
        };

        return _service.ExecuteForObjectAsync(Context, objectTypeName, objectId, eventId, actionRequest);
    }

    /// <summary>
    /// Get Form in the middle of flow 
    /// </summary>
    [HttpGet("{objectTypeName}({objectId})/Flow({flowRunId})/UserAction({eventId})/DataForm")]
    [UseApiNames]
    public Task<Form.Models.Form> GetActionAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromRoute] Guid eventId, [FromRoute] Guid flowRunId)
    {
        return BuildActionFormForFlowRunAsync(objectTypeName, objectId, eventId, flowRunId);
    }

    /// <summary>
    /// Execute action triggered by User Action (for one object)
    /// </summary>
    [HttpPost("{objectTypeName}({objectId})/Flow({flowRunId})/UserAction({eventId})/DataForm")]
    [HttpPost("{objectTypeName}({objectId})/Flow({flowRunId})/UserAction({eventId})")] // TODO: probably need to handle differently as the body will not be DataFormActionRequest
    [UseApiNames]
    public Task<DataFormActionResponse> RunActionInFlowAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId, [FromRoute] Guid eventId, [FromRoute] Guid flowRunId, [FromBody] DataFormActionRequest request)
    {
        return RunUserActionAsync(objectTypeName, objectId, eventId, flowRunId, request);
    }

    [HttpGet("{objectTypeName}({objectId})/Menu")]
    [UseApiNames]
    public async Task<Menu> GetActionsMenuForObjectAsync([FromRoute] string objectTypeName, [FromRoute] Guid objectId)
    {
        var objectType = await _objectTypeService.GetAsync(Context, objectTypeName);
        if (objectType == null) throw new NotFoundException("ObjectType");

        var c = new ObjectTypeService.GetUserActionsMenuContext
        {
            Context = Context,
            ObjectType = objectType,
            ObjectId = objectId,
            IncludeMultiple = true,
            SkipToNextUrlWhenNotForm = true,
        };

        var (_, menuItems) = await _objectTypeService.UserActionsMenuItemsAsync(c);
        // var (_, menuItems) = await _objectTypeService.GetUserActionsMenuItemsAsync(Context, objectType, objectId, includeMultiple: true);

        return new Menu
        {
            Name = "Actions",
            Icon = nameof(Icons.Action),
            Items = menuItems,
        };
    }
}

public class Decimal128Converter : System.Text.Json.Serialization.JsonConverter<Decimal128>
{
    public override Decimal128 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Handle Numbers (Float or Integer)
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetDecimal(out var decimalValue))
            {
                return (Decimal128)decimalValue;
            }
        }

        // Handle Strings
        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (decimal.TryParse(stringValue, out var decimalValue))
            {
                return (Decimal128)decimalValue;
            }
        }

        throw new JsonException($"Unexpected token {reader.TokenType} when deserializing Decimal128.");
    }

    public override void Write(Utf8JsonWriter writer, Decimal128 value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue((decimal)value);
    }
}

public class FlagsEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        var type = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
        return type.IsEnum && type.GetCustomAttribute<FlagsAttribute>() != null;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return new FlagsEnumConverter();
    }

    private class FlagsEnumConverter : System.Text.Json.Serialization.JsonConverter<object>
    {
        // We use object here to handle the dynamic nature of the factory
        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.Number)
            {
                throw new JsonException($"Expected number for Flags enum, found {reader.TokenType}.");
            }

            // Get the underlying type (int, byte, etc) and convert
            var underlyingType = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
            return Enum.ToObject(underlyingType, reader.GetInt32());
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNumberValue(0);
            }
            else
            {
                // Cast to int to ensure numeric output per your Newtonsoft logic
                writer.WriteNumberValue((int)value);
            }
        }
    }
}

public class ObjectIdConverter: System.Text.Json.Serialization.JsonConverter<ObjectId>
{
    public override ObjectId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected string for ObjectId, found {reader.TokenType}.");
        }

        var str = reader.GetString();
        if (Guid.TryParse(str, out var uuid) && uuid.TryGetObjectId(out var objectId))
        {
            return objectId;
        }

        throw new JsonException($"String '{str}' is not a valid 24-digit Hex or Guid-based ObjectId.");
    }

    public override void Write(Utf8JsonWriter writer, ObjectId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToGuid().ToString());
    }
}