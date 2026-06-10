using System;
using Serilog.Core;
using Serilog.Events;

namespace Crochik.Logging;

public class EnvironmentEnricher : ILogEventEnricher
{
    private LogEventProperty _environmentNameProperty;
    private LogEventProperty _containerImageProperty;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (_environmentNameProperty == null)
        {
            var name = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            _environmentNameProperty = propertyFactory.CreateProperty("environment", name);

            name = Environment.GetEnvironmentVariable("PI_CONTAINER");
            if (!string.IsNullOrEmpty(name))
            {
                _containerImageProperty = propertyFactory.CreateProperty("container", name);
            }
        }

        logEvent.AddPropertyIfAbsent(_environmentNameProperty);
        if (_containerImageProperty != null)
        {
            logEvent.AddPropertyIfAbsent(_containerImageProperty);
        }
    }
}