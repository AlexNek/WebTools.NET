namespace WebTools.NET.Models;

public sealed record SearchResult(
    bool Success,
    IReadOnlyList<SearchResultItem> Results,
    string? ErrorMessage);
