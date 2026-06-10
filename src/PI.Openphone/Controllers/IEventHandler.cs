using System.Threading.Tasks;
using PI.Openphone.Models;
using PI.Shared.Models;

namespace PI.Openphone.Controllers;

public interface IEventHandler
{
    public string ObjectType { get; }
    public Task HandleAsync(Organization organization, OpenPhoneIntegrationConfiguration integration, OpenPhoneEvent evt);
}