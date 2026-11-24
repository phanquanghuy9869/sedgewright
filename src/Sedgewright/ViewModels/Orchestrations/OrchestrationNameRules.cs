using System.Text.RegularExpressions;

namespace Sedgewright.ViewModels.Orchestrations;

internal static partial class OrchestrationNameRules
{
    public const string Pattern = "^[a-zA-Z0-9_-]+$";

    public static string ValidationMessage
        => $"Orchestration name must match the pattern '{Pattern}'. Use only letters, numbers, underscores, and hyphens.";

    public static bool IsValid(string name)
        => NamePattern().IsMatch(name);

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
