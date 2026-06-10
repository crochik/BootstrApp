using System;
using System.Collections.Generic;
using PI.Shared.App;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace Services
{
    [Obsolete]
    public interface ICacheService : ILifetimeService
    {
        Dictionary<Guid, AppIntegration> Integrations { get; }
    }

    [Obsolete]
    public class CacheService : ICacheService
    {
        public static ICacheService Instance { get; private set; }

        private Dictionary<Guid, AppIntegration> _integrations = null;
        private readonly IIntegrationAdapter _integrationAdapter;

        public Dictionary<Guid, AppIntegration> Integrations
        {
            get
            {
                if (_integrations == null)
                {
                    var dict = new Dictionary<Guid, AppIntegration>();
                    var list = _integrationAdapter.GetAllAsync().Result;
                    foreach (var i in list)
                    {
                        dict.Add(i.Id, i);
                    }
                    _integrations = dict;
                }

                return _integrations;
            }
        }

        public CacheService(
            IIntegrationAdapter integrationAdapter
            )
        {
            this._integrationAdapter = integrationAdapter;
        }

        public void Start()
        {
            CacheService.Instance = this;
        }

        public void Stop()
        {
        }
    }
}