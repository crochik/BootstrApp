using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace PI.Shared.Middleware;

public class CsvOutputFormatter : TextOutputFormatter
{
    public CsvOutputFormatter()
    {
        SupportedMediaTypes.Add(Microsoft.Net.Http.Headers.MediaTypeHeaderValue.Parse("text/csv"));
        SupportedEncodings.Add(Encoding.UTF8);
        SupportedEncodings.Add(Encoding.Unicode);
    }

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        var csv = default(string);

        if (context.Object is IDataViewResponse dvr)
        {
            csv = await GenerateCsvAsync(dvr, selectedEncoding);

            context.HttpContext.Response.Headers.Add("Access-Control-Expose-Headers", "Content-Disposition");
            context.HttpContext.Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{dvr.View.Name}.csv\""); // force download                
        }
        else if (context.Object is IEnumerable en)
        {
            using var memStream = new MemoryStream();
            using var writer = new StreamWriter(memStream);
            using (var csvWriter = new CsvWriter(writer, true))
            {
                csvWriter.WriteRecords(en);
            }

            await writer.FlushAsync();
            csv = Encoding.UTF8.GetString(memStream.GetBuffer(), 0, (int)memStream.Length);
            context.HttpContext.Response.Headers.Add("Access-Control-Expose-Headers", "Content-Disposition");
            context.HttpContext.Response.Headers.Add("Content-Disposition", $"attachment; filename=\"export.csv\""); // force download                
        }
        else
        {
            csv = "Error: Unrecognized Object";
        }

        await context.HttpContext.Response.WriteAsync(csv, selectedEncoding);
    }

    private async Task<string> GenerateCsvAsync(IDataViewResponse dvr, Encoding selectedEncoding)
    {
        var fields = dvr.View.Fields
            .Where(x=>x switch
            {
                IComplexFieldValue => false,
                _ => true,
            })
            .Where(x => x.IsVisible);
        var fieldNames = getFieldNames().ToArray();

        using var memStream = new MemoryStream();
        using var writer = new StreamWriter(memStream, selectedEncoding);
        using (var csvWriter = new CsvWriter(writer, true))
        {
            foreach (var col in getColumnLabels())
            {
                csvWriter.WriteField(col);
            }

            csvWriter.NextRecord();

            foreach (var record in dvr.Result)
            {
                if (!(record is IDictionary<string, object> dict))
                {
                    dict = record.GetType().GetProperties().ToDictionary
                    (
                        propInfo => propInfo.Name,
                        propInfo => propInfo.GetValue(record, null),
                        StringComparer.OrdinalIgnoreCase
                    );
                }

                for (var c = 0; c < fieldNames.Length; c++)
                {
                    var v = dict.TryGetValue(fieldNames[c], out var value) ? value : null;
                    csvWriter.WriteField(v);
                }
                csvWriter.NextRecord();
            }

            await csvWriter.FlushAsync();
        }

        await writer.FlushAsync();

        return Encoding.UTF8.GetString(memStream.GetBuffer(), 0, (int)memStream.Length);

        IEnumerable<string> getFieldNames()
        {
            foreach (var field in fields)
            {
                yield return field.Name;

                if (field is ReferenceField)
                {
                    yield return $"{field.Name}|Name";
                }
            }
        }

        IEnumerable<string> getColumnLabels()
        {
            foreach (var field in fields)
            {
                yield return field.Label ?? field.Name;

                if (field is ReferenceField referenceField && referenceField.Options is ReferenceFieldOptions options)
                {
                    yield return $"{field.Label ?? field.Name} ({options.ObjectType}.Name)";
                }
            }
        }
    }

    protected override bool CanWriteType(Type type)
    {
        return typeof(IDataViewResponse).IsAssignableFrom(type) || typeof(IEnumerable).IsAssignableFrom(type);
    }
}