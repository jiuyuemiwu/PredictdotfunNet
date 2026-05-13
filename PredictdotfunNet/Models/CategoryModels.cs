using System.Text.Json.Serialization;

namespace PredictdotfunNet.Models;

public class Category
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("isNegRisk")]
    public bool IsNegRisk { get; set; }

    [JsonPropertyName("isYieldBearing")]
    public bool IsYieldBearing { get; set; }

    [JsonPropertyName("marketVariant")]
    public string? MarketVariant { get; set; }

    [JsonPropertyName("variantData")]
    public object? VariantData { get; set; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("publishedAt")]
    public string? PublishedAt { get; set; }

    [JsonPropertyName("markets")]
    public List<Market>? Markets { get; set; }

    [JsonPropertyName("startsAt")]
    public string? StartsAt { get; set; }

    [JsonPropertyName("endsAt")]
    public string? EndsAt { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; set; }

    [JsonPropertyName("tags")]
    public List<Tag>? Tags { get; set; }
}

public class Tag
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class SearchResult
{
    [JsonPropertyName("categories")]
    public List<Category>? Categories { get; set; }

    [JsonPropertyName("markets")]
    public List<Market>? Markets { get; set; }
}
