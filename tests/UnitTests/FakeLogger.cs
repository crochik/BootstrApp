using System;
using Microsoft.Extensions.Logging;

namespace UnitTests;

public class FakeDisposable : IDisposable
{
    public void Dispose()
    {
    }
}

public class FakeLogger<T> : ILogger<T>
{
    public IDisposable BeginScope<TState>(TState state) => new FakeDisposable();
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        Console.WriteLine(formatter(state,exception));
    }
}