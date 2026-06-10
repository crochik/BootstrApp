using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using MongoDB.Driver;
using PI.Shared.Models;

namespace PI.Shared.Data.Mongo.Adapters
{
    public class AvailabilityAdapter 
    {
        private readonly MongoConnection _connection;

        public AvailabilityAdapter(MongoConnection connection)
        {
            this._connection = connection;
        }

        public async Task<bool> DeleteAsync(Guid userId, Guid id)
        {
            var result = await _connection.Filter<User>()
                .Eq(x => x.Id, userId)
                .Update
                    .PullFilter(
                        x => x.Availability,
                        Builders<Availability>.Filter.Eq(x => x.Id, id)
                    )
                .UpdateOneAsync();

            return result.MatchedCount == 1;
        }

        public async Task<bool> IsUserAvailableAsync(Guid userId, Guid appointmentTypeId, int dayId, int startMinutes, int endMinutes)
        {
            var result = await _connection.Filter<User>()
                .Eq(x => x.Id, userId)
                .ElemMatchBuilder(x => x.Availability,
                    f => f.Eq(a => a.DayId, (DayOfWeek)dayId)
                        .Lte(a => a.StartMinutes, startMinutes)
                        .Gte(a => a.EndMinutes, endMinutes)
                    // Builders<Availability>.Filter.Eq(a => a.DayId, (DayOfWeek)dayId) &
                    // Builders<Availability>.Filter.Lte(a => a.StartMinutes, startMinutes) &
                    // Builders<Availability>.Filter.Gte(a => a.EndMinutes, endMinutes)
                ).FirstOrDefaultAsync();

            return result != null;
        }
    }
}