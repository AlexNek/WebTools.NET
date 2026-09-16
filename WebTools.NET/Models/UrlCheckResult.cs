using System.Text;

namespace WebTools.NET.Models;

public sealed record UrlCheckResult(
    bool Reachable,
    int? HttpStatus,
    string? ErrorMessage,
    int RedirectCount = 0,
    string? FinalUrl = null,
    string? ProtectionType = null)
{
    /// <summary>
    /// Number of observed main-frame client-side URL changes during the bounded browser
    /// observation window. This value is intentionally excluded from the record's value
    /// equality, hash code, and <c>ToString()</c> output so that those behave exactly as the
    /// original positional contract, which had no client-redirect member.
    /// </summary>
    public int ClientRedirectCount { get; init; }

    // The equality, hash, and PrintMembers overrides below deliberately consider only the six
    // positional members. Do not add ClientRedirectCount here: keeping it out preserves the
    // released equality and string representation.
    public bool Equals(UrlCheckResult? other) =>
        other is not null
        && Reachable == other.Reachable
        && HttpStatus == other.HttpStatus
        && ErrorMessage == other.ErrorMessage
        && RedirectCount == other.RedirectCount
        && FinalUrl == other.FinalUrl
        && ProtectionType == other.ProtectionType;

    public override int GetHashCode() =>
        HashCode.Combine(Reachable, HttpStatus, ErrorMessage, RedirectCount, FinalUrl, ProtectionType);

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Reachable = ").Append(Reachable);
        builder.Append(", HttpStatus = ").Append(HttpStatus);
        builder.Append(", ErrorMessage = ").Append(ErrorMessage);
        builder.Append(", RedirectCount = ").Append(RedirectCount);
        builder.Append(", FinalUrl = ").Append(FinalUrl);
        builder.Append(", ProtectionType = ").Append(ProtectionType);
        return true;
    }
}
