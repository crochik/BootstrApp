using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Mongo;
using Models;
using PI.Shared.Models;

namespace Adapters
{
    public class SingerConfigAdapter : ISingerConfigAdapter
    {
        private readonly MongoConnection _connection;

        public SingerConfigAdapter(MongoConnection connection)
        {
            this._connection = connection;
        }

        private async Task<SingerJob> AddAsync(SingerJob singerImport)
        {
            await _connection.InsertAsync<SingerJob>(singerImport);
            return singerImport;
        }

        public Task AddAsync(Guid id, SingerMetricMessage metric)
            => _connection.Filter<SingerJob>()
                .Eq(x => x.Id, id)
                .Update
                    .AddToSet(x => x.ExtractMetrics, metric)
                .UpdateAndGetOneAsync();

        public Task AddToExtractLogAsync(Guid id, string message)
            => _connection.Filter<SingerJob>()
                .Eq(x => x.Id, id)
                .Update
                    .AddToSet(x => x.ExtractLog, message)
                .UpdateAndGetOneAsync();

        public async Task<SingerJob> InitExtractAsync(SingerImportConfig config)
        {
            if (config == null) throw new SingerException("Configuration required");

            var startedOn = DateTime.UtcNow;
            var tag = startedOn.ToString("yyyyMMdd_HHmmss");
            config = await _connection.Filter<SingerImportConfig>()
                .Eq(x => x.Id, config.Id)
                .OrBuilder(
                    q => q.Eq(x => x.CurrentTag, null),
                    q => q.Lt(x => x.ExtractStartedOn, DateTime.UtcNow.AddHours(-4)) // will retry every 4 hours
                )
                .Update
                    .Set(x => x.CurrentTag, tag)
                    .Set(x => x.ExtractStartedOn, startedOn)
                    .Unset(x => x.LoadEndedOn)
                .UpdateAndGetOneAsync();

            if (config == null) throw new SingerException("Busy");

            var job = await AddAsync(new SingerJob
            {
                Id = Guid.NewGuid(),
                AccountId = config.AccountId,
                ConfigId = config.Id,
                Tag = tag,
                InitialState = config.State,
                StartedOn = startedOn,
                ExtractLog = new[] { $"=== Start: {DateTime.UtcNow}" }
            });

            return job;
        }

        public async Task EndExtractAsync(SingerJob extract)
        {
            var endedOn = DateTime.UtcNow;

            var result = await _connection.Filter<SingerJob>()
                .Eq(x => x.Id, extract.Id)
                .Update
                    .Set(x => x.ExtractEndedOn, endedOn)
                    .AddToSet(x => x.ExtractLog, $"=== End: {endedOn}")
                .UpdateAndGetOneAsync();
        }

        public Task<SingerImportConfig> GetByIdAsync(Guid id)
            => _connection.Filter<SingerImportConfig>()
                .Eq(x => x.Id, id)
                .FirstOrDefaultAsync();

        public async Task<SingerImportConfig> GetDefaultForAccountAsync(Guid accountId)
        {
            var list = await _connection.Filter<SingerImportConfig>()
                .Eq(x => x.AccountId, accountId)
                .FindAsync();

            return list.Count == 1 ? list[0] : null;
        }

        public async Task<SingerJob> MarkLoadCompleteAsync(Guid id, SingerState state)
        {
            var endedOn = DateTime.UtcNow;

            var result = await _connection.Filter<SingerJob>()
                .Eq(x => x.Id, id)
                .Update
                    .Set(x => x.LoadEndedOn, endedOn)
                    .Set(x => x.State, state)
                .UpdateAndGetOneAsync();

            await _connection.Filter<SingerImportConfig>()
                .Eq(x => x.Id, result.ConfigId)
                .Update
                    .Set(x => x.State, result.State)
                    .Set(x => x.LoadEndedOn, endedOn)
                    .Unset(x => x.CurrentTag)
                .UpdateOneAsync();

            return result;
        }

        public Task<SingerJob> MarkLoadStartAsync(Guid configId, string tag)
            => _connection.Filter<SingerJob>()
                .Eq(x => x.ConfigId, configId)
                .Eq(x => x.Tag, tag)
                .Update
                    .Set(x => x.LoadStartedOn, DateTime.UtcNow)
                    .Unset(x => x.LoadEndedOn)
                .UpdateAndGetOneAsync();

        public Task UpdateAsync(Guid id, SingerState state)
            => _connection.Filter<SingerJob>()
                .Eq(x => x.Id, id)
                .Update.Set(x => x.State, state)
                .UpdateOneAsync();

        public async Task LogAsync(SingerLoadingLog item)
        {
            // await _connection.Filter<SingerJob>()
            //     .Eq(x => x.Id, id)
            //     .Update
            //         // .Push($"{nameof(SingerJob.LoadMetrics)}.{stream}.{nameof(SingerLoadMetrics.Errors)}", error)
            //         .Inc($"{nameof(SingerJob.LoadMetrics)}.{stream}.{nameof(SingerLoadMetrics.Total)}", 1)
            //     .UpdateOneAsync();

            await _connection.InsertAsync(item);
        }

        public async Task<IEnumerable<SingerImportConfig>> GetAsync(IEntityContext context)
        {
            var entityId = context.Role switch
            {
                EntityRoleId.Account => context.AccountId.Value,
                EntityRoleId.Admin => context.AccountId.Value,
                _ => throw new Exception("Not Authorized")
            };

            return await _connection.Filter<SingerImportConfig>()
                .Eq(x => x.AccountId, entityId)
                .FindAsync();
        }

        public Task<SingerJob> GetJobByIdAsync(Guid id)
            => _connection.Filter<SingerJob>().Eq(x => x.Id, id).FirstOrDefaultAsync();

        public async Task<IEnumerable<SingerJobSummary>> GetJobSummaryAsync(Guid id)
            => await _connection.DipperAggregateAsync<SingerJobSummary>("SingerLoadingLog.Summary", "global", new { JobId = id.ToString() });
    }
}
