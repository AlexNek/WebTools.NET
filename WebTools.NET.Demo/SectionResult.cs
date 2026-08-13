namespace WebTools.NET.Demo;

/// <summary>
/// Outcome of one demo section, rendered in the final summary table.
/// </summary>
internal sealed record SectionResult(string Name, bool Ok, string Detail, TimeSpan Elapsed);
