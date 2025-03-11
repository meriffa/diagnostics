// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Diagnostics.DebugServices;

namespace ByteZoo.Blog.Commands;

/// <summary>
/// Log to console or file service interface
/// </summary>
public interface IConsoleOrFileLoggingService : IConsoleService, IConsoleFileLoggingService, IDisposable
{
}
