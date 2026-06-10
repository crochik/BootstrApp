using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PI.Shared.Constants;
using PI.Shared.Models;

namespace PI.Shared.Data.Adapters
{
    public interface IIntegrationAdapter
    {
        IEnumerable<AppIntegration> GetAll();
        AppIntegration GetByServiceName(string name);
        AppIntegration GetById(Guid id);

        Task<IEnumerable<AppIntegration>> GetAllAsync();
        Task<AppIntegration> GetByServiceNameAsync(string name);
    }

    public interface IIntegrationAdapter<TObj, T>
    {
        Task<T> AddAsync(IEntityContext context, T dao);
        Task<(TObj, T)> FindAsync(Guid integrationId, string externalId);
        Task<IEnumerable<T>> GetAsync(Guid value);
        Task<T> UpsertAsync(IEntityContext context, T integration);
    }

    public interface IIntegrationLeadAdapter : IIntegrationAdapter<Lead, IIntegrationLead>
    {
        Task<IIntegrationLead> FindAsync(string serviceName, string externalId);
        Task<bool> UpdateAsync(IIntegrationLead iLead);
        Task<IIntegrationLead> PatchAsync(IEntityContext context, LeadIntegration createOrUpdate);
    }

    public interface IIntegrationAppointmentAdapter : IIntegrationAdapter<Appointment, IIntegrationAppointment>
    {
        Task<bool> UpdateStatusAsync(Guid appointmentId, Guid integrationId, string externalId, string status, string url);
    }

    [Obsolete]
    public class IntegrationAdapter : IIntegrationAdapter
    {
        public IEnumerable<AppIntegration> GetAll() => IntegrationIds.All.Select(x => New(x.Key, x.Value));

        protected static IReadOnlyDictionary<string, AppIntegration> _byName = IntegrationIds.All
            .Select(x => New(x.Key, x.Value))
            .ToDictionary(x => x.ServiceName);

        public Task<IEnumerable<AppIntegration>> GetAllAsync()
            => Task.FromResult<IEnumerable<AppIntegration>>(GetAll());

        public AppIntegration GetByServiceName(string name) => _byName[name];

        public Task<AppIntegration> GetByServiceNameAsync(string name)
            => Task.FromResult<AppIntegration>(GetByServiceName(name));

        private static AppIntegration New(Guid id, string name, string serviceName = null, string description = null)
            => new AppIntegration
            {
                Id = id,
                Name = name,
                ServiceName = serviceName ?? name,
                Description = description
            };

        public AppIntegration GetById(Guid id) => IntegrationIds.All.TryGetValue(id, out var name) ? New(id, name) : null;
    }
}