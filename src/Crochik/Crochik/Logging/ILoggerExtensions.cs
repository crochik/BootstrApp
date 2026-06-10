using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Crochik.Logging
{
    public static class ILoggerExtensions
    {
        public static IDisposable AddScope(this ILogger logger, object state)
        {
            var dict = new Dictionary<string, object>();
            foreach (var prop in state.GetType().GetProperties())
            {
                var value = prop.GetValue(state);
                if (value == null) continue;
                dict.Add(prop.Name, value);
            }

            return logger.BeginScope(dict);
        }
    }
}