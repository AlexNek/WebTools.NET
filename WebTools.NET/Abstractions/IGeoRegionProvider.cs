namespace WebTools.NET.Abstractions;

public interface IGeoRegionProvider
{
    Task<string> DetectRegionAsync(CancellationToken ct = default);
}
