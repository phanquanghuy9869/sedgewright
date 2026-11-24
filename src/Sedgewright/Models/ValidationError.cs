using System.Runtime.CompilerServices;

namespace Sedgewright.Models;

public sealed record ValidationError(
    string[] Messages,
    [CallerLineNumber] int LineNumber = 0,
    [CallerFilePath] string FilePath = "",
    [CallerMemberName] string MemberName = ""
) : IApplicationError;
