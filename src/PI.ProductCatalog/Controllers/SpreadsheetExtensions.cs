using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using PI.Shared;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers
{
    public static class SpreadsheetExtensions
    {
        public static async Task UpdateAsync(
            this Spreadsheet spreadsheet,
            MongoConnection connection,
            IEntityContext context,
            DataViewResponse response,
            ObjectTypeService objectTypeService =null,
            params MenuItem[] items)
        {
            var menuItems = new List<MenuItem>(items);

            var title = new TitleBuilder(spreadsheet.Name)
                .WithoutFileExtension()
                .WithMaxLengthOf(20, true)
                .Build();

            if (spreadsheet.ObjectStatusId.HasValue)
            {
                var objectStatus = await connection.Filter<ObjectStatus>()
                    .Eq(x => x.AccountId, context.AccountId.Value)
                    .Eq(x => x.ObjectType, spreadsheet.ObjectType)
                    .Eq(x => x.Id, spreadsheet.ObjectStatusId.Value)
                    .FirstOrDefaultAsync();

                title += $" ({objectStatus.Description})";

                var formName = $"{objectStatus.Name}";
                if (objectStatus.Id == Spreadsheet.ConvertedStatusId && spreadsheet.ErrorsCount > 0)
                {
                    formName += "|Errors";
                }

                if (response.Request.Skip <= 0)
                {
                    var form = await connection.GetProfileElementAsync<AppForm>(context, $"{nameof(Spreadsheet)}|{formName}");
                    if (form != null) response.NextUrl = $"dataForm://productcatalog/v1/{nameof(Spreadsheet)}({spreadsheet.Id})/{formName}";
                }
            }

            if (spreadsheet.ErrorsCount > 0)
            {
                var error = spreadsheet.ErrorsCount > 1 ? $"{spreadsheet.ErrorsCount} errors" : "1 error";
                title += $" - {error}";

                menuItems.Add(
                    new ActionMenuItem
                    {
                        Name = "Download Failed",
                        Action = $"dataFile://productcatalog/v1/{nameof(Spreadsheet)}({spreadsheet.Id})/Errors"
                    }
                );
            }

            if (objectTypeService != null)
            {
                // load user actions
                var userActions = await objectTypeService.GetUserActionsForObjectAsync(context, spreadsheet, $"productcatalog/v1/Item/Staging({spreadsheet.Id})");
                if (userActions.Count > 0)
                {
                    menuItems.AddRange(userActions);
                }
            }

            response.View.Title = title;

            var iconsCount = menuItems.Count(x => x.Icon!=null);
            var menu = new Menu
            {
                Name = "Menu",
                Items = menuItems.ToArray(),
            };

            if (iconsCount > 3 || menuItems.Count > 2)
            {
                menu.Collapsible = true;
                menu.Icon = nameof(Icons.Action);
                response.View.Menu = new Menu
                {
                    Name = "Actions",
                    Items = 
                    [
                        menu
                    ]
                };
            }
            else
            {
                response.View.Menu = menu;
            }
        }
    }
}