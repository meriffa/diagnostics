// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.ExtensionCommands;

namespace ByteZoo.Blog.Commands;

/// <summary>
/// Dump DateOnly value command
/// </summary>
[Command(Name = "dumpdateonly", Aliases = ["DumpDateOnly"], Help = "Dump DateOnly value.")]
public class DumpDateOnlyCommand : ClrRuntimeCommandBase
{

    #region Options
    [Option(Name = "-value", Help = "DateOnly raw value (hex).")]
    public string RawValue { get; set; }

    [Argument(Help = "DateOnly instance address ([Address]).")]
    public string ValueAddress { get; set; }
    #endregion

    #region Services
    [ServiceImport]
    public IMemoryService Memory { get; set; }
    #endregion

    #region Public Methods
    /// <summary>
    /// Execute the command
    /// </summary>
    public override void Invoke()
    {
        if (!string.IsNullOrEmpty(RawValue))
        {
            int value = int.Parse(RawValue, NumberStyles.AllowHexSpecifier);
            Console.WriteLine($"DateOnly = {GetEquivalentDateTime(value):yyyy-MM-dd}");
        }
        else
        {
            ulong address = ulong.Parse(ValueAddress, NumberStyles.AllowHexSpecifier);
            byte[] buffer = new byte[4];
            Memory.ReadMemory(address, buffer, out _);
            int value = BitConverter.ToInt32(buffer, 0);
            Console.WriteLine($"DateOnly = {GetEquivalentDateTime(value):yyyy-MM-dd}");
        }
    }

    /// <summary>
    /// Return command help
    /// </summary>
    /// <returns></returns>
    [HelpInvoke]
    public static string GetCommandHelp() => """

    DumpDateOnly [Options] [Address]

    Dump DateOnly value.

    -value                      DateOnly raw value (hex).
    Address                     DateOnly instance address.


    """;
    #endregion

    #region Private Methods
    /// <summary>
    /// Return DateTime from DateOnly.DayNumber
    /// </summary>
    /// <param name="dayNumber"></param>
    /// <returns></returns>
    private static DateTime GetEquivalentDateTime(int dayNumber) => new(dayNumber * TimeSpan.TicksPerDay);
    #endregion

}
