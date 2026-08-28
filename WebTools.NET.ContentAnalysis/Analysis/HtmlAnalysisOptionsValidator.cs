using WebTools.NET.ContentAnalysis.Models;

namespace WebTools.NET.ContentAnalysis.Analysis;

internal static class HtmlAnalysisOptionsValidator
{
    public static void Validate(HtmlAnalysisOptions options)
    {
        ValidatePositive(options.MaxInputLength, nameof(options.MaxInputLength));
        ValidatePositive(options.MaxTextBlocks, nameof(options.MaxTextBlocks));
        ValidatePositive(options.MaxStructuredDataRecords, nameof(options.MaxStructuredDataRecords));
        ValidatePositive(options.MaxStructuredDataPayloadLength, nameof(options.MaxStructuredDataPayloadLength));
        ValidatePositive(options.MaxCandidateRegions, nameof(options.MaxCandidateRegions));
        ValidatePositive(options.MaxCandidateFragmentLength, nameof(options.MaxCandidateFragmentLength));
        ValidatePositive(options.NearbyTextWindow, nameof(options.NearbyTextWindow));
    }

    private static void ValidatePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be positive.");
        }
    }
}
