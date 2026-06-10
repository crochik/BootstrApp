using System.Threading.Tasks;
using MongoDB.Driver;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public interface IObjectImporter
{
    string SourceObjectTypeName { get; }
    Task ImportAsync(IEntityContext context);
}

public interface IObjectImporter<TSrc, TDst> : IObjectImporter
    where TSrc : SalesforceCustomObject
    where TDst : IModel
{
    Task<WriteModel<TDst>> ImportAsync(IEntityContext context, TSrc row);
}