using System;
using System.Linq;
using System.Collections.Generic;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.ProductCatalog.Models;
using PI.Shared.Controllers;
using PI.Shared.Models;
using System.ComponentModel;
using System.Reflection;
using PI.Shared.Models.Expressions;
using PI.Shared.Exceptions;
using Crochik.Extensions;

namespace Controllers
{
    [Route("/productcatalog/v1/System")]
    public partial class SystemLookupController : APIController
    {
        private readonly MongoConnection _connection;

        public SystemLookupController(
            MongoConnection connection
            )
        {
            this._connection = connection;
        }

        // [Authorize("default")]
        [AllowAnonymous]
        [HttpPost("MaterialType/Lookup")]
        public IEnumerable<ReferenceValue> MaterialTypeLookup(DataViewRequest request)
        {
            if (request.Criteria.TryGetEqCondition(Condition.LookupId, out var idCond))
            {
                // hack for first load
                return new ReferenceValue
                {
                    Id = idCond.Value.ToString(),
                    Value = idCond.Value.ToString(),
                }.AsEnumerable();
            }

            var a = typeof(MaterialType)
                .GetFields()
                .Where(x => x.Name != "value__")
                .Select(x => new ReferenceValue
                {
                    Id = x.Name,
                    Value = x.GetCustomAttribute<DescriptionAttribute>()?.Description ?? x.Name,
                })
                .OrderBy(x => x.Value);

            return a;
        }

        [AllowAnonymous]
        [HttpPost("UOM/Lookup")]
        public IEnumerable<ReferenceValue> UOMLookup(DataViewRequest request)
        {
            if (request.Criteria.TryGetEqCondition(Condition.LookupId, out var idCond))
            {
                // hack for first load
                return new ReferenceValue
                {
                    Id = idCond.Value.ToString(),
                    Value = idCond.Value.ToString(),
                }.AsEnumerable();
            }

            var a = typeof(UnitOfMeasurement)
                .GetFields()
                .Where(x => x.Name != "value__")
                .Select(x => new ReferenceValue
                {
                    Id = x.Name,
                    Value = x.GetCustomAttribute<DescriptionAttribute>()?.Description ?? x.Name,
                })
                .OrderBy(x => x.Value);

            return a;
        }

        [AllowAnonymous]
        [HttpPost("MaterialSubType/Lookup")]
        public IEnumerable<ReferenceValue> MaterialSubTypeLookup(DataViewRequest request)
        {
            if (request.Criteria.TryGetEqCondition(Condition.LookupId, out var idCond))
            {
                if (Enum.TryParse<MaterialSubType>(idCond.Value.ToString(), out var subType))
                {
                    // hack for first load
                    return new ReferenceValue
                    {
                        Id = idCond.Value.ToString(),
                        Value = subType.GetDescription(),
                    }.AsEnumerable();
                }

                throw new NotFoundException(nameof(MaterialSubType));
            }

            var materialType = default(MaterialType?);
            if (request.Criteria.TryGetEqCondition(nameof(MaterialType), out var condition) &&
                Enum.TryParse<MaterialType>(condition.Value as string, true, out var filter))
            {
                materialType = filter;
            }

            IEnumerable<ReferenceValue> result = !materialType.HasValue ? null :
                materialType.Value.GetSubTypes()?
                    .Select(x => new ReferenceValue
                    {
                        Id = x.Name,
                        Value = x.Attrib?.Description ?? x.Name,
                    })
                    .OrderBy(x => x.Value);

            result = (result ?? Enumerable.Empty<ReferenceValue>())
                .Concat(MaterialType.Unclassified.GetSubTypes()
                .Select(x => new ReferenceValue
                {
                    Id = x.Name,
                    Value = x.Attrib?.Description ?? x.Name,
                })
                .OrderBy(x => x.Value)
            );

            return result;
        }

    }

    public static class ArrayExtensions
    {
        public static IEnumerable<T> Enumerate<T>(this Array array)
        {
            foreach (var i in array)
            {
                if (i is T t) yield return t;
            }
        }
    }
}
