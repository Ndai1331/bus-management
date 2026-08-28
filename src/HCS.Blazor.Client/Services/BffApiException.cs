using System;
using System.Net;

namespace HCS.Blazor.Client.Services;

public sealed class BffApiException(HttpStatusCode statusCode, string? responseBody)
    : Exception($"The request failed with HTTP {(int)statusCode}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? ResponseBody { get; } = responseBody;
}
