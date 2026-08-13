namespace WebTools.NET.Models;

public sealed record WebContent(
    bool Success,
    string Content,
    string? ErrorMessage,
    string FinalUrl);
