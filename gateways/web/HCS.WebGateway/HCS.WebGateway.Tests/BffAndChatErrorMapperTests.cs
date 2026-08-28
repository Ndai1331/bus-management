using System.Collections.Generic;
using System.Net;
using HCS.Blazor.Client.Collaboration;
using HCS.Blazor.Client.Services;
using Microsoft.Extensions.Localization;
using Xunit;

namespace HCS.WebGateway.Tests;

public sealed class BffAndChatErrorMapperTests
{
    private readonly MapLocalizer localizer = new();

    [Fact]
    public void Save_bad_request_is_a_validation_message()
    {
        Assert.Equal("Catalog:ValidationError",
            BffErrorMapper.From(localizer, HttpStatusCode.BadRequest, BffErrorKind.Save));
    }

    [Fact]
    public void Save_business_error_code_is_localized_from_response_body()
    {
        var exception = new BffApiException(HttpStatusCode.Forbidden,
            """{"error":{"code":"Work:DefinitionHasRunningInstances","message":"blocked"}}""");
        Assert.Equal("Work:DefinitionHasRunningInstances",
            BffErrorMapper.From(localizer, exception, BffErrorKind.Save));
    }

    [Fact]
    public void TryReadErrorCode_reads_abp_remote_service_payload()
    {
        Assert.True(BffErrorMapper.TryReadErrorCode(
            """{"error":{"code":"Work:DefinitionHasRunningInstances","message":"x"}}""",
            out var code));
        Assert.Equal("Work:DefinitionHasRunningInstances", code);
    }

    [Fact]
    public void TryReadErrorCode_reads_top_level_code()
    {
        Assert.True(BffErrorMapper.TryReadErrorCode(
            """{"code":"Work:DefinitionHasRunningInstances","message":"x"}""",
            out var code));
        Assert.Equal("Work:DefinitionHasRunningInstances", code);
    }

    [Fact]
    public void Chat_unauthorized_is_a_session_message_not_a_missing_grant()
    {
        var exception = new CollaborationApiException(HttpStatusCode.Unauthorized, null);
        Assert.Equal("Catalog:Unauthorized", ChatErrorMapper.From(localizer, exception, "Chat:LoadError"));
    }

    [Fact]
    public void Chat_forbidden_is_the_missing_grant_message()
    {
        var exception = new CollaborationApiException(HttpStatusCode.Forbidden, null);
        Assert.Equal("Chat:NoPermission", ChatErrorMapper.From(localizer, exception, "Chat:LoadError"));
    }

    private sealed class MapLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => this[name];
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
