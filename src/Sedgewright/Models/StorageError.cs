using System.Runtime.CompilerServices;

namespace Sedgewright.Models;

public sealed record StorageError(
    string[] Messages,
    [CallerLineNumber] int LineNumber = 0,
    [CallerFilePath] string FilePath = "",
    [CallerMemberName] string MemberName = ""
) : IApplicationError;
