using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HCS.Blazor.Client.Components;

public sealed record CatalogSelect2Item(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("avatarUrl")] string? AvatarUrl = null,
    [property: JsonPropertyName("initials")] string? Initials = null);

public sealed record CatalogSelect2SearchResponse(
    [property: JsonPropertyName("results")] List<CatalogSelect2Item> Results,
    [property: JsonPropertyName("more")] bool More);
