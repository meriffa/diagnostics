// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using ByteZoo.Blog.Commands.Services;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.ExtensionCommands;
using Microsoft.Diagnostics.Runtime;

namespace ByteZoo.Blog.Commands;

/// <summary>
/// Export heap command
/// </summary>
[Command(Name = "dumpheapexport", Aliases = ["DumpHeapExport", "dhe"], Help = "Export heap types & objects.")]
public class DumpHeapExportCommand : ExportCommandBase
{

    #region Options
    [Option(Name = "-displayType", Aliases = ["-d"], Help = "Display type (Address, ThinLock, String, StringSummary, Free, FreeSummary, Object, ObjectSummary, ObjectFragmentationSummary, default = ObjectSummary).")]
    public string DisplayType { get; set; } = "ObjectSummary";

    [Option(Name = "-mt", Help = "Filter heap objects by MethodTable.")]
    public string FilterMethodTable { get; set; }

    [Option(Name = "-type", Help = "Filter heap objects by type.")]
    public string FilterType { get; set; }

    [Option(Name = "-min", Help = "Filter heap objects by minimum size.")]
    public ulong FilterMin { get; set; }

    [Option(Name = "-max", Help = "Filter heap objects by maximum size.")]
    public ulong FilterMax { get; set; }

    [Option(Name = "-live", Help = "Filter heap by live objects.")]
    public bool FilterLive { get; set; }

    [Option(Name = "-dead", Help = "Filter heap by dead objects.")]
    public bool FilterDead { get; set; }

    [Option(Name = "-heap", Help = "Filter by heap index.")]
    public int FilterHeapIndex { get; set; } = -1;

    [Option(Name = "-segment", Help = "Filter by heap segment address.")]
    public string FilterSegment { get; set; }

    [Option(Name = "-gen", Help = "Filter heap objects by GC generation (Gen0, Gen1, Gen2, LOH (Large Object Heap), POH (Pinned Object Heap), FOH (Frozen Object Heap)).")]
    public string FilterGeneration { get; set; }

    [Option(Name = "-maxStringLength", Help = "Specify maximum string length (default = 1024).")]
    public int MaxStringLength { get; set; } = 1024;

    [Option(Name = "-minFragmentationBlockSize", Help = "Specify minimum fragmentation block size in bytes (default = 512 KB).")]
    public ulong MinFragmentationBlockSize { get; set; } = 512 * 1024;

    [Option(Name = "-ignoreGCState", Help = "Ignore the GC's marker that the heap is not walkable (will generate lots of false positive errors).")]
    public bool IgnoreGCState { get; set; }

    [Argument(Help = "Filter by heap object memory range ([StartAddress [EndAddress]]).")]
    public string[] FilterMemoryRange { get; set; }
    #endregion

    #region Services
    [ServiceImport]
    public LiveObjectService LiveObjects { get; set; }

    [ServiceImport]
    public IMemoryService Memory { get; set; }

    [ServiceImport]
    public DumpHeapExportService DumpHeapExport { get; set; }
    #endregion

    #region Public Methods
    /// <summary>
    /// Execute the command
    /// </summary>
    public override void Invoke()
    {
        DumpHeapExportService.DisplayType displayType = GetDisplayType();
        HeapWithFilters heap = GetFilteredHeap();
        IEnumerable<ClrObject> objects = heap.EnumerateFilteredObjects(Console.CancellationToken);
        bool? printWarningOriginal = null;
        if ((FilterLive || FilterDead) && displayType == DumpHeapExportService.DisplayType.Address)
        {
            printWarningOriginal = LiveObjects.PrintWarning;
            LiveObjects.PrintWarning = false;
        }
        objects = FilterObjectsByMethodTable(objects, displayType);
        objects = FilterObjectsByType(objects);
        objects = FilterObjectsByMinAndMax(objects);
        objects = FilterObjectsByLiveAndDead(objects);
        DumpHeapExport.PrintHeap(objects, displayType, MaxStringLength, MinFragmentationBlockSize, GetOutputType(), OutputFile);
        if (printWarningOriginal != null)
        {
            LiveObjects.PrintWarning = printWarningOriginal.Value;
        }
    }

    /// <summary>
    /// Return command help
    /// </summary>
    /// <returns></returns>
    [HelpInvoke]
    public static string GetCommandHelp() => $"""

    DumpHeapExport [Options] [StartAddress [EndAddress]]

    Export heap types & objects.
    
    -displayType, -d            Display type (Address, ThinLock, String, StringSummary, Free, FreeSummary, Object, ObjectSummary, ObjectFragmentationSummary, default = ObjectSummary).
    -mt                         Filter heap objects by MethodTable.
    -type                       Filter heap objects by type.
    -min                        Filter heap objects by minimum size.
    -max                        Filter heap objects by maximum size.
    -live                       Filter heap by live objects.
    -dead                       Filter heap by dead objects.
    -heap                       Filter by heap index.
    -segment                    Filter by heap segment address.
    -gen                        Filter heap objects by GC generation (Gen0, Gen1, Gen2, LOH (Large Object Heap), POH (Pinned Object Heap), FOH (Frozen Object Heap)).
    -maxStringLength            Specify maximum string length (default = 1024).
    -minFragmentationBlockSize  Specify minimum fragmentation block size in bytes (default = 512 KB).
    -ignoreGCState              Ignore the GC's marker that the heap is not walkable (will generate lots of false positive errors).
    {GetExportOptions()}
    StartAddress                Filter by heap object memory start address.
    EndAddress                  Filter by heap object memory end address.


    """;
    #endregion

    #region Private Methods
    /// <summary>
    /// Return display layout
    /// </summary>
    /// <returns></returns>
    private DumpHeapExportService.DisplayType GetDisplayType() => Enum.TryParse(DisplayType, true, out DumpHeapExportService.DisplayType value) ? value : throw new ArgumentException($"Invalid display type '{DisplayType}' specified.");

    /// <summary>
    /// Return MethodTable
    /// </summary>
    /// <param name="displayType"></param>
    /// <returns></returns>
    private ulong? GetMethodTable(DumpHeapExportService.DisplayType displayType)
    {
        if (displayType is DumpHeapExportService.DisplayType.String or DumpHeapExportService.DisplayType.StringSummary)
        {
            return Runtime.Heap.StringType.MethodTable;
        }
        else if (displayType is DumpHeapExportService.DisplayType.Free or DumpHeapExportService.DisplayType.FreeSummary)
        {
            return Runtime.Heap.FreeType.MethodTable;
        }
        else if (!string.IsNullOrWhiteSpace(FilterMethodTable))
        {
            return TryParseAddress(FilterMethodTable, out ulong value) ? value : throw new ArgumentException($"Invalid MethodTable '{FilterMethodTable}' specified.");
        }
        return null;
    }

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
        HeapWithFilters heap = new(Runtime.Heap);
        ApplyFilterMinAndMax(heap);
        ApplyFilterHeapIndex(heap);
        ApplyFilterSegment(heap);
        ApplyFilterGeneration(heap);
        ApplyFilterMemoryRange(heap);
        heap.SortSegments = (segment) => segment.OrderBy(seg => seg.Start);
        return heap;
    }

    /// <summary>
    /// Apply heap objects by minimum and maximum size filter
    /// </summary>
    /// <param name="heap"></param>
    private void ApplyFilterMinAndMax(HeapWithFilters heap)
    {
        if (FilterMin > 0)
        {
            heap.MinimumObjectSize = FilterMin;
        }
        if (FilterMax > 0)
        {
            heap.MaximumObjectSize = FilterMax;
        }
    }

    /// <summary>
    /// Apply heap index filter
    /// </summary>
    /// <param name="heap"></param>
    private void ApplyFilterHeapIndex(HeapWithFilters heap)
    {
        if (FilterHeapIndex != -1)
        {
            heap.GCHeap = FilterHeapIndex;
        }
    }

    /// <summary>
    /// Apply heap segment address filter
    /// </summary>
    /// <param name="heap"></param>
    private void ApplyFilterSegment(HeapWithFilters heap)
    {
        if (TryParseAddress(FilterSegment, out ulong segment))
        {
            heap.FilterBySegmentHex(segment);
        }
        else if (!string.IsNullOrWhiteSpace(FilterSegment))
        {
            throw new DiagnosticsException($"Invalid heap segment address '{FilterSegment}' specified.");
        }
    }

    /// <summary>
    /// Apply heap objects by GC generation filter
    /// </summary>
    /// <param name="heap"></param>
    private void ApplyFilterGeneration(HeapWithFilters heap)
    {
        if (!string.IsNullOrWhiteSpace(FilterGeneration))
        {
            heap.Generation = FilterGeneration.ToLowerInvariant() switch
            {
                "gen0" => Generation.Generation0,
                "gen1" => Generation.Generation1,
                "gen2" => Generation.Generation2,
                "loh" or "large" => Generation.Large,
                "poh" or "pinned" => Generation.Pinned,
                "foh" or "frozen" => Generation.Frozen,
                _ => throw new ArgumentException($"Invalid GC generation {FilterGeneration} (only gen0, gen1, gen2, loh (large), poh (pinned) and foh (frozen) are supported) specified.")
            };
        }
    }

    /// <summary>
    /// Apply heap object memory range filter
    /// </summary>
    /// <param name="heap"></param>
    private void ApplyFilterMemoryRange(HeapWithFilters heap)
    {
        if (FilterMemoryRange is not null && FilterMemoryRange.Length > 0)
        {
            if (FilterMemoryRange.Length > 2)
            {
                string invalidArgument = FilterMemoryRange.FirstOrDefault(arg => arg.StartsWith("-", StringComparison.Ordinal) || arg.StartsWith("/", StringComparison.Ordinal));
                if (invalidArgument != null)
                {
                    throw new ArgumentException($"Invalid argument '{invalidArgument}' specified.");
                }
                throw new ArgumentException("Too many arguments specified.");
            }
            string startAddress = FilterMemoryRange[0];
            string endAddress = FilterMemoryRange.Length > 1 ? FilterMemoryRange[1] : null;
            heap.FilterByHexMemoryRange(startAddress, endAddress);
        }
    }

    /// <summary>
    /// Filter heap objects by MethodTable
    /// </summary>
    /// <param name="objects"></param>
    /// <param name="displayType"></param>
    /// <returns></returns>
    private IEnumerable<ClrObject> FilterObjectsByMethodTable(IEnumerable<ClrObject> objects, DumpHeapExportService.DisplayType displayType)
    {
        ulong? methodTable = GetMethodTable(displayType);
        if (methodTable.HasValue)
        {
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
                return mt == methodTable.Value;
            });
        }
        return objects;
    }

    /// <summary>
    /// Filter heap objects by type
    /// </summary>
    /// <param name="objects"></param>
    /// <returns></returns>
    private IEnumerable<ClrObject> FilterObjectsByType(IEnumerable<ClrObject> objects)
    {
        if (FilterType is not null)
        {
            return objects.Where(obj => obj.Type?.Name?.StartsWith(FilterType, StringComparison.Ordinal) ?? false);
        }
        return objects;
    }

    /// <summary>
    /// Filter heap objects by minimum and maximum size
    /// </summary>
    /// <param name="objects"></param>
    /// <returns></returns>
    private IEnumerable<ClrObject> FilterObjectsByMinAndMax(IEnumerable<ClrObject> objects)
    {
        if (FilterMin != 0 || FilterMax != 0)
        {
            return objects.Where(obj => {
                if (!obj.IsValid)
                {
                    return false;
                }
                ulong size = obj.Size;
                if (FilterMin != 0 && size < FilterMin)
                {
                    return false;
                }
                if (FilterMax != 0 && size > FilterMax)
                {
                    return false;
                }
                return true;
            });
        }
        return objects;
    }

    /// <summary>
    /// Filter heap objects by live and dead flag
    /// </summary>
    /// <param name="objects"></param>
    /// <returns></returns>
    private IEnumerable<ClrObject> FilterObjectsByLiveAndDead(IEnumerable<ClrObject> objects)
    {
        if (FilterLive)
        {
            return objects.Where(LiveObjects.IsLive);
        }
        if (FilterDead)
        {
            return objects.Where(obj => !LiveObjects.IsLive(obj));
        }
        return objects;
    }
    #endregion

}
