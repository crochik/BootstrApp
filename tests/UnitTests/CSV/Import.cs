using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CsvHelper;
using FluentAssertions;
using PI.ProductCatalog.Services;
using PI.Shared.Services;
using Xunit;

namespace UnitTests
{
    public class Import
    {
        protected string GetFilePath(string fileName)
        {
            var workingDirectory = Directory.GetCurrentDirectory();
            return Path.Combine(workingDirectory, "..", "..", "..", "CSV", fileName);
        }    

        [Fact]
        public void Read()
        {
            var records = new List<object>();
            using (var reader = new StreamReader(GetFilePath("zipcode.csv")))
            {
                using (var csv = new CsvReader(reader, false))
                {
                    csv.Read();
                    csv.ReadHeader();

                    // csv.Parser.Context.HeaderRecord
                    while (csv.Read())
                    {
                        var record = new {
                            EntityExternalId = csv.GetField<string>("Entity:InspireNet"),
                            ExternalId = csv.GetField<string>("ExternalId"),
                            Name = csv.GetField<string>("Name"),
                        };

                        records.Add(record);
                    }
                }
            }
            
            records.Count.Should().Be(6605);
        }

        [Fact]
        public async Task ReadExcelFile()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var stream = File.OpenRead(GetFilePath("mal.xlsx"));
            using var reader = new ExcelSpreadsheetFileParser(stream);
            var rows = await reader.GetRowsAsync();
            await foreach (var record in rows.ReadAllAsync())
            {
                System.Console.WriteLine(record[0]);
            }

            true.Should().BeTrue();
        }
    }
}