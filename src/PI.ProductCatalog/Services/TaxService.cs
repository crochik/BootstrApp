using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Models;
using PI.Shared.Extensions;
using PI.Shared.Models;
using ZipTax.Clients;
using ZipTax.Models;

namespace PI.ProductCatalog.Services;

public class TaxService(ILogger<TaxService> logger, MongoConnection connection, IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    private HttpClient Client => httpClientFactory.CreateClient("ZipTax");
    private readonly Config _config = configuration.GetSection("ZipTax").Get<Config>();

    public async Task<Result<TaxRates>> ResolveForProjectAsync(IEntityContext context, string projectExternalId)
    {
        var project = await connection.Filter<SfProjectObject>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            // .Eq(x=>x.EntityId, context.OrganizationId.Value)
            .Eq(x => x.ExternalId, projectExternalId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        return await ResolveForProjectAsync(context, project);
    }

    public async Task<Result<TaxRates>> ResolveForProjectAsync(IEntityContext context, SfProjectObject project)
    {
        if (project == null) return Result.Error<TaxRates>("Project not found");

        if (project.TaxRates != null) return Result.Success(project.TaxRates);
        if (string.IsNullOrEmpty(project.Properties.PostalCode) || string.IsNullOrEmpty(project.Properties.City) || string.IsNullOrEmpty(project.Properties.State))
        {
            // TODO: fallback to customer/lead
            // ...
            return Result.Error<TaxRates>("Missing address for project, can't calculate tax rates");
        }

        var result = await CalculateTaxLiability(project.Properties.PostalCode, project.Properties.City, project.Properties.State, address: project.Properties.Street, project.Properties.Country);
        if (result.IsSuccess)
        {
            // update project
            project = await connection.Filter<SfProjectObject>()
                .Eq(x => x.AccountId, project.AccountId)
                .Eq(x => x.Id, project.Id)
                .Eq(x => x.TaxRates, null)
                .Update
                .Set(x => x.TaxRates, result.Value)
                .Set(x => x.LastActor, context.Actor())
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .UpdateAndGetOneAsync();

            if (project != null)
            {
                // TODO: fire update event?
                // ...
            }
        }

        return result;
    }

    public async Task<Result<TaxRates>> CalculateTaxLiability(string postalCode, string city, string state, string address, string country)
    {
        var api = new TaxRatesClient(Client);
        
        var fullAddress = address;
        if (fullAddress != null)
        {
            if (city != null) fullAddress += $", {city}";
            if (state != null) fullAddress += $", {state}";
            if (postalCode != null) fullAddress += $", {postalCode}";
            if (country != null) fullAddress += $", {country}";
        }
        
        try
        {
            var isCanadian = country == "Canada" || (postalCode?.Length > 0 && !(postalCode[0] >= '0' && postalCode[0] <= '9'));
            var response = await api.GetTaxRatesV60Async(key: _config.ApiKey, address: fullAddress, countryCode: isCanadian ? GetTaxRatesV60CountryCode.Can :  GetTaxRatesV60CountryCode.Usa);

            var result = new List<TaxLiability>();

            foreach (var rate in response.BaseRates)
            {
                if (rate.Rate <= 0) continue;

                var liability = new TaxLiability
                {
                    Category = rate.JurType switch
                    {
                        "US_STATE_SALES_TAX" => TaxCategory.Sales,
                        "US_STATE_USE_TAX" => TaxCategory.Use,
                        "US_COUNTY_SALES_TAX" => TaxCategory.Sales,
                        "US_COUNTY_USE_TAX" => TaxCategory.Use,
                        "US_CITY_SALES_TAX" => TaxCategory.Sales,
                        "US_CITY_USE_TAX" => TaxCategory.Use,
                        "US_DISTRICT_SALES_TAX" => TaxCategory.Sales,
                        "US_DISTRICT_USE_TAX" => TaxCategory.Use,
                        _ => TaxCategory.Sales, // ?????
                    },
                    Amount = (decimal)rate.Rate,
                    Name = rate.JurName,
                    Description = $"{rate.JurDescription}: {rate.JurName} ({rate.JurTaxCode})",
                };

                result.Add(liability);

                if (liability.Category != TaxCategory.Sales) continue;

                // service
                if (response.Service?.Taxable != ServiceV60Taxable.N)
                {
                    result.Add(new TaxLiability
                    {
                        Category = TaxCategory.Service,
                        Amount = liability.Amount,
                        Name = rate.JurName,
                        Description = $"Service: {rate.JurName} ({rate.JurTaxCode})",
                    });
                }
                
                // freight
                if (response.Shipping?.Taxable != ShippingV60Taxable.N)
                {
                    result.Add(new TaxLiability
                    {
                        Category = TaxCategory.Freight,
                        Amount = liability.Amount,
                        Name = rate.JurName,
                        Description = $"Freight: {rate.JurName} ({rate.JurTaxCode})",
                    });
                }
            }
            
            var taxRates = new TaxRates
            {
                Id = Guid.NewGuid(),
                CreatedOn = DateTime.UtcNow,
                TaxLiabilities = result.ToArray(),
                PostalCode = postalCode,
                City = city,
                State = state,
                Destination = response.SourcingRules?.Value == OriginDestinationV60Value.D, // ???? 
            };

            return Result.Success(taxRates);
        }
        catch (RequestException ex)
        {
            return Result.Error<TaxRates>(ex.Message);
        }
    }

    public class Config
    {
        public string ApiKey { get; set; }
    }
}