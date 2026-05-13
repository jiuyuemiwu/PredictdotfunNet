using System.Text.Json.Serialization;

namespace PredictdotfunNet.Models;

public class Position
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("market")]
    public PositionMarket Market { get; set; } = new();

    [JsonPropertyName("outcome")]
    public PositionOutcome Outcome { get; set; } = new();

    [JsonPropertyName("amount")]
    public string Amount { get; set; } = "";

    [JsonPropertyName("valueUsd")]
    public string ValueUsd { get; set; } = "";

    [JsonPropertyName("averageBuyPriceUsd")]
    public string AverageBuyPriceUsd { get; set; } = "";

    [JsonPropertyName("pnlUsd")]
    public string PnlUsd { get; set; } = "";
}

public class PositionMarket
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("conditionId")]
    public string ConditionId { get; set; } = "";

    [JsonPropertyName("isNegRisk")]
    public bool IsNegRisk { get; set; }

    [JsonPropertyName("isYieldBearing")]
    public bool IsYieldBearing { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("status")]
    public object? Status { get; set; }

    [JsonPropertyName("tradingStatus")]
    public object? TradingStatus { get; set; }
}

public class PositionOutcome
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("indexSet")]
    public int IndexSet { get; set; }

    [JsonPropertyName("onChainId")]
    public string OnChainId { get; set; } = "";

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

public class PositionsQuery : PaginatedQuery
{
    public int? MarketId { get; set; }
    public bool? IsResolved { get; set; }
    public string? Sort { get; set; }
}
