namespace WebTools.NET.Models;

public sealed record UrlCheckResult(
    bool Reachable,
    int? HttpStatus,
    string? ErrorMessage,
    string? FinalUrl,
    int RedirectCount = 0,
    string? ProtectionType = null)
{
    public int ClientRedirectCount { get; init; }
}