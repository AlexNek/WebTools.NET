namespace WebTools.NET.Models;

public sealed record UrlCheckResult(
    bool Reachable,
    int? HttpStatus,
    string? ErrorMessage,
    int RedirectCount = 0,
    string? FinalUrl = null,
    string? ProtectionType = null)
{
    public int ClientRedirectCount { get; init; }
}
