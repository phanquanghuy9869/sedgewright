using System;
using System.Runtime.CompilerServices;

namespace Sedgewright.Models;

public record ExceptionError(
    string[] Messages,
    Exception Exception,
    [CallerLineNumber] int LineNumber = 0,
    [CallerFilePath] string FilePath = "",
    [CallerMemberName] string MemberName = ""
) : IApplicationError;
