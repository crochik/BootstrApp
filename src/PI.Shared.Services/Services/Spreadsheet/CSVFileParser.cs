using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using CsvHelper;
using PI.Shared.Exceptions;

namespace PI.Shared.Services;

public class CSVFileParser : ISpreadsheetFileParser
{
    public string[] ColumnNames { get; }
    public Dictionary<string, int> Columns { get; } = new Dictionary<string, int>();
    private readonly Stream _stream;
    private readonly StreamReader _reader;
    private readonly CsvReader _csv;

    public CSVFileParser(Stream stream)
    {
        _stream = stream;

        _reader = new StreamReader(_stream);
        _csv = new CsvReader(_reader, false);

        if (!_csv.Read() || !_csv.ReadHeader())
        {
            throw new BadRequestException("Failed to open/read header");
        }

        ColumnNames = _csv.Parser.Context.HeaderRecord;
        for (var i = 0; i < ColumnNames.Length; i++)
        {
            Columns.TryAdd(ColumnNames[i], i);
        }
    }

    public void Dispose()
    {
        _csv?.Dispose();
        _reader?.Dispose();
    }

    public ValueTask<ChannelReader<object[]>> GetRowsAsync(int[] columns = null)
    {
        var channel = Channel.CreateBounded<object[]>(100);

        var _ = Task.Run(() => WriteAsync(columns, channel.Writer));

        return new ValueTask<ChannelReader<object[]>>(channel.Reader);
    }

    private async Task WriteAsync(int[] columns, ChannelWriter<object[]> channel)
    {
        Exception error = null;

        try
        {
            while (_csv.Read())
            {
                if (columns == null)
                {
                    // all columns
                    var record = new object[ColumnNames.Length];
                    for (var i = 0; i < ColumnNames.Length; i++)
                    {
                        record[i] = _csv.GetField(i);
                    }

                    await channel.WriteAsync(record);
                }
                else
                {
                    // specific/sorted columns
                    var record = new object[columns.Length];
                    for (var i = 0; i < columns.Length; i++)
                    {
                        record[i] = _csv.GetField(columns[i]);
                    }

                    await channel.WriteAsync(record);
                }
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            channel.Complete(error);
        }
    }
}