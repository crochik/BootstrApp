using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MongoDB.Bson.Serialization.Attributes;

namespace IDP
{
    [BsonCollection("idp.AuthenticationTicket")]
    public class AuthenticationTicketCookie
    {
        [BsonId]
        public Guid Id { get; set; }

        public DateTime IssuedOn { get; set; }
        public byte[] Data { get; set; }
        public string AuthenticationScheme { get; set; }
        public string Name { get; set; }
        public string AuthenticationType { get; set; }
    }

    public class MongoCacheTicketStore : ITicketStore
    {
        private static MongoCacheTicketStore _instance = null;
        public static MongoCacheTicketStore Get() => _instance ??= new MongoCacheTicketStore();

        public MongoConnection Connection { get; internal set; }

        public async Task RenewAsync(string key, AuthenticationTicket ticket)
        {
            var token = new AuthenticationTicketCookie
            {
                Id = Guid.Parse(key),
                Data = TicketSerializer.Default.Serialize(ticket),
                AuthenticationScheme = ticket.AuthenticationScheme,
                Name = ticket.Principal.Identity?.Name,
                AuthenticationType = ticket.Principal.Identity?.AuthenticationType,
                IssuedOn = ticket.Properties.IssuedUtc?.UtcDateTime ?? DateTime.UtcNow
            };

            var result = await Connection.Filter<AuthenticationTicketCookie>()
                .Eq(x => x.Id, token.Id)
                .ReplaceOneAsync(token, true);
        }

        public async Task RemoveAsync(string key)
        {
            var id = Guid.Parse(key);

            await Connection.Filter<AuthenticationTicketCookie>()
                .Eq(x => x.Id, id)
                .DeleteOneAsync();
        }

        async Task<AuthenticationTicket> ITicketStore.RetrieveAsync(string key)
        {
            var id = Guid.Parse(key);

            var token = await Connection.Filter<AuthenticationTicketCookie>()
                .Eq(x => x.Id, id)
                .FirstOrDefaultAsync();

            if (token == null) return null;

            return TicketSerializer.Default.Deserialize(token.Data);
        }

        async Task<string> ITicketStore.StoreAsync(AuthenticationTicket ticket)
        {
            var token = new AuthenticationTicketCookie
            {
                Id = Guid.NewGuid(),
                Data = TicketSerializer.Default.Serialize(ticket),
                AuthenticationScheme = ticket.AuthenticationScheme,
                Name = ticket.Principal.Identity?.Name,
                AuthenticationType = ticket.Principal.Identity?.AuthenticationType,
                IssuedOn = ticket.Properties.IssuedUtc?.UtcDateTime ?? DateTime.UtcNow
            };

            await Connection.InsertAsync(token);

            return token.Id.ToString();
        }
    }
}