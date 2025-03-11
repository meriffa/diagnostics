// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ByteZoo.Blog.Commands.Enums;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.ExtensionCommands;

namespace ByteZoo.Blog.Commands;

/// <summary>
/// Export base command
/// </summary>
public abstract class ExportCommandBase : ClrRuntimeCommandBase
{

    #region Options
    [Option(Name = "-outputType", Aliases = ["-outputtype", "-t"], Help = "Output type (Console, CSV (Comma Delimited), Tab (Tab Delimited), JSON, default = Console).")]
    public string OutputType { get; set; } = "Console";

    [Option(Name = "-outputFile", Aliases = ["-outputfile", "-o"], Help = "Output file.")]
    public string OutputFile { get; set; }
    #endregion

    #region Protected Methods
    /// <summary>
    /// Return output type
    /// </summary>
    /// <returns></returns>
    protected OutputType GetOutputType() => Enum.TryParse(OutputType, true, out OutputType value) ? value : throw new ArgumentException($"Invalid output type '{OutputType}' specified.");

    /// <summary>
    /// Return export options help
    /// </summary>
    /// <returns></returns>
    protected static string GetExportOptions() => """
    -outputType, -t             Output type (Console, CSV (Comma Delimited), Tab (Tab Delimited), JSON, default = Console).
    -outputFile, -o             Output file. If not specified, uses the console as output.
    """;
    #endregion

}
