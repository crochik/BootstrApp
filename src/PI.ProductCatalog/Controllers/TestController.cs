using System;
using System.Threading.Tasks;
// using Crochik.FTP;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Models;
using PI.Shared.Controllers;
using System.Collections.Generic;
using MongoDB.Driver;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using PI.Shared.Models;
using PI.Shared.Exceptions;

namespace Controllers
{
    public class Test
    {
        public string CamelCase { get; set; }
        public Dictionary<string, string> Dict { get; set; }
    }

    [Route("[controller]")]
    public class TestController : APIController
    {
        private readonly ILogger<TestController> _logger;

        public TestController(ILogger<TestController> logger)
        {
            this._logger = logger;
        }

        [Authorize("admin")]
        [HttpGet("RecalculatePrices")]
        public async Task<IActionResult> RecalculatePricesAsync([FromServices] MongoConnection connection)
        {
            using var cursor = connection.Filter<CatalogItem>()
                // .Eq(x => x.CatalogFeedId, catalogFeedId)
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Exists("Costs.0", true)
                .WithBatchSize(100)
                .Skip(284400)
                .ToCursor();

            var total = 0;
            while (await cursor.MoveNextAsync())
            {
                var list = new List<UpdateOneModel<CatalogItem>>();
                foreach (var item in cursor.Current)
                {
                    var model = connection.Filter<CatalogItem>()
                        .Eq(x => x.Id, item.Id)
                        .Update
                        .SetOrUnset(x => x.CutCost, item.CutCost)
                        // .SetOrUnset(x => x.CutPrice, item.CutPrice)
                        .SetOrUnset(x => x.StandardCost, item.StandardCost)
                        // .SetOrUnset(x => x.StandardPrice, item.StandardPrice)
                        .SetOrUnset(x => x.PalletCost, item.PalletCost)
                        // .SetOrUnset(x => x.PalletPrice, item.PalletPrice)
                        .SetOrUnset(x => x.RollLength, item.RollLength)
                        .SetOrUnset(x => x.RollWidth, item.RollWidth)
                        .SetOrUnset(x => x.Package, item.Package)
                        .SetOrUnset(x => x.Pallet, item.Pallet)
                        .SetOrUnset(x => x.PackagesPerPallet, item.PackagesPerPallet)
                        .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                        .UpdateOneModel();

                    list.Add(model);
                }

                if (list.Count > 0)
                {
                    var result = await connection.BulkWriteAsync(list);
                    total += list.Count;
                    _logger.LogInformation($"Wrote {list.Count}: {result.MatchedCount} / {result.ModifiedCount} = {total}");
                }
            }

            return Ok(total);
        }

        [Authorize("admin")]
        [HttpGet("MaterialType")]
        public async Task<IActionResult> ImportMaterialType([FromServices] MongoConnection connection)
        {
            var objectType = await connection.Filter<ObjectType>()
                .Eq(x => x.AccountId, Context.AccountId)
                .Eq(x => x.Name, nameof(MaterialType))
                .Eq(x => x.Namespace, null)
                .FirstOrDefaultAsync();

            if (objectType == null) throw new NotFoundException(nameof(MaterialType));

            var records = typeof(MaterialType)
                .GetFields()
                .Where(x => x.Name != "value__")
                .Select(x => new CustomObject
                {
                    Id = Model.NewGuid(),
                    AccountId = Context.AccountId.Value,
                    EntityId = Context.AccountId.Value,
                    ObjectType = nameof(MaterialType),
                    ObjectTypeId = objectType.Id,
                    ExternalId = x.Name,
                    Name = x.GetCustomAttribute<DescriptionAttribute>()?.Description ?? x.Name,
                    Description = x.GetCustomAttribute<DescriptionAttribute>()?.Description,
                });

            await connection.InsertAsync(records);

            return Ok(records);
        }

        [Authorize("admin")]
        [HttpGet("PlMaterialTypeLookup")]
        public async Task<IActionResult> ImportPlMaterialTypeLookup([FromServices] MongoConnection connection)
        {
            var objectType = await connection.Filter<ObjectType>()
                .Eq(x => x.AccountId, Context.AccountId)
                .Eq(x => x.Name, "PlMaterialTypeLookup")
                .Eq(x => x.Namespace, null)
                .FirstOrDefaultAsync();

            if (objectType == null) throw new NotFoundException("PlMaterialTypeLookup");

            var list = new List<CustomObject>();
            foreach (var fi in typeof(MaterialType).GetFields().Where(x => x.Name != "value__"))
            {
                var materialType = (MaterialType)fi.GetValue(typeof(MaterialType));
                var attrib = fi.GetCustomAttribute<DescriptionAttribute>();
                var subTypes = createObjects(MaterialType.Unclassified, materialType, attrib)
                    .Concat(createObjects(materialType, materialType, attrib));

                list.AddRange(subTypes);
            }

            await connection.InsertAsync((IEnumerable<CustomObject>)list);

            return Ok(list);

            IEnumerable<CustomObject> createObjects(MaterialType src, MaterialType dst, DescriptionAttribute attrib)
            {
                if (dst == MaterialType.Unclassified) return Enumerable.Empty<CustomObject>();

                return src.GetSubTypes()?
                    .Where(x => x.Name != "value__")
                    .Select(x =>
                    {
                        var obj = new CustomObject
                        {
                            Id = Model.NewGuid(),
                            AccountId = Context.AccountId.Value,
                            EntityId = Context.AccountId.Value,
                            ObjectType = objectType.Name,
                            ObjectTypeId = objectType.Id,
                            ExternalId = $"{dst}:{x.Name}",
                            Name = $"{attrib?.Description ?? dst.ToString()} \\ {x.Attrib?.Description ?? x.Name}",
                            IsActive = false,
                        };

                        obj.Properties = new Dictionary<string, object>
                        {
                            { nameof(MaterialType), dst.ToString() },
                            { nameof(MaterialSubType), x.Name }
                        };

                        return obj;
                    }) ?? Enumerable.Empty<CustomObject>();
            }
        }

        [Authorize("admin")]
        [HttpGet("MaterialSubType")]
        public async Task<IActionResult> ImportMaterialSubType([FromServices] MongoConnection connection)
        {
            var objectType = await connection.Filter<ObjectType>()
                .Eq(x => x.AccountId, Context.AccountId)
                .Eq(x => x.Name, nameof(MaterialSubType))
                .Eq(x => x.Namespace, null)
                .FirstOrDefaultAsync();

            if (objectType == null) throw new NotFoundException(nameof(MaterialSubType));

            var records = typeof(MaterialSubType)
                .GetFields()
                .Where(x => x.Name != "value__")
                .Select(x =>
                {
                    var attrib = x.GetCustomAttribute<MaterialTypeAttribute>();

                    var obj = new CustomObject
                    {
                        Id = Model.NewGuid(),
                        AccountId = Context.AccountId.Value,
                        EntityId = Context.AccountId.Value,
                        ObjectType = nameof(PI.ProductCatalog.Models.MaterialSubType),
                        ObjectTypeId = objectType.Id,
                        ExternalId = x.Name,
                        Name = attrib?.Description ?? x.Name,
                        Description = attrib?.Description,
                    };

                    if (attrib != null)
                    {
                        obj.Properties = new Dictionary<string, object>
                        {
                            { nameof(MaterialType), attrib.Material.ToString() }
                        };
                    }

                    return obj;
                });

            await connection.InsertAsync(records);

            return Ok(records);
        }

        [AllowAnonymous]
        [HttpGet("Test")]
        public IActionResult TestCase()
        {
            var a = new Test
            {
                CamelCase = "camelCaseProperty",
                Dict = new Dictionary<string, string>
                {
                    { "Apple", "banana" },
                    { "banana", "Apple" }
                }
            };

            return Ok(a);
        }

        // [AllowAnonymous]
        // [HttpGet("Test")]
        // public async Task<IActionResult> UploadFileAsync([FromServices] IFileStorageService service)
        // {
        //     await service.UploadAsync(
        //         inputPath: @"C:\DEVELOPMENT\github\SchedOnl\PI.ProductCatalog\00000000-0e60-7c2d-5667-c1d6cc8d1691\631133_832_738188.832",
        //         contentType: "plain/text",
        //         bucket: "pi-productcatalog-staging",
        //         path: "631133_832_738188.832"
        //     );

        //     return Ok();
        // }

        //         [Authorize("admin")]
        //         [HttpGet("Emser")]
        //         public async Task<IActionResult> ImportEmserAsync([FromServices] CatalogParser parser)
        //         {
        //             // https://support.qfloors.com/index.php/b2b-table/b2b-chart
        //             await parser.ParseAsync(Context, @"./emser.edi");

        //             return Ok();
        //         }

        //         [Authorize("admin")]
        //         [HttpGet("Shaw")]
        //         public async Task<IActionResult> ImportShawAsync([FromServices] CatalogParser parser)
        //         {
        //             // https://support.qfloors.com/index.php/b2b-table/b2b-chart
        //             await parser.ParseAsync(Context, @"./000000686.832.edi");

        //             return Ok();
        //         }

        //         [Authorize("admin")]
        //         [HttpGet("Daltile")]
        //         public async Task<IActionResult> ImportDaltileAsync([FromServices] CatalogParser parser)
        //         {
        //             // https://support.qfloors.com/index.php/b2b-table/b2b-chart
        //             await parser.ParseAsync(Context, @"./000307502.832.edi"); //000294111.832.edi

        //             return Ok();
        //         }

        //         [Authorize("admin")]
        //         [HttpGet("Nourison")]
        //         public async Task<IActionResult> ImportNourisonAsync([FromServices] CatalogParser parser)
        //         {
        //             // https://support.qfloors.com/index.php/b2b-table/b2b-chart
        //             await parser.ParseAsync(Context, @"./3580052121.832.edi"); //000294111.832.edi

        //             return Ok();
        //         }

        //         [Authorize("admin")]
        //         [HttpGet("Nourison/Download")]
        //         public async Task<IActionResult> DownloadNourison([FromServices] CatalogParser parser)
        //         {
        //             /* 
        //             var settings = new FtpSettings
        //             {
        //                 Host = "b2b.nourison.net",
        //                 User = "102358",
        //                 Password = "102358FCI?",
        //                 KeepAlive = true,
        //             };

        //             var client = new FtpClient(settings);
        //             var files = await client.ListAsync("OUTBOX");
        //             foreach (var filename in files)
        //             {
        //                 await client.DownloadAsync($"./{filename}", "OUTBOX", filename);
        //             }
        //             */

        //             var client = new FtpClient("ftp://b2b.nourison.net");
        //             client.Credentials = new System.Net.NetworkCredential("102358", "102358FCI?");
        //             await client.ConnectAsync();
        //             var files = await client.GetListingAsync("OUTBOX");

        //             foreach (var item in files)
        //             {
        //                 if (item.Type == FtpFileSystemObjectType.File)
        //                 {
        //                     var localFileStream = new FileStream($"./{item.Name}", FileMode.CreateNew);
        //                     await client.DownloadAsync(localFileStream, item.FullName);
        //                     localFileStream.Close();
        //                 }
        //             }

        //             return Ok(files);
        //         }
    }
}