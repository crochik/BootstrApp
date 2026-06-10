using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using ExcelDataReader;

namespace PI.Shared.Services;

public class ExcelSpreadsheetFileParser : ISpreadsheetFileParser
{
    private readonly Stream _stream;
    private readonly IExcelDataReader _reader;

    public string[] ColumnNames { get; private set; }

    public Dictionary<string, int> Columns { get; } = new Dictionary<string, int>();

    public ExcelSpreadsheetFileParser(Stream stream)
    {
        if (!stream.CanSeek)
        {
            // hack
            _stream = new MemoryStream();
            stream.CopyTo(_stream);
        }
        else
        {
            _stream = stream;    
        }

        _reader = ExcelReaderFactory.CreateReader(_stream);

        if (!_reader.Read())
        {
            throw new Exception("Couldn't load header");
        }

        ColumnNames = new string[_reader.FieldCount];
        for (var i = 0; i < ColumnNames.Length; i++)
        {
            ColumnNames[i] = _reader.GetString(i);
            Columns.TryAdd(ColumnNames[i], i);
        }
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
            do
            {
                while (_reader.Read())
                {
                    if (columns == null)
                    {
                        var record = new object[_reader.FieldCount];
                        for (var i = 0; i < ColumnNames.Length; i++)
                        {
                            record[i] = _reader.GetValue(i);
                        }

                        await channel.WriteAsync(record);
                    }
                    else
                    {
                        // specific/sorted columns
                        var record = new object[columns.Length];
                        for (var i = 0; i < columns.Length; i++)
                        {
                            record[i] = _reader.GetValue(columns[i]);
                        }

                        await channel.WriteAsync(record);
                    }
                }
            } while (_reader.NextResult());
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

    public void Dispose()
    {
        _reader?.Dispose();
        _stream?.Dispose();
    }
}