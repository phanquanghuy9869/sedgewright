using System;
using Microsoft.Agents.AI.Tools.Shell;
using Sedgewright.Entities;

namespace Sedgewright.Infras.MAF.Extensions;

public static class ShellExecutorExtensions
{
    extension(ShellExecutor)
    {
        public static ShellExecutor From(ShellConfiguration config)
        => config.Executor switch
        {
            ShellExecutorType.Local => new LocalShellExecutor(
                new LocalShellExecutorOptions
                {
                    WorkingDirectory = config.WorkingDirectory,
                    ConfineWorkingDirectory = true,
                    Policy = new ShellPolicy(denyList:
                    [
                        @"\brm\s+-rf\b",
                        @"\bsudo\b",
                        @":\(\)\s*\{",          // fork-bomb shape
                        @"\bmkfs\b",
                        @">\s*/dev/sd",
                    ]),
                    Timeout = TimeSpan.FromSeconds(30),
                }
            ),
            ShellExecutorType.Docker or _ => new DockerShellExecutor(
                new DockerShellExecutorOptions
                {
                    HostWorkdir = config.WorkingDirectory
                }
            )
        };
    }
}
