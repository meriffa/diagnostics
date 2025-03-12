// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.ExtensionCommands;

namespace ByteZoo.Blog.Commands;

/// <summary>
/// Dump TimeSpan value command
/// </summary>
[Command(Name = "dumptimespan", Aliases = ["DumpTimeSpan"], Help = "Dump TimeSpan value.")]
public class DumpTimeSpanCommand : ClrRuntimeCommandBase
{

    #region Options
    [Option(Name = "-value", Help = "TimeSpan raw value (hex).")]
    public string RawValue { get; set; }

    [Argument(Help = "TimeSpan instance address ([Address]).")]
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
            long value = long.Parse(RawValue, NumberStyles.AllowHexSpecifier);
            Console.WriteLine($"TimeSpan = {new TimeSpan(value):d\\.hh\\:mm\\:ss\\.fffffff}");
        }
        else
        {
            ulong address = ulong.Parse(ValueAddress, NumberStyles.AllowHexSpecifier);
            byte[] buffer = new byte[8];
            Memory.ReadMemory(address, buffer, out _);
            long value = BitConverter.ToInt64(buffer, 0);
            Console.WriteLine($"TimeSpan = {new TimeSpan(value):d\\.hh\\:mm\\:ss\\.fffffff}");
        }
    }

    /// <summary>
    /// Return command help
    /// </summary>
    /// <returns></returns>
    [HelpInvoke]
    public static string GetCommandHelp() => """

    DumpTimeSpan [Options] [Address]

    Dump TimeSpan value.

    -value                      TimeSpan raw value (hex).
    Address                     TimeSpan instance address.


    """;
    #endregion

}
