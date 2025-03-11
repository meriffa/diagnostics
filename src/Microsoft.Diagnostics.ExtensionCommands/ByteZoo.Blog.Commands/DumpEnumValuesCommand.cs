// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Diagnostics.DebugServices;
using Microsoft.Diagnostics.ExtensionCommands;
using Microsoft.Diagnostics.Runtime;

namespace ByteZoo.Blog.Commands;

/// <summary>
/// Dump enum values command
/// </summary>
[Command(Name = "dumpenumvalues", Aliases = ["DumpEnumValues", "dev"], Help = "Dump enum values.")]
public class DumpEnumValuesCommand : ClrRuntimeCommandBase
{

    #region Options
    [Option(Name = "-mt", Help = "Enum MethodTable.")]
    public string EnumMethodTable { get; set; }

    [Option(Name = "-type", Help = "Enum type name.")]
    public string EnumType { get; set; }

    [Option(Name = "-module", Help = "Enum type module name.")]
    public string EnumModule { get; set; } = null!;
    #endregion

    #region Public Methods
    /// <summary>
    /// Execute the command
    /// </summary>
    public override void Invoke()
    {
        ClrType type = GetEnumType();
        if (type.IsEnum)
        {
            foreach ((string name, object value) in type.AsEnum().EnumerateValues())
            {
                Console.WriteLine($"{type.Name}.{name} = {value}");
            }
        }
        else
        {
            throw new($"Type '{type.Name}' is not an enum.");
        }
    }

    /// <summary>
    /// Return command help
    /// </summary>
    /// <returns></returns>
    [HelpInvoke]
    public static string GetCommandHelp() => """

    DumpEnumValues [Options]

    Dump enum values.

    -mt                         Enum MethodTable.
    -type                       Enum type name.
    -module                     Enum type module name.


    """;
    #endregion

    #region Private Methods
    /// <summary>
    /// Return enum type
    /// </summary>
    /// <returns></returns>
    private ClrType GetEnumType()
    {
        if (!string.IsNullOrWhiteSpace(EnumMethodTable))
        {
            return TryParseAddress(EnumMethodTable, out ulong value) && Runtime.GetTypeByMethodTable(value) is ClrType type ? type : throw new ArgumentException($"Invalid MethodTable '{EnumMethodTable}' specified.");
        }
        else if (!string.IsNullOrEmpty(EnumType))
        {
            if (!string.IsNullOrEmpty(EnumModule))
            {
                if (GetModule(EnumModule).GetTypeByName(EnumType) is ClrType moduleType)
                {
                    return moduleType;
                }
            }
            else
            {
                foreach (ClrModule module in Runtime.EnumerateModules())
                {
                    if (module.GetTypeByName(EnumType) is ClrType type)
                    {
                        return type;
                    }
                }
            }
            throw new($"Type '{EnumType}' is not found.");
        }
        else
        {
            throw new($"No enum type specified.");
        }
    }

    /// <summary>
    /// Return module instance
    /// </summary>
    /// <param name="moduleName"></param>
    /// <returns></returns>
    protected ClrModule GetModule(string moduleName)
    {
        string name = $"{Path.DirectorySeparatorChar}{moduleName}";
        return Runtime.EnumerateModules().FirstOrDefault(i => i.Name != null && i.Name.EndsWith(name, StringComparison.Ordinal)) ?? throw new($"Module '{moduleName}' is not found.");
    }
    #endregion

}
