using System;
using System.Collections.Generic;
using System.Linq;
using PI.ProductCatalog.Models;

namespace PI.ProductCatalog.Edi832
{
    public interface ICatalogFormat
    {
        string Name { get; }

        Uri Url { get; }

        string SenderId { get; }

        bool SkipInvalidPrices { get; }

        /// <summary>
        /// Whether to create the name using the style + color 
        /// </summary>
        bool UseStyleAndColorName { get; }

        IEnumerable<string> IgnoreColorProperties { get; }

        Dictionary<string, ILineParser> GetParsers(CatalogUpdate catalogUpdate, Loop loop);

        /// <summary>
        /// Allow sender to override mapping UOM
        /// </summary>
        UnitOfMeasurement ParseUOM(object value);
    }

    public abstract class AbstractCatalogFormat : ICatalogFormat
    {
        protected Dictionary<string, ILineParser>[] _cache = new Dictionary<string, ILineParser>[(int)Loop.MAX];

        public abstract string Name { get; }
        public abstract string SenderId { get; }
        public abstract Uri Url { get; }
        public virtual bool SkipInvalidPrices => true;
        public virtual bool UseStyleAndColorName => false;
        public virtual IEnumerable<string> IgnoreColorProperties => Enumerable.Empty<string>();
        public virtual ILineParser[] HeaderParsers { get; }


        public Dictionary<string, ILineParser> GetParsers(CatalogUpdate catalogUpdate, Loop loop)
        {
            var index = (int)loop;
            if (_cache[index] == null)
            {
                _cache[index] = Init(catalogUpdate, loop);
            }

            return _cache[index];
        }

        protected abstract Dictionary<string, ILineParser> Init(CatalogUpdate catalogUpdate, Loop loop);

        protected static bool TryGetParser<T>(Dictionary<string, ILineParser> parsers, string code, out T parser) where T : class
        {
            if (parsers.TryGetValue(code, out var p) && p is T obj)
            {
                parser = obj;
                return true;
            }

            parser = null;
            return false;
        }

        public virtual UnitOfMeasurement ParseUOM(object value) => Measurement.Parse(value?.ToString());
    }
}