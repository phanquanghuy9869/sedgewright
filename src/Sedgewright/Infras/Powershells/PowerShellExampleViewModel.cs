using System;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Sedgewright.Infrastructure.Powershells;

namespace Sedgewright.Example;

/// <summary>
/// Example ViewModel showing how to use the PowerShellExecutionService.
/// This demonstrates the recommended pattern for integrating PowerShell execution into the UI.
/// </summary>
public sealed class PowerShellExampleViewModel
{
    /// <summary>
    /// Example: Execute a command asynchronously from a ViewModel.
    /// </summary>
    public static void ExecuteCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Command cannot be null or empty.", nameof(command));
        }

        try
        {
            var result = PowerShell.ExecuteCommandBypassPolicy(command);

            if (result.IsSuccess)
            {
                // Handle successful execution
                Console.WriteLine($"Command executed successfully:\n{result.StandardOutput}");
            }
            else
            {
                // Handle error
                Console.WriteLine($"Command failed with exit code {result.ExitCode}:\n{result.StandardError}");
            }
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"Invalid command: {ex.Message}");
        }
    }

    /// <summary>
    /// Example: Check if npm is installed and get its version.
    /// </summary>
    public static bool CheckNpmInstalled()
    {
        try
        {
            var result = PowerShell.ExecuteCommandBypassPolicy("npm --version");
            return result.IsSuccess;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Example: Run the skills add command from the requirements.
    /// </summary>
    public static void AddSkills()
    {
        const string skillsCommand = "npx skills add https://github.com/vercel-labs/skills --skill find-skills";

        try
        {
            var result = PowerShell.ExecuteCommandBypassPolicy(skillsCommand);

            if (result.IsSuccess)
            {
                Console.WriteLine("Skills added successfully!");
                // You could parse the output here to extract specific information
            }
            else
            {
                Console.WriteLine($"Failed to add skills:\nError: {result.StandardError}");
                Console.WriteLine($"Output: {result.StandardOutput}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception while adding skills: {ex.Message}");
        }
    }

    /// <summary>
    /// Example: Execute a PowerShell script file.
    /// </summary>
    public static void ExecuteScript(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            throw new ArgumentException("Script path cannot be null or empty.", nameof(scriptPath));
        }

        try
        {
            var result = PowerShell.ExecuteScript(scriptPath);

            if (result.IsSuccess)
            {
                Console.WriteLine($"Script executed successfully:\n{result.StandardOutput}");
            }
            else
            {
                Console.WriteLine($"Script failed with exit code {result.ExitCode}:\n{result.StandardError}");
            }
        }
        catch (FileNotFoundException ex)
        {
            Console.WriteLine($"Script file not found: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception while executing script: {ex.Message}");
        }
    }

    /// <summary>
    /// Example: Execute a command with a timeout using cancellation.
    /// </summary>
    public static async Task ExecuteCommandWithTimeoutAsync(string command, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            var result = PowerShell.ExecuteCommandBypassPolicy(command);

            if (result.IsSuccess)
            {
                Console.WriteLine($"Command completed within timeout:\n{result.StandardOutput}");
            }
            else
            {
                Console.WriteLine($"Command failed:\n{result.StandardError}");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"Command timed out after {timeout.TotalSeconds} seconds");
        }
    }
}
