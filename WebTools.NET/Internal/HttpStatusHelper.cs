using System.Net;

namespace WebTools.NET.Internal;

/// <summary>
/// HTTP status code helpers using well-known class boundaries (RFC 9110).
/// The numeric literals 200, 300, 400 are not magic numbers here — they are
/// the universally recognized boundaries between HTTP status-code classes:
/// 2xx Successful, 3xx Redirection, 4xx Client Error, 5xx Server Error.
/// Named constants for these boundaries would reduce readability without
/// adding clarity. Specific status codes (403, 429) that carry distinct
/// semantics are named as constants.
/// </summary>
internal static class HttpStatusHelper
{
    internal const int Ok = (int)HttpStatusCode.OK;

    internal const int Forbidden = (int)HttpStatusCode.Forbidden;

    internal const int TooManyRequests = (int)HttpStatusCode.TooManyRequests;

    internal static bool IsSuccess(int statusCode) =>
        statusCode is >= 200 and < 300;

    internal static bool IsRedirect(int statusCode) =>
        statusCode is
            (int)HttpStatusCode.MultipleChoices or
            (int)HttpStatusCode.MovedPermanently or
            (int)HttpStatusCode.Found or
            (int)HttpStatusCode.SeeOther or
            (int)HttpStatusCode.TemporaryRedirect or
            (int)HttpStatusCode.PermanentRedirect;

    // 304 is not followed as a redirect, but it remains a reachable terminal response.
    internal static bool IsSuccessOrRedirect(int statusCode) =>
        statusCode is >= 200 and < 300 ||
        statusCode == (int)HttpStatusCode.NotModified ||
        IsRedirect(statusCode);

    internal static bool IsNotSuccess(int statusCode) =>
        !IsSuccess(statusCode);

    internal static bool IsError(int statusCode) =>
        statusCode >= 400;
}
