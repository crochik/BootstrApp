using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Mongo;
using PI.Shared.Exceptions;

namespace PI.Salesforce.IIF;

public class QbProposalGenerator
{
    private readonly MongoConnection _connection;

    public QbProposalGenerator(MongoConnection connection)
    {
        _connection = connection;
    }

    public async Task<string> GenerateAsync(string id)
    {
        var list = await _connection.DipperAggregateAsync<Option>("OptionIIF", "fci", new
        {
            OptionId = id
        });

        if (list.Count != 1)
        {
            throw new NotFoundException("Option not found");
        }

        var option = list[0];

        return Generate(option);
    }

    private string Generate(Option option)
    {
        var data = Accounts()
                .Concat(InventoryItemsHeader())
                .Concat(option.GetInventoryItems())
                .Append(CompTaxInventoryItem())
                .Append(CustomerHeader())
                .Append(option.GetCustomer())
                .Concat(Vendors())
                .Concat(TransactionHeaders())
                .Concat(option.GetTransactions())
            ;

        var lines = ToLines(data);

        return string.Join("\r\n", lines);
    }

    private static IEnumerable<string> CompTaxInventoryItem()
    {
        return new[] { "INVITEM", "TAX", "COMPTAX", "Sales Tax Payable", "", "Sales Tax Payable", "", "", "", "", "N" };
    }

    private static IEnumerable<string> CustomerHeader()
    {
        return new[] { "!CUST", "NAME", "BADDR1", "BADDR2", "BADDR3", "BADDR4", "BADDR5", "SADDR1", "SADDR2", "SADDR3", "SADDR4", "SADDR5", "PHONE1", "PHONE2", "FAXNUM", "EMAIL", "NOTE", "CONT1", "CONT2", "CTYPE", "TAXABLE", "FIRSTNAME", "MIDINIT", "LASTNAME", "JOBDESC" };
    }

    private static IEnumerable<IEnumerable<string>> TransactionHeaders()
    {
        yield return new[] { "!TRNS", "TRNSID", "TRNSTYPE", "DATE", "ACCNT", "NAME", "CLASS", "AMOUNT", "DOCNUM", "MEMO", "CLEAR", "TOPRINT", "NAMEISTAXABLE", "ADDR1", "ADDR2", "ADDR3", "SADDR1", "SADDR2", "SADDR3" };
        yield return new[] { "!SPL", "SPLID", "TRNSTYPE", "DATE", "ACCNT", "NAME", "CLASS", "AMOUNT", "DOCNUM", "MEMO", "CLEAR", "QNTY", "PRICE", "INVITEM", "TAXABLE", "EXTRA" };
        yield return new[] { "!ENDTRNS" };
    }

    private static IEnumerable<IEnumerable<string>> Vendors()
    {
        yield return new[] { "!VEND", "NAME", "NOTEPAD" };
        yield return new[] { "VEND", "TAX", "TAX" };
    }

    private static IEnumerable<IEnumerable<string>> Accounts()
    {
        // accounts (fixed)
        yield return new[] { "!ACCNT", "NAME", "ACCNTTYPE", "ACCNUM", "EXTRA" };

        yield return new[] { "ACCNT", "Sales Tax Payable", "OCLIAB", "2150", "SALESTAX" };
        yield return new[] { "ACCNT", "Sales", "INC", "4000", "" };
        yield return new[] { "ACCNT", "Sales:Area Rug Sales", "INC", "4010", "" };
        yield return new[] { "ACCNT", "Sales:Carpet Sales", "INC", "4020", "" };
        yield return new[] { "ACCNT", "Sales:Ceramic and Stone Sales", "INC", "4030", "" };
        yield return new[] { "ACCNT", "Sales:Hardwood Sales", "INC", "4040", "" };
        yield return new[] { "ACCNT", "Sales:Laminate Sales", "INC", "4050", "" };
        yield return new[] { "ACCNT", "Sales:Vinyl Sales", "INC", "4060", "" };
        yield return new[] { "ACCNT", "Sales:Sales - Other", "INC", "4070", "" };
        yield return new[] { "ACCNT", "Sales:Sales Discounts", "INC", "4080", "" };
        yield return new[] { "ACCNT", "Sales:Installation Labor", "INC", "4090", "" };
        yield return new[] { "ACCNT", "Cost of Goods Sold", "COGS", "5000", "" };
        yield return new[] { "ACCNT", "Cost of Goods Sold:Material Costs", "COGS", "5100", "" };
        yield return new[] { "ACCNT", "Cost of Goods Sold:Material Costs:Area Rugs", "COGS", "5110", "" };
        yield return new[] { "ACCNT", "Cost of Goods Sold:Material Costs:Carpet Material", "COGS", "5120", "" };
        yield return new[] { "ACCNT", "Cost of Goods Sold:Material Costs:Ceramic and Stone Material", "COGS", "5130", "" };
        yield return new[] { "ACCNT", "Cost of Goods Sold:Material Costs:Hardwood Material", "COGS", "5140", "" };
        yield return new[] { "ACCNT", "Cost of Goods Sold:Material Costs:Laminate Material", "COGS", "5150", "" };
        yield return new[] { "ACCNT", "Cost of Goods Sold:Material Costs:Vinyl Material", "COGS", "5160", "" };
        yield return new[] { "ACCNT", "Cost of Goods Sold:Material Costs:Material Costs - Other", "COGS", "5170", "" };
        yield return new[] { "ACCNT", "Cost of Goods Sold:Material Costs:Sales Tax on Material", "COGS", "5180", "" };
        yield return new[] { "ACCNT", "Cost of Goods Sold:Material Costs:Early Payment Discount", "COGS", "5190", "" };
        yield return new[] { "ACCNT", "Cost of Goods Sold:Installation Costs", "COGS", "5200", "" };
    }

    private static IEnumerable<IEnumerable<string>> InventoryItemsHeader()
    {
        yield return new[] { "!INVITEM", "NAME", "INVITEMTYPE", "DESC", "PURCHASEDESC", "ACCNT", "ASSETACCNT", "COGSACCNT", "PRICE", "COST", "TAXABLE" };
    }

    private static IEnumerable<string> ToLines(IEnumerable<IEnumerable<string>> tokens)
    {
        return tokens
            .Where(x => x != null)
            .Select(x => string.Join('\t', x));
    }


    /// <summary>
    /// salesforce.Account
    /// </summary>
    public class Account
    {
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Phone2__c { get; set; }
        public string Fax { get; set; }
        public string Main_Email__c { get; set; }
    }

    /// <summary>
    /// salesforce.INET_InternalCustomSettings__c
    /// RecordTypeId = "Settings"
    /// SettingsType = "Product"
    /// </summary>
    public class ProductSetting
    {
        public string Sales_Category__c { get; set; }
        public string Cost_Category__c { get; set; }
    }

    public class Product2
    {
        public string ProductCode { get; set; }
        public string INET_ProductSetting__c { get; set; }
        public bool INET_IsLabor__c { get; set; }
        public string Name { get; set; }

        public ProductSetting INET_ProductSetting__r { get; set; }
    }

    public class LineItem
    {
        public string Product__c { get; set; }
        public decimal? UnitPrice__c { get; set; }
        public decimal? UnitCost__c { get; set; }
        public decimal? TotalPrice__c { get; set; }
        public decimal? TotalBeforeTax__c { get; set; }
        public decimal? AdjQuantity__c { get; set; }
        public decimal? TaxPrice__c { get; set; }

        public Product2 Product__r { get; set; }

        public IEnumerable<string> Tokens()
        {
            var product = Product__r;
            yield return "INVITEM";

            if (product.ProductCode == "PROADJ")
            {
                yield return "[PRICE-ADJUSTMENT]";
            }
            else
            {
                yield return product.ProductCode;
            }

            if (!product.INET_IsLabor__c || product.ProductCode == "PRODISC" || product.ProductCode == "PROADJ")
            {
                yield return "PART";
            }
            else
            {
                yield return "SERV";
            }

            if (product.ProductCode == "PROADJ")
            {
                yield return "[PRICE-ADJUSTMENT]";
            }
            else
            {
                yield return product.Name;
            }

            yield return " ";
            yield return product.INET_ProductSetting__r.Sales_Category__c;

            if (!product.INET_IsLabor__c)
            {
                yield return "Inventory Asset";
            }
            else
            {
                yield return " ";
            }

            yield return product.INET_ProductSetting__r.Cost_Category__c;

            yield return UnitPrice__c.GetValueOrDefault(0).ToString("0.00");
            yield return UnitCost__c.GetValueOrDefault(0).ToString("0.00");

            yield return "Y";
        }
    }

    public class WorkOrder
    {
        public string AccountId { get; set; }
        public string WorkOrderNumber { get; set; }
        public string INET_InspireNetProjectNumber__c { get; set; }
        public string Street { get; set; }
        public string City { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }

        public Account Account { get; set; }

        public string ProjectNumber => !string.IsNullOrEmpty(INET_InspireNetProjectNumber__c) ? INET_InspireNetProjectNumber__c : WorkOrderNumber;
        public string ProjectName => !string.IsNullOrEmpty(Account.Name) ? $"{Account.Name}:Project #{ProjectNumber}" : $"Project #{ProjectNumber}";

        public string StreetAddress => !string.IsNullOrEmpty(Street) ?
            Regex.Replace(Street, @"\r\n?|\n", " ") :
            string.Empty;

        public IEnumerable<string> Tokens()
        {
            var project = this;
            var account = project.Account;
            project.City ??= string.Empty;
            project.PostalCode ??= string.Empty;

            yield return "CUST";
            yield return ProjectName; // NAME

            yield return account?.Name ?? " "; //BADDR1
            yield return project.Street; //BADDR2
            yield return project.City + " " + project.PostalCode; //BADDR3
            yield return project.Country; //BADDR4
            yield return " "; //BADDR5

            yield return account?.Name ?? " "; //SADDR1
            yield return StreetAddress; //SADDR2
            yield return project.City + " " + project.PostalCode; //SADDR3
            yield return project.Country; //SADDR4
            yield return " "; //SADDR5

            if (account != null)
            {
                yield return account.Phone; //PHONE1
                yield return account.Phone2__c; //PHONE2
                yield return account.Fax; //FAXNUM
                yield return account.Main_Email__c; //EMAIL
            }
            else
            {
                yield return " "; //PHONE1
                yield return " "; //PHONE2
                yield return " "; //FAXNUM
                yield return " "; //EMAIL
            }

            yield return ProjectNumber; //NOTE
            yield return account?.Name ?? " "; //CONT1
            yield return " "; //CONT2
            yield return " "; //CTYPE
            yield return "N"; //TAXABLE

            if (account != null)
            {
                var nameParts = account.Name.Split(" ");
                yield return nameParts[0]; //FIRSTNAME
                yield return nameParts.Length > 2 ? nameParts[1] : " "; //MIDINIT
                yield return nameParts.Length > 1 ? nameParts[^1] : " "; //LASTNAME
            }
            else
            {
                yield return " "; //FIRSTNAME
                yield return " "; //MIDINIT
                yield return " "; //LASTNAME
            }

            yield return "Project #" + ProjectNumber; //JOBDESC
        }
    }

    public class Section
    {
        public string Id { get; set; }
        public string ParentOption__c { get; set; }
        public LineItem[] INET_SectionLineItems__r { get; set; }
    }

    public class Totals
    {
        public decimal TotalBeforeTax { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal TaxPrice { get; set; }
        public decimal UnitPrice { get; set; }
        public int Index { get; set; }
    }

    public class AggregatedProduct : Totals
    {
        public Product2 Product { get; set; }

        public decimal AdjQuantity { get; set; }

        public IEnumerable<string> Tokens(DateTime createdDate)
        {
            yield return "SPL";
            yield return Index.ToString(); //SPLID
            yield return "INVOICE"; //TRNSTYPE
            yield return createdDate.ToString("MM/dd/yyyy"); //DATE
            yield return Product.INET_ProductSetting__r.Sales_Category__c; //ACCNT
            yield return " "; //NAME
            yield return " "; //CLASS
            yield return (-1 * TotalBeforeTax).ToString("0.00"); //AMOUNT
            yield return " "; //DOCNUM
            if (Product.ProductCode == "PROADJ")
            {
                //MEMO
                yield return "[PRICE-ADJUSTMENT]";
            }
            else
            {
                yield return Product.Name;
            }

            yield return "N"; //CLEAR

            // 10/23
            // yield return Math.Abs(AdjQuantity).ToString("0.00"); //QNTY
            
            yield return (-1 * AdjQuantity).ToString("0.00"); //QNTY
            yield return UnitPrice.ToString("0.00"); //PRICE
            
            if (Product.ProductCode == "PROADJ")
            {
                // INVITEM
                
                // 10/23 
                // yield return UnitPrice.ToString("0.00"); //PRICE
                
                yield return "[PRICE-ADJUSTMENT]";
            }
            // 10/23 
            // else if (Product.ProductCode == "PRODISC")
            // {
            //     yield return (-1 * UnitPrice).ToString("0.00"); //PRICE
            //     yield return Product.ProductCode;
            // }
            else
            {
                // 10/23 
                // yield return UnitPrice.ToString("0.00"); //PRICE
                
                yield return Product.ProductCode;
            }

            if (TaxPrice != 0)
            {
                yield return "Y"; //TAXABLE
            }
            else
            {
                yield return "N"; //TAXABLE
            }

            yield return " "; //EXTRA
        }
    }

    public class Option
    {
        public string Id { get; set; }
        public string ParentProject__c { get; set; }
        public string Proposal_Number__c { get; set; }
        public DateTime CreatedDate { get; set; }

        public WorkOrder ParentProject__r { get; set; }
        public LineItem[] Option_Line_Items__r { get; set; }
        public Section[] INET_Sections__r { get; set; }

        private LineItem[] _allLineItems = null;
        public LineItem[] AllLineItems => _allLineItems ??= AggregateLineItems().ToArray();

        private AggregatedProduct[] _aggregatedProducts = null;
        public AggregatedProduct[] AggregatedProducts => _aggregatedProducts ??= GetAggregatedProducts().ToArray();

        private IEnumerable<LineItem> AggregateLineItems()
        {
            var lineItems = Option_Line_Items__r ?? Enumerable.Empty<LineItem>();

            if (INET_Sections__r != null)
            {
                foreach (var section in INET_Sections__r)
                {
                    if (section.INET_SectionLineItems__r == null) continue;
                    lineItems = lineItems.Concat(section.INET_SectionLineItems__r);
                }
            }

            return lineItems;
        }

        public IEnumerable<IEnumerable<string>> GetInventoryItems() => AllLineItems.Select(x => x.Tokens());

        public IEnumerable<string> GetCustomer() => ParentProject__r?.Tokens();

        public IEnumerable<AggregatedProduct> GetAggregatedProducts()
        {
            var dict = new Dictionary<string, AggregatedProduct>();
            foreach (var lineItem in AllLineItems)
            {
                if (!dict.TryGetValue(lineItem.Product__c, out var aggregatedProduct))
                {
                    aggregatedProduct = new AggregatedProduct
                    {
                        Product = lineItem.Product__r,
                        UnitPrice = lineItem.UnitPrice__c.GetValueOrDefault(0),
                        Index = dict.Count + 1,
                    };

                    dict.Add(lineItem.Product__c, aggregatedProduct);
                }

                aggregatedProduct.TotalPrice += lineItem.TotalPrice__c.GetValueOrDefault(0);
                aggregatedProduct.TotalBeforeTax += lineItem.TotalBeforeTax__c.GetValueOrDefault(0); // sf rounds each line to 2 decimal places before adding 
                aggregatedProduct.AdjQuantity += lineItem.AdjQuantity__c.GetValueOrDefault(0);
                aggregatedProduct.TaxPrice += lineItem.TaxPrice__c.GetValueOrDefault(0);
            }

            return dict.Values.OrderBy(x => x.Index);
        }

        public IEnumerable<IEnumerable<string>> GetTransactions()
        {
            var totals = AggregatedProducts.Aggregate(new Totals(), (prev, x) =>
            {
                prev.TotalPrice += x.TotalPrice;
                prev.TotalBeforeTax += x.TotalBeforeTax;
                prev.TaxPrice += x.TaxPrice;
                return prev;
            });

            yield return GetTransactionTokens(totals);

            foreach (var product in AggregatedProducts)
            {
                yield return product.Tokens(CreatedDate);
            }

            yield return GetTaxTransaction(totals);

            // body = buildSPLSectionTaxRow(body, option, totalsMap);
            // ...

            yield return new[] { "ENDTRNS" };
        }

        private IEnumerable<string> GetTaxTransaction(Totals totals)
        {
            yield return "SPL";
            yield return "TAX"; //SPLID
            yield return "INVOICE"; //TRNSTYPE
            yield return CreatedDate.ToString("MM/dd/yyyy"); //DATE
            yield return "Sales Tax Payable"; //ACCNT
            yield return "TAX"; //NAME
            yield return " "; //CLASS
            yield return (-1 * totals.TaxPrice).ToString("0.00"); //AMOUNT
            yield return " "; //DOCNUM
            yield return "TAX"; //MEMO
            yield return "N"; //CLEAR
            yield return " "; //QNTY
            if (totals.TaxPrice == 0 || totals.TotalBeforeTax == 0)
            {
                //PRICE
                yield return "0%";
            }
            else
            {
                var taxPercentage = 100 * totals.TaxPrice / totals.TotalBeforeTax;
                yield return $"{taxPercentage:0.00}%";
            }

            yield return "TAX"; //INVITEM
            yield return "N"; //TAXABLE
            yield return "AUTOSTAX"; //EXTRA
        }

        private IEnumerable<string> GetTransactionTokens(Totals totals)
        {
            var total = decimal.Round(totals.TotalBeforeTax, 2) + totals.TaxPrice;

            yield return "TRNS";
            yield return Proposal_Number__c;
            yield return "INVOICE";
            yield return DateTime.UtcNow.ToString("MM/dd/yyyy");
            yield return "Accounts Receivable";
            yield return ParentProject__r.ProjectName;
            yield return " ";
            yield return total.ToString("0.00");
            yield return Proposal_Number__c;
            yield return " ";
            yield return "N";
            yield return "Y";
            yield return "Y";
            yield return ParentProject__r.StreetAddress;
            yield return ParentProject__r.City + " " + ParentProject__r.PostalCode;
            yield return ParentProject__r.Country;
            yield return ParentProject__r.StreetAddress;
            yield return ParentProject__r.City + " " + ParentProject__r.PostalCode;
            yield return ParentProject__r.Country;
        }
    }
}