using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace PI.Shared.Services;

public interface ISpreadsheetFileParser : IDisposable
{
    string[] ColumnNames { get; }
    Dictionary<string, int> Columns { get; }
    ValueTask<ChannelReader<object[]>> GetRowsAsync(int[] columns = null);
}

public static class SpreadsheetFileParser
{
    public static ISpreadsheetFileParser Create(IFormFile file)
    {
        return file.ContentType switch
        {
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => new ExcelSpreadsheetFileParser(file.OpenReadStream()),
            "application/vnd.ms-excel" when file.FileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) => new ExcelSpreadsheetFileParser(file.OpenReadStream()),
            "application/vnd.ms-excel" => new CSVFileParser(file.OpenReadStream()),
            "text/csv" => new CSVFileParser(file.OpenReadStream()),
            _ => null,
        };
    }
    
    public static ISpreadsheetFileParser Create(string contentType, string filename, Stream stream)
    {
        return contentType switch
        {
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => new ExcelSpreadsheetFileParser(stream),
            "application/vnd.ms-excel" when filename.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) => new ExcelSpreadsheetFileParser(stream),
            "application/vnd.ms-excel" => new CSVFileParser(stream),
            "text/csv" => new CSVFileParser(stream),
            _ => null,
        };
    }
}