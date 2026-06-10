using System.Collections.Generic;

namespace PI.Shared.Models;

public static class Result
{
    public static Result<T> Success<T>(T value, string status = null) => Result<T>.Success(value, status);
    public static Result<T> Error<T>(string status = null) => Result<T>.Error(status);
    public static Result<object> Error(string status = null) => Result<object>.Error(status);
    public static Result<T> Unknown<T>(string status) => Result<T>.Success(default, status);
    public static Result<object> Unknown(string status) => Result<object>.Success(null, status);
}

public interface IResult
{
    string Status { get; }
    bool IsSuccess { get; }
    bool IsError { get; }
    bool IsUnknown { get; }
    object ObjectValue { get; }
}

public class Result<T> : IResult
{
    public T Value { get; protected set; }

    public string Status { get; protected set; }
    public bool IsSuccess => Value != null;
    public bool IsError { get; protected set; }
    public bool IsUnknown => !(IsError || IsSuccess);
    public object ObjectValue => Value;

    public Result<TOut> ConvertTo<TOut>() 
    {
        return new Result<TOut>
        {
            Value = Value is TOut converted ? converted : default(TOut),
            IsError = IsError,
            Status = Status,  
        };
    }

    public static Result<T> Success(T value, string status = null)
    {
        return new Result<T>
        {
            Value = value,
            Status = status,
        };
    }

    public static Result<T> Error(string error)
    {
        return new Result<T>
        {
            IsError = true,
            Status = error,
        };
    }

    public static Result<T> Unknown(string status = null)
    {
        return new Result<T>()
        {
            Status = status
        };
    }

    public static implicit operator bool(Result<T> result) => result.IsSuccess;
}
    
public class AsyncResultStream : Result<IAsyncEnumerable<IResult>>
{
    public static AsyncResultStream Success(IAsyncEnumerable<IResult> value, string status = null)
    {
        return new AsyncResultStream
        {
            Value = value,
            Status = status,
        };
    }    
    public static AsyncResultStream Error(string error)
    {
        return new AsyncResultStream
        {
            IsError = true,
            Status = error,
        };
    }

    public static AsyncResultStream Unknown(string status = null)
    {
        return new AsyncResultStream()
        {
            Status = status
        };
    }        
}