using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Services;
using Qvinci.Models;

namespace Qvinci;

public class QvinciExtractJob : IRunJob
{
    private readonly ILogger<QvinciExtractJob> _logger;
    private readonly MongoConnection _connection;
    private readonly Client _client;

    private Regex expr = new Regex("^([0-9]+)\\s");

    public string Name => "QvinciExtract";

    public QvinciExtractJob(
        ILogger<QvinciExtractJob> logger,
        MongoConnection connection,
        Client client
    )
    {
        _logger = logger;
        _connection = connection;
        _client = client;
    }

    public async Task<JobResult> ExecuteAsync(IEntityContext context, CancellationToken stoppingToken)
    {
        var locations = await SyncLocationsAsync(context);
        // var locations = new[] {
        //     new QvinciLocation
        //     {
        //         Id = 2418416,
        //         CompanyId = 1388387
        //     }
        // };

        var reports = new[]
        {
            QvinciReport.Aging,
            QvinciReport.AP,
            QvinciReport.BalanceSheet_LastYear,
            QvinciReport.BalanceSheet_YTD,
            QvinciReport.PNL_LastYear,
            QvinciReport.PNL_YTD,
        };

        foreach (var location in locations)
        {
            foreach (var report in reports)
            {
                await LoadReportAsync(context, location, report);
            }
        }

        return new JobResult
        {
            Message = "Loaded reports",
            // ...
        };
    }

    private async Task<List<QvinciLocation>> SyncLocationsAsync(IEntityContext context)
    {
        var objectType = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, AccountIds.FCI)
            .Eq(x => x.Name, "QvinciLocation")
            .Eq(x => x.Namespace, null)
            .FirstOrDefaultAsync();

        if (objectType == null) throw new Exception("QvinciLocation Object Type not found for account");

        var locations = await _client.GetLocationsAsync();
        var existing = await _connection.Filter<CustomObject>()
            .Eq(x => x.AccountId, AccountIds.FCI)
            .Eq(x => x.ObjectTypeId, objectType.Id)
            .FindAsync();
        var dict = existing.ToDictionary(x => int.Parse(x.ExternalId));

        var queue = new List<WriteModel<CustomObject>>();
        foreach (var location in locations)
        {
            var add = false;
            if (!dict.TryGetValue(location.Id, out var obj))
            {
                add = true;
                obj = new CustomObject
                {
                    Id = Guid.NewGuid(),
                    CreatedOn = DateTime.UtcNow,
                    LastModifiedOn = DateTime.UtcNow,
                    LastActor = context.Actor(),
                    AccountId = context.AccountId.Value,
                    EntityId = context.AccountId.Value,
                    ObjectType = objectType.Name,
                    ObjectTypeId = objectType.Id,
                    ExternalId = location.Id.ToString(),
                };
            }
            else
            {
                location.EntityId = obj.EntityId;
            }

            obj.Name = location.Name;

            obj.Properties ??= new Dictionary<string, object>();

            obj.Properties[nameof(QvinciLocation.CompanyId)] = location.CompanyId;
            obj.Properties[nameof(QvinciLocation.CreatedAt)] = location.CreatedAt;
            obj.Properties[nameof(QvinciLocation.Type)] = location.Type;
            obj.Properties[nameof(QvinciLocation.FileType)] = location.FileType;
            obj.Properties[nameof(QvinciLocation.Founded)] = location.Founded;
            obj.Properties[nameof(QvinciLocation.NumberEmployees)] = location.NumberEmployees;
            obj.Properties[nameof(QvinciLocation.NAICS)] = location.NAICS;
            // obj.Properties[nameof(QvinciLocation.Url)] = location.Url;

            obj.Properties[nameof(LocationAddress.Address1)] = location.Address?.Address1;
            obj.Properties[nameof(LocationAddress.Address2)] = location.Address?.Address2;
            obj.Properties[nameof(LocationAddress.Country)] = location.Address?.Country;
            obj.Properties[nameof(LocationAddress.State)] = location.Address?.State;
            obj.Properties[nameof(LocationAddress.City)] = location.Address?.City;
            obj.Properties[nameof(LocationAddress.Zip)] = location.Address?.Zip;

            var model = add ?
                new InsertOneModel<CustomObject>(obj) :
                (WriteModel<CustomObject>)_connection.Filter<CustomObject>().Eq(x => x.Id, obj.Id).ReplaceOneModel(obj);

            queue.Add(model);
        }

        if (queue.Count > 0)
        {
            await _connection.BulkWriteAsync(queue);
        }

        return locations;
    }

    private async Task LoadReportAsync(IEntityContext context, QvinciLocation location, QvinciReport report)
    {
        var transactionId = (context.Actor() as JobActor).TransactionId;

        using var scope = _logger.AddScope(new
        {
            Report = report,
            location.Name,
            location.CompanyId,
            LocationId = location.Id,
            TransactionId = transactionId,
        });

        var locationReport = new QvinciLocationReport
        {
            TransactionId = transactionId,
            Id = Guid.NewGuid(),
            LocationId = location.Id,
            Location = location.Name,
            AccountId = AccountIds.FCI,
            EntityId = location.EntityId ?? AccountIds.FCI,
            CreatedOn = DateTime.UtcNow,
            Report = report,
            LastActor = context.Actor(),
            LastModifiedOn = DateTime.UtcNow,
        };

        try
        {
            var gReport = await _client.GetAsync(location, report);
            if (gReport == null)
            {
                _logger.LogInformation("Request didn't return any results");
                locationReport.Error = new ReportError
                {
                    Status = "No Content",
                };
            }
            else
            {
                locationReport.Raw = gReport;
                Process(locationReport);
            }
        }
        catch (HttpClientRequestException ex)
        {
            // Body: "{\"ResponseStatus\":{\"ErrorCode\":\"ReportNotAvailableException\",\"Message\":\"A/R Aging is not available for AccountRight, or QuickBooks Online (By Class).\",\"Errors\":[]}}"
            locationReport.Error = new ReportError
            {
                Status = ex.Status,
                StatusCode = ex.StatusCode,
                Body = ex.Body,
            };
        }

        await _connection.InsertAsync(locationReport);

        switch (locationReport.Report)
        {
            case QvinciReport.PNL_LastYear:
            case QvinciReport.PNL_YTD:
                await SaveAsync<QvinciPNL>(locationReport);
                break;
            case QvinciReport.BalanceSheet_LastYear:
            case QvinciReport.BalanceSheet_YTD:
                await SaveAsync<QvinciBalance>(locationReport);
                break;
        }
    }

    private async Task SaveAsync<T>(QvinciLocationReport locationReport) where T : QvinciRow, new()
    {
        if (locationReport.EntityId == locationReport.AccountId) return;
        if (locationReport.Error != null) return;

        var year = locationReport.Report switch
        {
            QvinciReport.PNL_LastYear => $"{DateTime.UtcNow.Year - 1}",
            QvinciReport.BalanceSheet_LastYear => $"{DateTime.UtcNow.Year - 1}",
            QvinciReport.PNL_YTD => $"{DateTime.UtcNow.Year}",
            QvinciReport.BalanceSheet_YTD => $"{DateTime.UtcNow.Year}",
            _ => null,
        };

        await _connection.Filter<T>()
            .Eq(x => x.AccountId, locationReport.AccountId)
            .Eq(x => x.EntityId, locationReport.EntityId)
            .Eq(x => x.Year, year)
            .DeleteAsync();

        var rows = locationReport.Rows
            .Select(x => new T
            {
                Id = Guid.NewGuid(),
                AccountId = locationReport.AccountId,
                EntityId = locationReport.EntityId,
                CreatedOn = locationReport.CreatedOn,
                Name = x.Name,
                Description = x.Description,
                Code = x.Code,
                RowType = x.RowType,
                Levels = x.Levels,
                Year = year,
                Months = x.Values
            })
            .Select(x => new InsertOneModel<T>(x));

        await _connection.BulkWriteAsync(rows, locationReport.Rows.Count + 1);
    }

    private void Process(QvinciLocationReport report)
    {
        report.Rows = new List<ReportRow>();

        process(report.Raw.ReportModel.TopMostRows, string.Empty, Array.Empty<string>());

        void process(ReportSection[] items, string parentLocator, string[] levels)
        {
            var rowIndex = 0;
            foreach (var item in items)
            {
                var match = expr.Match(item.Name);
                var code = match.Success ? match.Groups[1].Value : item.Name;

                var locator = $"{parentLocator}{(rowIndex++):x3}";
                var row = new ReportRow
                {
                    LineLocator = locator,
                    Name = item.Name,
                    Values = null,
                    Levels = levels,
                    Code = code,
                };

                report.Rows.Add(row);

                if (!item.IsParentAccount)
                {
                    row.RowType = RowType.Data;
                    row.Values = Map(report.Report, item.Values);
                    continue;
                }

                // section
                row.RowType = RowType.Header;
                row.Levels = levels;

                if (item.Children?.Length > 0)
                {
                    var newLevels = levels.Append(item.Name).ToArray();
                    process(item.Children, locator + "-", newLevels);
                }

                if (item.TotalRow != null)
                {
                    report.Rows.Add(new ReportRow
                    {
                        LineLocator = $"{locator}-FFF",
                        Name = item.Name,
                        RowType = RowType.Total,
                        Values = Map(report.Report, item.TotalRow.Values),
                        Levels = levels,
                    });
                }
            }
        }
    }

    private Dictionary<string, decimal> Map(QvinciReport report, ReportValue[] values)
    {
        var result = new Dictionary<string, decimal>();

        foreach (var value in values)
        {
            result.Add(getKey(value.ColumnName), value.Value);
        }

        string getKey(string name)
        {
            return report switch
            {
                QvinciReport.Aging or QvinciReport.AP => name switch
                {
                    "Current" => "Current",
                    "1 - 30" => "Month1",
                    "31 - 60" => "Month2",
                    "61 - 90" => "Month3",
                    "Over 90" => "Over90",
                    "Total" => "Total",
                    _ => throw new Exception($"Invalid Column: {name}")
                },
                _ => DateTime.TryParse(name, out var date) ?
                    date.ToString("MMM") :
                    name,
            };
        }

        return result;
    }
}