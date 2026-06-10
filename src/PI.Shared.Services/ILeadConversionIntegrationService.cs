using System;
using System.Threading.Tasks;
using PI.Shared.Models;

namespace PI.Shared.Services;

public interface ILeadConversionIntegrationService
{
    Guid IntegrationId { get; }
    string ClientId { get; }
    Task<IResult> ConditionallyPostLeadAsync(Lead lead);
    Task<Guid> AddNoteAsync(IEntityContext context, Guid leadId, string subject, string content, ContentFormat format);
    Task<Guid> AddNoteAsync(IEntityContext context, Lead lead, string subject, string content = null, ContentFormat? format = null);
}