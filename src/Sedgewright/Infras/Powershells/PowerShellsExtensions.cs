using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Text.RegularExpressions;

namespace Sedgewright.Infrastructure.Powershells;

public static class PowerShellsExtensions
{
    private static readonly Regex AnsiEscapeSequenceRegex = new("\\x1B\\[[0-?]*[ -/]*[@-~]", RegexOptions.Compiled);
    private static readonly Regex OtherControlSequenceRegex = new("\\x1B(?:\\][^\\x07]*(?:\\x07|\\x1B\\\\)|[@-Z\\\\-_])", RegexOptions.Compiled);

    extension(PowerShell ps)
    {
        public static PowerShellExecutionResult ExecuteCommandBypassPolicy(string command) =>
            ExecuteBypassPolicy(powerShell => powerShell.AddScript(command));

        public static PowerShellExecutionResult ExecuteCommandBypassPolicy(string command, Action<string>? streamCallback) =>
            ExecuteBypassPolicy(powerShell => powerShell.AddScript(command), streamCallback);

        public static PowerShellExecutionResult ExecuteCommandBypassPolicy(string command, string workingDirectory, Action<string>? streamCallback = null) =>
            ExecuteBypassPolicy(powerShell => powerShell.AddScript(GetLocationScript(workingDirectory) + command), streamCallback);

        public static PowerShellExecutionResult ExecuteScript(string scriptPath) =>
            ExecuteBypassPolicy(powerShell => powerShell.AddCommand(scriptPath));

        public static PowerShellExecutionResult ExecuteScript(string scriptPath, Action<string>? streamCallback) =>
            ExecuteBypassPolicy(powerShell => powerShell.AddCommand(scriptPath), streamCallback);

        public static PowerShellExecutionResult ExecuteScript(string scriptPath, string workingDirectory, Action<string>? streamCallback = null) =>
            ExecuteBypassPolicy(powerShell => powerShell.AddScript(GetLocationScript(workingDirectory)).AddCommand(scriptPath), streamCallback);
    }

    private static string GetLocationScript(string workingDirectory)
        => $"Set-Location -LiteralPath '{workingDirectory.Replace("'", "''")}'; ";

    private static void AttachStreamLogging(PowerShell ps, Action<string> streamCallback)
    {
        ps.Streams.Error.DataAdded += (_, e) =>
            StreamSanitizedMessage("[ERR] " + ps.Streams.Error[e.Index], streamCallback);

        ps.Streams.Warning.DataAdded += (_, e) =>
            StreamSanitizedMessage("[WRN] " + ps.Streams.Warning[e.Index], streamCallback);

        ps.Streams.Verbose.DataAdded += (_, e) =>
            StreamSanitizedMessage("[VRB] " + ps.Streams.Verbose[e.Index], streamCallback);

        ps.Streams.Debug.DataAdded += (_, e) =>
            StreamSanitizedMessage("[DBG] " + ps.Streams.Debug[e.Index], streamCallback);

        ps.Streams.Information.DataAdded += (_, e) =>
            StreamSanitizedMessage("[INF] " + ps.Streams.Information[e.Index].MessageData, streamCallback);

        ps.Streams.Progress.DataAdded += (_, e) =>
        {
            ProgressRecord progressRecord = ps.Streams.Progress[e.Index];
            StreamSanitizedMessage($"[PRO] {progressRecord.Activity} {progressRecord.PercentComplete}% {progressRecord.StatusDescription}", streamCallback);
        };
    }

    private static void AttachOutputLogging(PSDataCollection<PSObject> outputCollection, Action<string> streamCallback)
    {
        outputCollection.DataAdded += (_, e) =>
        {
            string? message = outputCollection[e.Index]?.ToString();
            StreamSanitizedMessage(message, streamCallback);
        };
    }

    private static void StreamSanitizedMessage(object? message, Action<string> streamCallback)
    {
        foreach (string line in NormalizeConsoleText(message?.ToString()).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            streamCallback(line);
        }
    }

    private static string NormalizeConsoleText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        normalized = OtherControlSequenceRegex.Replace(normalized, string.Empty);
        normalized = AnsiEscapeSequenceRegex.Replace(normalized, string.Empty);

        StringBuilder builder = new(normalized.Length);
        foreach (char character in normalized)
        {
            if (character == '\n' || character == '\t' || !char.IsControl(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Trim();
    }

    private static void ConfigurePlainTextOutput(PowerShell powerShell)
    {
        const string configurationScript = "[Console]::InputEncoding=[System.Text.UTF8Encoding]::new($false); " +
            "[Console]::OutputEncoding=[System.Text.UTF8Encoding]::new($false); " +
            "$OutputEncoding=[System.Text.UTF8Encoding]::new($false); " +
            "$PSStyle.OutputRendering='PlainText'; " +
            "$env:NO_COLOR='1'; " +
            "$env:CLICOLOR='0'; " +
            "$env:FORCE_COLOR='0'; " +
            "$env:TERM='dumb';";

        powerShell.AddScript(configurationScript).AddStatement();
    }

    private static PowerShellExecutionResult ExecuteBypassPolicy(Action<PowerShell> executeAction)
    {
        return ExecuteBypassPolicy(executeAction, null);
    }

    private static PowerShellExecutionResult ExecuteBypassPolicy(Action<PowerShell> executeAction, Action<string>? streamCallback)
    {
        InitialSessionState state = PowerShellBypassSessionState.GetInstance().SessionState;
        using var runspace = RunspaceFactory.CreateRunspace(state);
        runspace.Open();
        using var powerShell = PowerShell.Create(runspace);
        ConfigurePlainTextOutput(powerShell);
        executeAction(powerShell);
        return ExecutePowerShell(powerShell, streamCallback);
    }

    private static PowerShellExecutionResult ExecutePowerShell(PowerShell powerShell, Action<string>? streamCallback)
    {
        try
        {
            using PSDataCollection<PSObject> inputCollection = [];
            using PSDataCollection<PSObject> outputCollection = [];
            inputCollection.Complete();

            if (streamCallback is not null)
            {
                AttachStreamLogging(powerShell, streamCallback);
                AttachOutputLogging(outputCollection, streamCallback);
            }

            IAsyncResult asyncResult = powerShell.BeginInvoke<PSObject, PSObject>(inputCollection, outputCollection);
            asyncResult.AsyncWaitHandle.WaitOne();
            powerShell.EndInvoke(asyncResult);

            Collection<PSObject> results = [.. outputCollection];
            string stdOut = NormalizeConsoleText(string.Join("\n", results.Select(r => r.ToString())));
            string stdErr = NormalizeConsoleText(string.Join("\n", powerShell.Streams.Error.Select(e => e.ToString())));
            int exitCode = powerShell.HadErrors ? 1 : 0;
            return new PowerShellExecutionResult
            {
                ExitCode = exitCode,
                StandardOutput = stdOut,
                StandardError = stdErr
            };
        }
        catch (Exception ex)
        {
            return new PowerShellExecutionResult
            {
                ExitCode = 1,
                StandardOutput = string.Empty,
                StandardError = ex.ToString()
            };
        }
    }
}
