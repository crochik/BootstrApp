using System;

namespace PI.Shared.Exceptions;

public class NotModifiedException(string objectType, object id) : Exception($"{objectType}({id}) not modified"), IHttpRequestException
{
    public int? StatusCode => 304;
}