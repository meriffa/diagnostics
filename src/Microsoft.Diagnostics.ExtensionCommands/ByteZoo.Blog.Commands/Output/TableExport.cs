// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.ExtensionCommands.Output;

namespace ByteZoo.Blog.Commands.Output;

/// <summary>
/// Table export
/// </summary>
/// <param name="console"></param>
/// <param name="outputType"></param>
/// <param name="columns"></param>
internal sealed class TableExport(IConsoleOrFileLoggingService console, DumpHeapExportService.OutputType outputType, params Column[] columns) : Table(console, columns)
{

    #region Private Members
    private readonly char separator = outputType == DumpHeapExportService.OutputType.CSV ? ',' : '\t';
    private string[] ColumnTitles = [];
    private bool rowWritten;
    #endregion

    #region Public Methods
    /// <summary>
    /// Write table header
    /// </summary>
    /// <param name="values"></param>
    public override void WriteHeader(params string[] values)
    {
        if (outputType == DumpHeapExportService.OutputType.Json)
        {
            ColumnTitles = values;
            Console.Write("[");
        }
        else
        {
            WriteHeaderDelimited(values);
        }
    }

    /// <summary>
    /// Write table row
    /// </summary>
    /// <param name="values"></param>
    public override void WriteRow(params object[] values)
    {
        StringBuilder rowBuilder = _stringBuilderPool.Rent();
        if (outputType == DumpHeapExportService.OutputType.Json)
        {
            if (rowWritten)
            {
                rowBuilder.Append(',');
            }
            WriteRowJson(values, rowBuilder);
            rowWritten = true;
        }
        else
        {
            WriteRowDelimited(values, rowBuilder);
        }
        _stringBuilderPool.Return(rowBuilder);
    }

    /// <summary>
    /// Write table footer
    /// </summary>
    /// <param name="values"></param>
    public override void WriteFooter(params object[] values)
    {
        if (outputType == DumpHeapExportService.OutputType.Json)
        {
            Console.WriteLine("]");
        }
        console.Disable();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Write table header delimiter separated
    /// </summary>
    /// <param name="values"></param>
    private void WriteHeaderDelimited(string[] values)
    {
        StringBuilder rowBuilder = _stringBuilderPool.Rent();
        for (int i = 0; i < values.Length; i++)
        {
            if (i != 0)
            {
                rowBuilder.Append(separator);
            }
            rowBuilder.Append(values[i]);
        }
        Console.WriteLine(rowBuilder.ToString());
        _stringBuilderPool.Return(rowBuilder);
    }

    /// <summary>
    /// Write table row JSON
    /// </summary>
    /// <param name="values"></param>
    /// <param name="rowBuilder"></param>
    private void WriteRowJson(object[] values, StringBuilder rowBuilder)
    {
        rowBuilder.Append('{');
        for (int i = 0; i < values.Length; i++)
        {
            if (i != 0)
            {
                rowBuilder.Append(',');
            }
            rowBuilder.Append('"').Append(ColumnTitles[i]).Append("\":");
            Column column = i < Columns.Length ? Columns[i] : ColumnKind.Text;
            if (column.Format != Formats.IntegerWithoutCommas)
            {
                rowBuilder.Append('"');
            }
            column.Format.FormatValue(rowBuilder, values[i], column.Width, true);
            if (column.Format != Formats.IntegerWithoutCommas)
            {
                rowBuilder.Append('"');
            }
        }
        rowBuilder.Append('}');
        Console.Write(rowBuilder.ToString());
    }

    /// <summary>
    /// Write table row delimiter separated
    /// </summary>
    /// <param name="values"></param>
    /// <param name="rowBuilder"></param>
    private void WriteRowDelimited(object[] values, StringBuilder rowBuilder)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (i != 0)
            {
                rowBuilder.Append(separator);
            }
            Column column = i < Columns.Length ? Columns[i] : ColumnKind.Text;
            column.Format.FormatValue(rowBuilder, values[i], column.Width, true);
        }
        Console.WriteLine(rowBuilder.ToString());
    }
    #endregion

}
