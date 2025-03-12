// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.ExtensionCommands;

namespace ByteZoo.Blog.Commands;

/// <summary>
/// Dump decimal value command
/// </summary>
[Command(Name = "dumpdecimal", Aliases = ["DumpDecimal"], Help = "Dump decimal value.")]
public class DumpDecimalCommand : ClrRuntimeCommandBase
{

    #region Options
    [Option(Name = "-valueLow", Help = "Decimal raw low value (hex).")]
    public string RawValueLow { get; set; }

    [Option(Name = "-valueHigh", Help = "Decimal raw high value (hex).")]
    public string RawValueHigh { get; set; }

    [Argument(Help = "Decimal instance address ([Address]).")]
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
        if (!string.IsNullOrEmpty(RawValueLow) && !string.IsNullOrEmpty(RawValueHigh))
        {
            ulong rawValueLow = ulong.Parse(RawValueLow, NumberStyles.AllowHexSpecifier);
            int flags = (int)(rawValueLow & 0xFFFFFFFF);
            uint hi32 = (uint)(rawValueLow >> 32);
            ulong lo64 = ulong.Parse(RawValueHigh, NumberStyles.AllowHexSpecifier);
            int[] value = [(int)(lo64 & 0xFFFFFFFF), (int)(lo64 >> 32), (int)hi32, flags];
            Console.WriteLine($"Decimal = {new decimal(value)}");
        }
        else
        {
            ulong address = ulong.Parse(ValueAddress, NumberStyles.AllowHexSpecifier);
            byte[] buffer = new byte[16];
            Memory.ReadMemory(address, buffer, out _);
            ulong[] rawValues = MemoryMarshal.Cast<byte, ulong>(buffer.AsSpan()).ToArray();
            int flags = (int)(rawValues[0] & 0xFFFFFFFF);
            uint hi32 = (uint)(rawValues[0] >> 32);
            ulong lo64 = rawValues[1];
            int[] value = [(int)(lo64 & 0xFFFFFFFF), (int)(lo64 >> 32), (int)hi32, flags];
            Console.WriteLine($"Decimal = {new decimal(value)}");
        }
    }

    /// <summary>
    /// Return command help
    /// </summary>
    /// <returns></returns>
    [HelpInvoke]
    public static string GetCommandHelp() => """

    DumpDecimal [Options] [Address]

    Dump decimal value.

    -valueLow                   Decimal raw low value (hex).
    -valueHigh                  Decimal raw high value (hex).
    Address                     Decimal instance address.


    """;
    #endregion

}
