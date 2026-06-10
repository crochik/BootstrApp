using System;
using System.Threading.Tasks;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public interface IObjectChangeProcessor 
{
    string ObjectType { get; }
    
    Task<(SalesforceCustomObject, IFlowObject)> ProcessChangeAsync(Guid accountId, string externalId, DateTime? timestamp);
    Task<(SalesforceCustomObject, IFlowObject)> ProcessChangeAsync(ProcessObjectChange change);
    
    Task<IFlowObject> ImportObjectAsync(IEntityContext context, SalesforceObjectType objectType, SalesforceCustomObject source);
    Task<(SalesforceCustomObject Source, IFlowObject Imported)> ImportObjectAsync(IEntityContext context, SalesforceObjectType objectType, string externalId);
}

public interface IOnLeadChangeProcessor : IObjectChangeProcessor
{
}

public interface IOnAccountChangeProcessor : IObjectChangeProcessor
{
}

public interface IOnServiceAppointmentChangeProcessor : IObjectChangeProcessor
{
}

public interface IOnWorkOrderChangeProcessor : IObjectChangeProcessor
{
}
