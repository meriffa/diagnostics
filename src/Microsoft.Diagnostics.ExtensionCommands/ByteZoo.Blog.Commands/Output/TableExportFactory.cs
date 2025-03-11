// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ByteZoo.Blog.Commands.Enums;
using ByteZoo.Blog.Commands.Interfaces;
using Microsoft.Diagnostics.ExtensionCommands.Output;

namespace ByteZoo.Blog.Commands.Output;

/// <summary>
/// Export base service
/// </summary>
internal static class TableExportFactory
{

    #region Protected Methods
    /// <summary>
    /// Return table export
    /// </summary>
    /// <param name="columns"></param>
    /// <param name="outputType"></param>
    /// <param name="outputFile"></param>
    /// <param name="consoleOrFileLogging"></param>
    /// <returns></returns>
    internal static Table GetTable(Column[] columns, OutputType outputType, string outputFile, IConsoleOrFileLoggingService consoleOrFileLogging)
    {
        if (outputType != OutputType.Console)
        {
            if (string.IsNullOrEmpty(outputFile))
            {
                return new TableExport(consoleOrFileLogging, outputType, columns);
            }
            else
            {
                consoleOrFileLogging.Enable(outputFile);
                return new TableExport(consoleOrFileLogging, outputType, columns);
            }
        }
        else
        {
            return new Table(consoleOrFileLogging, columns);
        }
    }
    #endregion

}
