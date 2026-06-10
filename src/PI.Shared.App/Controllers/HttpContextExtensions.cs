using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace PI.Shared.Models
{
    public static class HttpContextExtensions
    {
        private const string ItemKey = "PiActorContext";

        public static IContextWithActor GetContextWithActor(this HttpContext httpContext)
        {
            if (httpContext.User?.Identity is not ClaimsIdentity identity) return null;
            if (!identity.IsAuthenticated) return null;

            // cached?
            if (httpContext.Items.TryGetValue(ItemKey, out var actor))
            {
                return actor as IContextWithActor;
            }

            var actorContext = identity.GetEntityContextWithActor();
            httpContext.Items.Add(ItemKey, actorContext);

            return actorContext;
        }
    }
}