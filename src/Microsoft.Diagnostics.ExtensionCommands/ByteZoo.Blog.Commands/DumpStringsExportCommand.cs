// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using ByteZoo.Blog.Commands.Interfaces;
using ByteZoo.Blog.Commands.Output;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.ExtensionCommands;
using Microsoft.Diagnostics.ExtensionCommands.Output;
using Microsoft.Diagnostics.Runtime;

namespace ByteZoo.Blog.Commands;

/// <summary>
/// Export strings command
/// </summary>
[Command(Name = "dumpstringsexport", Aliases = ["DumpStringsExport", "dse"], Help = "Export strings.")]
public class DumpStringsExportCommand : ExportCommandBase
{

    #region Constants
    private const string FIELD_STRING_LENGTH = "_stringLength";
    #endregion

    #region Options
    [Option(Name = "-start", Help = "Filter strings by start value.")]
    public string FilterStarts { get; set; } = null!;

    [Option(Name = "-end", Help = "Filter strings by end value.")]
    public string FilterEnds { get; set; } = null!;

    [Option(Name = "-contain", Help = "Filter strings by containing value.")]
    public string FilterContains { get; set; } = null!;

    [Option(Name = "-ignoreCase", Aliases = ["-i"], Help = "Perform case insensitive search.")]
    public bool IgnoreCase { get; set; }

    [Option(Name = "-maxStringLength", Help = "Specify maximum string length (default = 1024).")]
    public int MaxStringLength { get; set; } = 1024;

    [Option(Name = "-ignoreGCState", Help = "Ignore the GC's marker that the heap is not walkable (will generate lots of false positive errors).")]
    public bool IgnoreGCState { get; set; }

    [Argument(Help = "Filter strings by exact value.")]
    public string FilterExactMatch { get; set; }
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
        HeapWithFilters heap = GetFilteredHeap();
        IEnumerable<ClrObject> objects = heap.EnumerateFilteredObjects(Console.CancellationToken);
        objects = FilterObjectsByString(objects);
        bool filterStarts = !string.IsNullOrEmpty(FilterStarts);
        bool filterEnds = !string.IsNullOrEmpty(FilterEnds);
        bool filterContains = !string.IsNullOrEmpty(FilterContains);
        bool filterExactMatch = !string.IsNullOrEmpty(FilterExactMatch);
        StringComparison comparisonType = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        Table table = TableExportFactory.GetTable([ColumnKind.DumpObj, ColumnKind.IntegerWithoutCommas, ColumnKind.IntegerWithoutCommas, ColumnKind.Text], GetOutputType(), OutputFile, ConsoleOrFileLogging);
        table.WriteHeader("Address", "Length", "Size", "Text");
        foreach (ClrObject obj in objects)
        {
            Console.CancellationToken.ThrowIfCancellationRequested();
            string value = obj.AsString(MaxStringLength);
            bool match = true;
            if (filterStarts && value != null)
            {
                match = value.StartsWith(FilterStarts, comparisonType);
            }
            if (match && filterEnds && value != null)
            {
                match = value.EndsWith(FilterEnds, comparisonType);
            }
            if (match && filterContains && value != null)
            {
                match = value.IndexOf(FilterContains, comparisonType) != -1;
            }
            if (match && filterExactMatch && value != null)
            {
                match = value.Equals(FilterExactMatch, comparisonType);
            }
            if (match)
            {
                table.WriteRow(obj.Address, GetStringLength(obj), obj.Size, value);
            }
        }
        table.WriteFooter();
    }

    /// <summary>
    /// Return command help
    /// </summary>
    /// <returns></returns>
    [HelpInvoke]
    public static string GetCommandHelp() => $"""

    DumpStringsExport [Options] [ExactValue]

    Export strings.

    -start                      Filter strings by start value.
    -end                        Filter strings by end value.
    -contain                    Filter strings by containing value.
    -ignoreCase, -i             Perform case insensitive search.
    -maxStringLength            Specify maximum string length (default = 1024).
    -ignoreGCState              Ignore the GC's marker that the heap is not walkable (will generate lots of false positive errors).
    {GetExportOptions()}
    ExactValue                  Filter strings by exact value.


    """;
    #endregion

    #region Private Methods
    /// <summary>
    /// Return filtered heap
    /// </summary>
    /// <returns></returns>
    private HeapWithFilters GetFilteredHeap()
    {
        if (!Runtime.Heap.CanWalkHeap && !IgnoreGCState)
        {
            throw new DiagnosticsException("The GC heap is not in a valid state for traversal (use -ignoreGCState to override).");
        }
        return new(Runtime.Heap) { SortSegments = (segment) => segment.OrderBy(seg => seg.Start) };
    }

    /// <summary>
    /// Filter heap objects by string
    /// </summary>
    /// <param name="objects"></param>
    /// <returns></returns>
    private IEnumerable<ClrObject> FilterObjectsByString(IEnumerable<ClrObject> objects)
    {
        ulong methodTable = Runtime.Heap.StringType.MethodTable;
        return objects.Where(obj => {
            ulong mt;
            if (obj.Type is not null)
            {
                mt = obj.Type.MethodTable;
            }
            else
            {
                Memory.ReadPointer(obj, out mt);
            }
            return mt == methodTable;
        });
    }

    /// <summary>
    /// Return string length
    /// </summary>
    /// <param name="clrObject"></param>
    /// <returns></returns>
    private static int GetStringLength(ClrObject clrObject) => clrObject.ReadField<int>(FIELD_STRING_LENGTH);
    #endregion

}
