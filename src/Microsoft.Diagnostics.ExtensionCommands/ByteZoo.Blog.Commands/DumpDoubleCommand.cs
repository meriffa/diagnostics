// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.ExtensionCommands;

namespace ByteZoo.Blog.Commands;

/// <summary>
/// Dump double value command
/// </summary>
[Command(Name = "dumpdouble", Aliases = ["DumpDouble"], Help = "Dump double value.")]
public class DumpDoubleCommand : ClrRuntimeCommandBase
{

    #region Options
    [Option(Name = "-value", Help = "Double raw value (hex).")]
    public string RawValue { get; set; }

    [Argument(Help = "Double instance address ([Address]).")]
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
            ulong value = ulong.Parse(RawValue, NumberStyles.AllowHexSpecifier);
            Console.WriteLine($"Double = {BitConverter.ToDouble(BitConverter.GetBytes(value), 0)}");
        }
        else
        {
            ulong address = ulong.Parse(ValueAddress, NumberStyles.AllowHexSpecifier);
            byte[] buffer = new byte[8];
            Memory.ReadMemory(address, buffer, out _);
            Console.WriteLine($"Double = {BitConverter.ToDouble(buffer, 0)}");
        }
    }

    /// <summary>
    /// Return command help
    /// </summary>
    /// <returns></returns>
    [HelpInvoke]
    public static string GetCommandHelp() => """

    DumpDouble [Options] [Address]

    Dump double value.

    -value                      Double raw value (hex).
    Address                     Double instance address.


    """;
    #endregion

}
