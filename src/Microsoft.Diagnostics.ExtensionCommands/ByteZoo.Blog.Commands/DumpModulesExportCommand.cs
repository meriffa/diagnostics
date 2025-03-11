// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using ByteZoo.Blog.Commands.Interfaces;
using ByteZoo.Blog.Commands.Output;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.ExtensionCommands.Output;
using Microsoft.Diagnostics.Runtime;

namespace ByteZoo.Blog.Commands;

/// <summary>
/// Export managed modules command
/// </summary>
[Command(Name = "dumpmodulesexport", Aliases = ["DumpModulesExport", "dme"], Help = "Export managed modules.")]
public class DumpModulesExportCommand : ExportCommandBase
{

    #region Options
    [Option(Name = "-name", Help = "Module name.")]
    public string ModuleName { get; set; }

    /// <summary>
    /// Display module types
    /// </summary>
    [Option(Name = "-types", Help = "Display module types.")]
    public bool DisplayTypes { get; set; }
    #endregion

    #region Services
    [ServiceImport]
    public IConsoleOrFileLoggingService ConsoleOrFileLogging { get; set; }
    #endregion

    #region Public Methods
    /// <summary>
    /// Execute the command
    /// </summary>
    public override void Invoke()
    {
        if (!DisplayTypes)
        {
            DisplayModules();
        }
        else
        {
            DisplayModuleTypes();
        }
    }

    /// <summary>
    /// Return command help
    /// </summary>
    /// <returns></returns>
    [HelpInvoke]
    public static string GetCommandHelp() => $"""

    DumpModulesExport [Options]

    Export managed modules.

    -name                       Module name.
    -types                      Display module types.
    {GetExportOptions()}


    """;
    #endregion

    #region Private Methods
    /// <summary>
    /// Return modules
    /// </summary>
    /// <returns></returns>
    private IEnumerable<ClrModule> GetModules()
    {
        if (string.IsNullOrEmpty(ModuleName))
        {
            return Runtime.EnumerateModules();
        }
        else
        {
            return Runtime.EnumerateModules().Where(module => module.Name != null && Path.GetFileName(module.Name).StartsWith(ModuleName, System.StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Display modules
    /// </summary>
    private void DisplayModules()
    {
        Table table = TableExportFactory.GetTable([ColumnKind.DumpObj, ColumnKind.IntegerWithoutCommas, ColumnKind.Integer, ColumnKind.TypeName], GetOutputType(), OutputFile, ConsoleOrFileLogging);
        table.WriteHeader("Address", "Size", "Dynamic", "ModuleName");
        foreach (ClrModule module in GetModules())
        {
            Console.CancellationToken.ThrowIfCancellationRequested();
            table.WriteRow(module.Address, module.Size, module.IsDynamic ? 1 : 0, module.Name ?? "<N/A>");
        }
        table.WriteFooter();
    }

    /// <summary>
    /// Display module types
    /// </summary>
    private void DisplayModuleTypes()
    {
        Table table = TableExportFactory.GetTable([ColumnKind.DumpObj, ColumnKind.DumpHeap, ColumnKind.TypeName], GetOutputType(), OutputFile, ConsoleOrFileLogging);
        table.WriteHeader("Module", "MT", "ClassName");
        foreach (ClrModule module in GetModules())
        {
            foreach ((ulong methodTable, _) in module.EnumerateTypeDefToMethodTableMap())
            {
                if (Runtime.GetTypeByMethodTable(methodTable) is ClrType type)
                {
                    Console.CancellationToken.ThrowIfCancellationRequested();
                    table.WriteRow(module.Address, type.MethodTable, type.Name);
                }
            }
        }
        table.WriteFooter();
    }
    #endregion

}
