using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OpenApi;

namespace PI.Shared.Services.OpenApiGenerator;

public class Layer
{
    public Layer Parent { get; set; }
}

public class ArrayLayer : Layer
{
    public ArrayLayer(Layer parent)
    {
        Parent = parent;
    }

    public List<object> Array { get; set; } = new List<object>();
}

public class ObjectLayer : Layer
{
    public ObjectLayer(Layer parent)
    {
        Parent = parent;
    }

    public Dictionary<string, object> Object { get; set; } = new Dictionary<string, object>();
}

public class PropertyLayer : Layer
{
    public PropertyLayer(Layer parent, string prop)
    {
        Parent = parent;
        Property = prop;
    }

    public string Property { get; set; }
}

public class ObjectWriter : IOpenApiWriter
{
    public bool IgnoreNullPropertyValues = true;

    public Layer CurrentLayer { get; set; }
    public Dictionary<string, object> Result { get; set; }

    public ObjectWriter()
    {
    }

    public void WriteStartObject()
    {
        var objectLayer = new ObjectLayer(CurrentLayer);
        Result ??= objectLayer.Object;
        CurrentLayer = objectLayer;
    }

    public void WriteEndObject()
    {
        if (CurrentLayer is not ObjectLayer objectLayer)
        {
            throw new Exception("Unexpected Layer");
        }

        if (CurrentLayer.Parent != null)
        {
            CurrentLayer = CurrentLayer.Parent;
            SetValue(objectLayer.Object);
        }
    }

    private void SetValue(object value)
    {
        switch (CurrentLayer)
        {
            case PropertyLayer prop:
            {
                if (prop.Parent is not ObjectLayer parentObject) throw new Exception("Invalid parent");
                if (value != null || !IgnoreNullPropertyValues)
                {
                    parentObject.Object[prop.Property] = value;
                }

                CurrentLayer = parentObject;
                break;
            }
            case ArrayLayer array:
            {
                array.Array.Add(value);
                CurrentLayer = array;
                break;
            }

            default:
                throw new Exception("Unexpected layer");
        }
    }

    public void WriteStartArray()
    {
        CurrentLayer = new ArrayLayer(CurrentLayer);
    }

    public void WriteEndArray()
    {
        if (CurrentLayer is not ArrayLayer arrayLayer)
        {
            throw new Exception("Unexpected Layer");
        }

        CurrentLayer = CurrentLayer.Parent;
        SetValue(arrayLayer.Array);
    }

    public void WritePropertyName(string name)
    {
        CurrentLayer = new PropertyLayer(CurrentLayer, name);
    }

    public void WriteValue(string value)
    {
        SetValue(value);
    }

    public void WriteValue(decimal value)
    {
        SetValue(value);
    }

    public void WriteValue(int value)
    {
        SetValue(value);
    }

    public void WriteValue(bool value)
    {
        SetValue(value);
    }

    public void WriteNull()
    {
        SetValue(null);
    }

    public void WriteRaw(string value)
    {
        Console.WriteLine($"?RAW? {value}");
    }

    public void WriteValue(object value)
    {
        SetValue(value);
    }

    public Task FlushAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        // ...
        return Task.CompletedTask;
    }

    public void Flush()
    {
        // ...
    }
}