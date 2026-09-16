using WebTools.NET.Models;

namespace WebTools.NET.Search;

internal sealed record SearchAttemptOutcome(
    SearchResult Result,
    SearchFailureKind FailureKind);
