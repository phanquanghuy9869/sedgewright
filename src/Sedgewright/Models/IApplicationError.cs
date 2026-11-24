using System.Collections.Generic;
using System.Linq;

namespace Sedgewright.Models;

public interface IApplicationError
{
    string[] Messages { get; }
    int LineNumber { get; }
    string FilePath { get; }
    string MemberName { get; }
}

public static class IApplicationErrorExtensions
{
    extension(IEnumerable<IApplicationError> errors)
    {
        public  string[] GetMessages() => [.. errors.SelectMany(e => e.Messages)];
    }
}
