// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using ByteZoo.Blog.Commands.Enums;
using ByteZoo.Blog.Commands.Interfaces;
using ByteZoo.Blog.Commands.Output;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.ExtensionCommands.Output;
using Microsoft.Diagnostics.Runtime;

namespace ByteZoo.Blog.Commands.Services;

/// <summary>
/// Export heap service
/// </summary>
[ServiceExport(Scope = ServiceScope.Runtime)]
public class DumpHeapExportService
{

    #region Display Type
    /// <summary>
    /// Display type
    /// </summary>
    public enum DisplayType
    {
        Address,
        ThinLock,
        String,
        StringSummary,
        Free,
        FreeSummary,
        Object,
        ObjectSummary,
        ObjectFragmentationSummary
    }
    #endregion

    #region Constants
    private const char StringReplacementCharacter = '.';
    #endregion

    #region Services
    [ServiceImport]
    public IConsoleService Console { get; set; }

    [ServiceImport]
    public IMemoryService Memory { get; set; }

    [ServiceImport]
    public IConsoleOrFileLoggingService ConsoleOrFileLogging { get; set; }
    #endregion

    #region Public Methods
    /// <summary>
    /// Print heap objects
    /// </summary>
    /// <param name="objects"></param>
    /// <param name="displayType"></param>
    /// <param name="maxStringLength"></param>
    /// <param name="minFragmentationBlockSize"></param>
    /// <param name="outputType"></param>
    /// <param name="outputFile"></param>
    public void PrintHeap(IEnumerable<ClrObject> objects, DisplayType displayType, int maxStringLength, ulong minFragmentationBlockSize, OutputType outputType, string outputFile)
    {
        Table details = IsDisplayTypeDetails(displayType) ? TableExportFactory.GetTable(GetDetailsColumns(displayType), outputType, outputFile, ConsoleOrFileLogging) : null;
        Dictionary<(string String, ulong Size), uint> stringStatistics = [];
        Dictionary<ulong, (int Count, ulong Size, string TypeName)> objectStatistics = [];
        ClrObject lastFreeObject = default;
        List<(ClrObject Free, ClrObject Next)> fragmentationBlocks = [];
        details?.WriteHeader(GetDetailsHeader(displayType));
        foreach (ClrObject obj in objects)
        {
            Console.CancellationToken.ThrowIfCancellationRequested();
            if (displayType == DisplayType.Address)
            {
                details.WriteRow(obj);
            }
            else if (displayType == DisplayType.ThinLock)
            {
                if (obj.GetThinLock() is ClrThinLock thinLock)
                {
                    details.WriteRow(obj, thinLock.Thread, thinLock.Thread?.OSThreadId ?? 0, thinLock.Recursion);
                }
            }
            else
            {
                ulong size = obj.IsValid ? obj.Size : 0;
                if (displayType is DisplayType.String or DisplayType.Free or DisplayType.Object)
                {
                    details.WriteRow(obj, obj.Type, obj.IsValid ? size : null);
                }
                else if (displayType == DisplayType.StringSummary)
                {
                    UpdateStringStatistics(obj, size, stringStatistics, maxStringLength);
                }
                else if (displayType is DisplayType.FreeSummary or DisplayType.ObjectSummary)
                {
                    UpdateObjectStatistics(obj, size, objectStatistics);
                }
                else if (displayType == DisplayType.ObjectFragmentationSummary)
                {
                    lastFreeObject = UpdateObjectFragmentationStatistics(obj, size, lastFreeObject, fragmentationBlocks, minFragmentationBlockSize);
                }
            }
        }
        details?.WriteFooter();
        if (displayType == DisplayType.StringSummary)
        {
            PrintStringStatistics(stringStatistics, outputType, outputFile);
        }
        else if (displayType is DisplayType.FreeSummary or DisplayType.ObjectSummary)
        {
            PrintObjectStatistics(objectStatistics, outputType, outputFile);
        }
        else if (displayType == DisplayType.ObjectFragmentationSummary)
        {
            PrintFragmentationStatistics(fragmentationBlocks, outputType, outputFile);
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Check if display type is details
    /// </summary>
    /// <param name="displayType"></param>
    /// <returns></returns>
    private static bool IsDisplayTypeDetails(DisplayType displayType) => displayType is DisplayType.Address or DisplayType.ThinLock or DisplayType.String or DisplayType.Free or DisplayType.Object;

    /// <summary>
    /// Return details columns
    /// </summary>
    /// <param name="displayType"></param>
    /// <returns></returns>
    private static Column[] GetDetailsColumns(DisplayType displayType) => displayType switch
    {
        DisplayType.Address => [ColumnKind.DumpObj],
        DisplayType.ThinLock => [ColumnKind.DumpObj, ColumnKind.Pointer, ColumnKind.HexValue, ColumnKind.IntegerWithoutCommas],
        DisplayType.String or DisplayType.Free or DisplayType.Object => [ColumnKind.DumpObj, ColumnKind.DumpHeap, ColumnKind.IntegerWithoutCommas],
        _ => []
    };

    /// <summary>
    /// Return details header
    /// </summary>
    /// <param name="displayType"></param>
    /// <returns></returns>
    private static string[] GetDetailsHeader(DisplayType displayType) => displayType switch
    {
        DisplayType.Address => ["Address"],
        DisplayType.ThinLock => ["Object", "Thread", "OSID", "Recursion"],
        DisplayType.String or DisplayType.Free or DisplayType.Object => ["Address", "MT", "Size"],
        _ => []
    };

    /// <summary>
    /// Update string statistics
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="size"></param>
    /// <param name="stringStatistics"></param>
    /// <param name="maxStringLength"></param>
    private static void UpdateStringStatistics(ClrObject obj, ulong size, Dictionary<(string String, ulong Size), uint> stringStatistics, int maxStringLength)
    {
        string value = obj.AsString(maxStringLength);
        (string value, ulong size) key = (value, size);
        stringStatistics.TryGetValue(key, out uint stringCount);
        stringStatistics[key] = stringCount + 1;
    }

    /// <summary>
    /// Update object statistics
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="size"></param>
    /// <param name="objectStatistics"></param>
    private void UpdateObjectStatistics(ClrObject obj, ulong size, Dictionary<ulong, (int Count, ulong Size, string TypeName)> objectStatistics)
    {
        ulong mt;
        if (obj.Type is not null)
        {
            mt = obj.Type.MethodTable;
        }
        else
        {
            Memory.ReadPointer(obj, out mt);
        }
        if (!objectStatistics.TryGetValue(mt, out (int Count, ulong Size, string TypeName) typeStats))
        {
            objectStatistics.Add(mt, (1, size, obj.Type?.Name ?? $"<unknown_type_{mt:x}>"));
        }
        else
        {
            objectStatistics[mt] = (typeStats.Count + 1, typeStats.Size + size, typeStats.TypeName);
        }
    }

    /// <summary>
    /// Update object fragmentation statistics
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="size"></param>
    /// <param name="lastFreeObject"></param>
    /// <param name="fragmentationBlocks"></param>
    /// <param name="minFragmentationBlockSize"></param>
    /// <returns></returns>
    private static ClrObject UpdateObjectFragmentationStatistics(ClrObject obj, ulong size, ClrObject lastFreeObject, List<(ClrObject Free, ClrObject Next)> fragmentationBlocks, ulong minFragmentationBlockSize)
    {
        if (lastFreeObject.IsFree && obj.IsValid && !obj.IsFree)
        {
            if (lastFreeObject.Address + lastFreeObject.Size == obj.Address)
            {
                ClrSegment seg = obj.Type.Heap.GetSegmentByAddress(obj);
                if (seg is not null && seg.Kind is not GCSegmentKind.Large or GCSegmentKind.Pinned or GCSegmentKind.Frozen)
                {
                    fragmentationBlocks.Add((lastFreeObject, obj));
                }
            }
        }
        if (obj.IsFree && size >= minFragmentationBlockSize)
        {
            return obj;
        }
        else
        {
            return default;
        }
    }

    /// <summary>
    /// Sanitize string
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private static string SanitizeString(string value)
    {
        foreach (char ch in value)
        {
            if (!char.IsLetterOrDigit(ch))
            {
                return FilterString(value);
            }
        }
        return value;
    }

    /// <summary>
    /// Filter string
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private static string FilterString(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        ReadOnlySpan<char> valueSpan = value.AsSpan(0, buffer.Length);
        for (int i = 0; i < valueSpan.Length; ++i)
        {
            char ch = valueSpan[i];
            buffer[i] = char.IsLetterOrDigit(ch) || char.IsPunctuation(ch) || ch == ' ' ? ch : StringReplacementCharacter;
        }
        return buffer.ToString();
    }

    /// <summary>
    /// Print string statistics
    /// </summary>
    /// <param name="statistics"></param>
    /// <param name="outputType"></param>
    /// <param name="outputFile"></param>
    private void PrintStringStatistics(Dictionary<(string String, ulong Size), uint> statistics, OutputType outputType, string outputFile)
    {
        Table table = TableExportFactory.GetTable([ColumnKind.IntegerWithoutCommas, ColumnKind.IntegerWithoutCommas, ColumnKind.Text], outputType, outputFile, ConsoleOrFileLogging);
        table.WriteHeader("Count", "TotalSize", "Text");
        foreach (var item in statistics.Select(i => new { Count = i.Value, TotalSize = i.Value * i.Key.Size, String = SanitizeString(i.Key.String) }).OrderBy(i => i.TotalSize))
        {
            Console.CancellationToken.ThrowIfCancellationRequested();
            table.WriteRow(item.Count, item.TotalSize, item.String);
        }
        table.WriteFooter();
    }

    /// <summary>
    /// Print object statistics
    /// </summary>
    /// <param name="statistics"></param>
    /// <param name="outputType"></param>
    /// <param name="outputFile"></param>
    private void PrintObjectStatistics(Dictionary<ulong, (int Count, ulong Size, string TypeName)> statistics, OutputType outputType, string outputFile)
    {
        Table table = TableExportFactory.GetTable([ColumnKind.DumpHeap, ColumnKind.IntegerWithoutCommas, ColumnKind.IntegerWithoutCommas, ColumnKind.TypeName], outputType, outputFile, ConsoleOrFileLogging);
        table.WriteHeader("MT", "Count", "TotalSize", "ClassName");
        foreach (var item in statistics.Select(i => new { MethodTable = i.Key, i.Value.Count, i.Value.Size, i.Value.TypeName }).OrderBy(i => i.Size))
        {
            Console.CancellationToken.ThrowIfCancellationRequested();
            table.WriteRow(item.MethodTable, item.Count, item.Size, item.TypeName);
        }
        table.WriteFooter();
    }

    /// <summary>
    /// Print fragmentation statistics
    /// </summary>
    /// <param name="statistics"></param>
    /// <param name="outputType"></param>
    /// <param name="outputFile"></param>
    private void PrintFragmentationStatistics(List<(ClrObject Free, ClrObject Next)> statistics, OutputType outputType, string outputFile)
    {
        Table table = TableExportFactory.GetTable([ColumnKind.ListNearObj, ColumnKind.IntegerWithoutCommas, ColumnKind.DumpObj, ColumnKind.TypeName], outputType, outputFile, ConsoleOrFileLogging);
        table.WriteHeader("Address", "Size", "FollowedBy", "ClassName");
        foreach ((ClrObject free, ClrObject next) in statistics)
        {
            Console.CancellationToken.ThrowIfCancellationRequested();
            table.WriteRow(free.Address, free.Size, next.Address, next.Type);
        }
        table.WriteFooter();
    }
    #endregion
}
