namespace Sedgewright.Infrastructure.Powershells;

public record PowerShellExecutionResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;

    public bool IsSuccess => ExitCode == 0;
}
