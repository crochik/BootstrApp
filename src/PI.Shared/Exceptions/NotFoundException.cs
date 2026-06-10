using System;

namespace PI.Shared.Exceptions;

public class NotFoundException : Exception, IHttpRequestException
{
    public static NotFoundException New<T>(Guid id) => new(typeof(T).Name, id);
    public static NotFoundException New(string message) => new(message);
    public static NotFoundException New(string objectType, Guid id) => new(objectType, id);

    public string ObjectType { get; }
    public Guid? Id { get; }

    public int? StatusCode => 404;

    public NotFoundException(string message = "Not Found") :
        base(message)
    {
    }

    public NotFoundException(string objectType, Guid? id) :
        base($"{objectType}({id}) not found")
    {
        ObjectType = objectType;
        Id = id;
    }
}
