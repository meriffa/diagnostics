// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.ExtensionCommands;

namespace ByteZoo.Blog.Commands;

/// <summary>
/// Dump single value command
/// </summary>
[Command(Name = "dumpsingle", Aliases = ["DumpSingle"], Help = "Dump single value.")]
public class DumpSingleCommand : ClrRuntimeCommandBase
{

    #region Options
    [Option(Name = "-value", Help = "Single raw value (hex).")]
    public string RawValue { get; set; }

    [Argument(Help = "Single instance address ([Address]).")]
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
            uint value = uint.Parse(RawValue, NumberStyles.AllowHexSpecifier);
            Console.WriteLine($"Single = {BitConverter.ToSingle(BitConverter.GetBytes(value), 0)}");
        }
        else
        {
            ulong address = ulong.Parse(ValueAddress, NumberStyles.AllowHexSpecifier);
            byte[] buffer = new byte[4];
            Memory.ReadMemory(address, buffer, out _);
            Console.WriteLine($"Single = {BitConverter.ToSingle(buffer, 0)}");
        }
    }

    /// <summary>
    /// Return command help
    /// </summary>
    /// <returns></returns>
    [HelpInvoke]
    public static string GetCommandHelp() => """

    DumpSingle [Options] [Address]

    Dump single value.

    -value                      Single raw value (hex).
    Address                     Single instance address.


    """;
    #endregion

}
