// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using ByteZoo.Blog.Commands.Interfaces;
using ByteZoo.Blog.Commands.Output;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.ExtensionCommands.Output;
using Microsoft.Diagnostics.Runtime;

namespace ByteZoo.Blog.Commands;

/// <summary>
/// Export GC Handles command
/// </summary>
[Command(Name = "dumpgchandlesexport", Aliases = ["DumpGCHandlesExport", "dgche"], Help = "Export GC Handles.")]
public class DumpGCHandlesExportCommand : ExportCommandBase
{

    #region Display Type
    /// <summary>
    /// Display type
    /// </summary>
    private enum DisplayType
    {
        Handles,
        Statistics,
        Totals
    }
    #endregion

    #region Options
    [Option(Name = "-displayType", Aliases = ["-d"], Help = "Display type (Handles, Statistics, Totals, default = Statistics).")]
    public string DisplayTypeOption { get; set; } = "Statistics";

    [Option(Name = "-handleKind", Aliases = ["-k"], Help = "Filter GC Handles by kind (WeakShort, WeakLong, Strong, Pinned, RefCounted, Dependent, AsyncPinned, SizedRef, WeakWinRT).")]
    public string HandleKind { get; set; }
    #endregion

    #region Services
    [ServiceImport]
    public IMemoryService Memory { get; set; }

    [ServiceImport]
    public IConsoleOrFileLoggingService ConsoleOrFileLogging { get; set; }
    #endregion

    #region Public Methods
    /// <summary>
    /// Execute the command
    /// </summary>
    public override void Invoke()
    {
        DisplayType displayType = GetDisplayType();
        ClrHandleKind? handleKind = !string.IsNullOrEmpty(HandleKind) ? GetHandleKind() : null;
        IEnumerable<ClrHandle> handles = Runtime.EnumerateHandles().Where(h => HandleKind == null || h.HandleKind == handleKind);
        if (displayType == DisplayType.Handles)
        {
            PrintGCHandles(handles);
        }
        else if (displayType == DisplayType.Statistics)
        {
            PrintGCHandleStatistics(handles);
        }
        else if (displayType == DisplayType.Totals)
        {
            PrintGCHandleTotals(handles);
        }
    }

    /// <summary>
    /// Return command help
    /// </summary>
    /// <returns></returns>
    [HelpInvoke]
    public static string GetCommandHelp() => $"""

    DumpGCHandlesExport [Options]

    Export GC Handles.
    
    -displayType, -d            Display type (Handles, Statistics, Totals, default = Statistics).
    -handleKind, -k             Filter GC Handles by kind (WeakShort, WeakLong, Strong, Pinned, RefCounted, Dependent, AsyncPinned, SizedRef, WeakWinRT).
    {GetExportOptions()}


    """;
    #endregion

    #region Private Methods
    /// <summary>
    /// Return display type
    /// </summary>
    /// <returns></returns>
    private DisplayType GetDisplayType() => Enum.TryParse(DisplayTypeOption, true, out DisplayType value) ? value : throw new ArgumentException($"Invalid display type '{DisplayTypeOption}' specified.");

    /// <summary>
    /// Return handle kind
    /// </summary>
    /// <returns></returns>
    private ClrHandleKind GetHandleKind() => Enum.TryParse(HandleKind, true, out ClrHandleKind value) ? value : throw new ArgumentException($"Invalid GC Handle kind '{HandleKind}' specified.");

    /// <summary>
    /// Update GC Handle statistics
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="statistics"></param>
    private void UpdateGCHandleStatistics(ClrHandle handle, Dictionary<ulong, (int Count, ulong Size, string TypeName)> statistics)
    {
        ulong mt;
        if (handle.Object.Type is not null)
        {
            mt = handle.Object.Type.MethodTable;
        }
        else
        {
            Memory.ReadPointer(handle.Object, out mt);
        }
        if (!statistics.TryGetValue(mt, out (int Count, ulong Size, string TypeName) typeStats))
        {
            statistics.Add(mt, (1, handle.Object.Size, handle.Object.Type?.Name ?? $"<unknown_type_{mt:x}>"));
        }
        else
        {
            statistics[mt] = (typeStats.Count + 1, typeStats.Size + handle.Object.Size, typeStats.TypeName);
        }
    }

    /// <summary>
    /// Print GC Handles
    /// </summary>
    /// <param name="handles"></param>
    private void PrintGCHandles(IEnumerable<ClrHandle> handles)
    {
        Table table = TableExportFactory.GetTable([ColumnKind.Pointer, ColumnKind.Text, ColumnKind.DumpObj, ColumnKind.IntegerWithoutCommas, ColumnKind.Pointer, ColumnKind.TypeName, ColumnKind.TypeName], GetOutputType(), OutputFile, ConsoleOrFileLogging);
        table.WriteHeader("Handle", "Type", "Object", "Size", "Data", "ClassName");
        foreach (ClrHandle handle in handles)
        {
            Console.CancellationToken.ThrowIfCancellationRequested();
            table.WriteRow(handle.Address, handle.HandleKind, handle.Object.Address, handle.Object.Size, handle.Dependent.IsNull ? null : handle.Dependent.Address, handle.Object.Type?.Name);
        }
        table.WriteFooter();
    }

    /// <summary>
    /// Print GC Handle statistics
    /// </summary>
    /// <param name="handles"></param>
    private void PrintGCHandleStatistics(IEnumerable<ClrHandle> handles)
    {
        Dictionary<ulong, (int Count, ulong Size, string TypeName)> statistics = [];
        foreach (ClrHandle handle in handles)
        {
            Console.CancellationToken.ThrowIfCancellationRequested();
            UpdateGCHandleStatistics(handle, statistics);
        }
        Table table = TableExportFactory.GetTable([ColumnKind.DumpHeap, ColumnKind.IntegerWithoutCommas, ColumnKind.IntegerWithoutCommas, ColumnKind.TypeName], GetOutputType(), OutputFile, ConsoleOrFileLogging);
        table.WriteHeader("MT", "Count", "TotalSize", "ClassName");
        foreach (var item in statistics.Select(i => new { MethodTable = i.Key, i.Value.Count, i.Value.Size, i.Value.TypeName }).OrderBy(i => i.Size))
        {
            Console.CancellationToken.ThrowIfCancellationRequested();
            table.WriteRow(item.MethodTable, item.Count, item.Size, item.TypeName);
        }
        table.WriteFooter();
    }

    /// <summary>
    /// Print GC Handle totals
    /// </summary>
    /// <param name="handles"></param>
    private void PrintGCHandleTotals(IEnumerable<ClrHandle> handles)
    {
        Dictionary<ClrHandleKind, int> statistics = [];
        foreach (ClrHandle handle in handles)
        {
            Console.CancellationToken.ThrowIfCancellationRequested();
            if (!statistics.TryGetValue(handle.HandleKind, out int count))
            {
                statistics.Add(handle.HandleKind, 1);
            }
            else
            {
                statistics[handle.HandleKind]++;
            }
        }
        Table table = TableExportFactory.GetTable([ColumnKind.Text, ColumnKind.IntegerWithoutCommas], GetOutputType(), OutputFile, ConsoleOrFileLogging);
        table.WriteHeader("Type", "Count");
        foreach (var item in statistics.Select(i => new { Kind = i.Key, Count = i.Value }).OrderBy(i => i.Kind))
        {
            Console.CancellationToken.ThrowIfCancellationRequested();
            table.WriteRow(item.Kind, item.Count);
        }
        table.WriteFooter();
    }
    #endregion

}
