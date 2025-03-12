// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.ExtensionCommands;

namespace ByteZoo.Blog.Commands;

/// <summary>
/// Dump DateTime value command
/// </summary>
[Command(Name = "dumpdatetime", Aliases = ["DumpDateTime"], Help = "Dump DateTime value.")]
public class DumpDateTimeCommand : ClrRuntimeCommandBase
{

    #region Options
    [Option(Name = "-value", Help = "DateTime raw value (hex).")]
    public string RawValue { get; set; }

    [Argument(Help = "DateTime instance address ([Address]).")]
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
            Console.WriteLine($"DateTime = {DateTime.FromBinary(value):yyyy-MM-dd HH:mm:ss.fffffff}");
        }
        else
        {
            ulong address = ulong.Parse(ValueAddress, NumberStyles.AllowHexSpecifier);
            byte[] buffer = new byte[8];
            Memory.ReadMemory(address, buffer, out _);
            long value = BitConverter.ToInt64(buffer, 0);
            Console.WriteLine($"DateTime = {DateTime.FromBinary(value):yyyy-MM-dd HH:mm:ss.fffffff}");
        }
    }

    /// <summary>
    /// Return command help
    /// </summary>
    /// <returns></returns>
    [HelpInvoke]
    public static string GetCommandHelp() => """

    DumpDateTime [Options] [Address]

    Dump DateTime value.

    -value                      DateTime raw value (hex).
    Address                     DateTime instance address.


    """;
    #endregion

}
