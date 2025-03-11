// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Diagnostics.DebugServices;

namespace ByteZoo.Blog.Commands;

/// <summary>
/// Log to console or file service
/// </summary>
/// <param name="console"></param>
public class ConsoleOrFileLoggingService(IConsoleService console) : IConsoleOrFileLoggingService
{

    #region Private Members
    private readonly List<StreamWriter> writers = [];
    private FileStream consoleStream;
    #endregion

    #region IConsoleService
    /// <summary>
    /// Write text to console's standard out
    /// </summary>
    /// <param name="value"></param>
    public void Write(string value)
    {
        if (writers.Count == 0)
        {
            console.Write(value);
        }
        else
        {
            WriteFile(value, writers);
        }
    }

    /// <summary>
    /// Write warning text to console
    /// </summary>
    /// <param name="value"></param>
    public void WriteWarning(string value)
    {
        if (writers.Count == 0)
        {
            console.WriteWarning(value);
        }
        else
        {
            WriteFile(value, writers);
        }
    }

    /// <summary>
    /// Write error text to console
    /// </summary>
    /// <param name="value"></param>
    public void WriteError(string value)
    {
        if (writers.Count == 0)
        {
            console.WriteError(value);
        }
        else
        {
            WriteFile(value, writers);
        }
    }

    /// <summary>
    /// Writes Debugger Markup Language (DML) markup text.
    /// </summary>
    /// <param name="text"></param>
    public void WriteDml(string text)
    {
        if (writers.Count == 0)
        {
            console.WriteDml(text);
        }
        else
        {
            WriteFile(text, writers);
        }
    }

    /// <summary>
    /// Writes an exec tag to the output stream.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="action"></param>
    public void WriteDmlExec(string text, string action)
    {
        if (writers.Count == 0)
        {
            console.WriteDmlExec(text, action);
        }
        else
        {
            WriteFile(text, writers);
        }
    }

    /// <summary>
    /// Gets whether <see cref="WriteDml"/> is supported.
    /// </summary>
    public bool SupportsDml => console.SupportsDml;

    /// <summary>
    /// Cancellation token for current command
    /// </summary>
    public CancellationToken CancellationToken { get => console.CancellationToken; set => console.CancellationToken = value; }

    /// <summary>
    /// Screen or window width or 0.
    /// </summary>
    public int WindowWidth => console.WindowWidth;
    #endregion

    #region IConsoleFileLoggingService
    /// <summary>
    /// The log file path if enabled, otherwise null.
    /// </summary>
    public string FilePath => consoleStream?.Name;

    /// <summary>
    /// Enable file logging.
    /// </summary>
    /// <param name="filePath"></param>
    public void Enable(string filePath)
    {
        FileStream consoleStream = File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        Disable();
        AddStream(consoleStream);
        this.consoleStream = consoleStream;
    }

    /// <summary>
    /// Disable/close file logging.
    /// </summary>
    public void Disable()
    {
        if (consoleStream is not null)
        {
            RemoveStream(consoleStream);
            consoleStream.Close();
            consoleStream = null;
        }
    }

    /// <summary>
    /// Add to the list of file streams to write the output.
    /// </summary>
    /// <param name="stream">Stream to add. Lifetime managed by caller.</param>
    public void AddStream(Stream stream)
    {
        writers.Add(new StreamWriter(stream) { AutoFlush = true });
    }

    /// <summary>
    /// Remove the specified file stream from the writers.
    /// </summary>
    /// <param name="stream">Stream passed to add. Stream not closed or disposed.</param>
    public void RemoveStream(Stream stream)
    {
        if (stream is not null)
        {
            foreach (StreamWriter writer in writers)
            {
                if (writer.BaseStream == stream)
                {
                    writers.Remove(writer);
                    break;
                }
            }
        }
    }
    #endregion

    #region IDisposable
    /// <summary>
    /// Finalization
    /// </summary>
    public void Dispose() => Disable();
    #endregion

    #region Private Methods
    /// <summary>
    /// Write value to file
    /// </summary>
    /// <param name="value"></param>
    /// <param name="writers"></param>
    private static void WriteFile(string value, List<StreamWriter> writers)
    {
        foreach (StreamWriter writer in writers)
        {
            try
            {
                writer.Write(value);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or NotSupportedException)
            {
            }
        }
    }
    #endregion

}
