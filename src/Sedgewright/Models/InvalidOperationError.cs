using System.Runtime.CompilerServices;

namespace Sedgewright.Models;

public sealed record InvalidOperationError(
    string[] Messages,
    [CallerLineNumber] int LineNumber = 0,
    [CallerFilePath] string FilePath = "",
    [CallerMemberName] string MemberName = ""
) : IApplicationError
{
    /// <summary>
    /// Gets additional diagnostic details that should not be shown in a concise error summary.
    /// </summary>
    public string? Details { get; init; }
}
