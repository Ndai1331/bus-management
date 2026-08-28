using System;
using System.Net;
using HCS.Blazor.Client.Collaboration;
using Microsoft.Extensions.Localization;

namespace HCS.Blazor.Client.Services;

public static class ChatErrorMapper
{
    public static string From(IStringLocalizer localizer, Exception exception, string fallbackKey)
    {
        if (exception is not CollaborationApiException api)
        {
            return localizer[fallbackKey].Value;
        }

        if (api.ResponseBody?.Contains("AdminTransferRequired", StringComparison.OrdinalIgnoreCase) == true)
            return localizer["Collaboration:AdminTransferRequired"].Value;

        if (api.ResponseBody?.Contains("WorkSubjectNotProvisioned", StringComparison.OrdinalIgnoreCase) == true)
            return localizer["Chat:WorkSubjectNotProvisioned"].Value;

        return api.StatusCode switch
        {
            HttpStatusCode.Unauthorized => localizer["Catalog:Unauthorized"].Value,
            HttpStatusCode.Forbidden => localizer["Chat:NoPermission"].Value,
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                => localizer["Catalog:ServiceUnavailable"].Value,
            _ => localizer[fallbackKey].Value
        };
    }
}
