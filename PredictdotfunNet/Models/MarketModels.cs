using System.Text.Json.Serialization;

namespace PredictdotfunNet.Models;

public class BestPrice
{
    [JsonPropertyName("price")]
    public decimal Price {get;set;}
    
    [JsonPropertyName("size")]
    public decimal Size{get;set;} 
}

public class MarketOutcome
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

public class RewardPeriod
{
    [JsonPropertyName("hourlyRate")]
    public double HourlyRate { get; set; }

    [JsonPropertyName("startsAt")]
    public string? StartsAt { get; set; }

    [JsonPropertyName("endsAt")]
    public string? EndsAt { get; set; }
}

public class MarketRewards
{
    [JsonPropertyName("current")]
    public RewardPeriod? Current { get; set; }

    [JsonPropertyName("schedule")]
    public List<RewardPeriod> Schedule { get; set; } = [];
}

public class Market
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("question")]
    public string Question { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("tradingStatus")]
    public object? TradingStatus { get; set; }

    [JsonPropertyName("status")]
    public object? Status { get; set; }

    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; set; }

    [JsonPropertyName("isNegRisk")]
    public bool IsNegRisk { get; set; }

    [JsonPropertyName("isYieldBearing")]
    public bool IsYieldBearing { get; set; }

    [JsonPropertyName("feeRateBps")]
    public int? FeeRateBps { get; set; }

    [JsonPropertyName("resolution")]
    public object? Resolution { get; set; }

    [JsonPropertyName("oracleQuestionId")]
    public string? OracleQuestionId { get; set; }

    [JsonPropertyName("conditionId")]
    public string ConditionId { get; set; } = "";

    [JsonPropertyName("resolverAddress")]
    public string? ResolverAddress { get; set; }

    [JsonPropertyName("outcomes")]
    public List<MarketOutcome> Outcomes { get; set; } = [];

    [JsonPropertyName("questionIndex")]
    public int? QuestionIndex { get; set; }

    [JsonPropertyName("spreadThreshold")]
    public double? SpreadThreshold { get; set; }

    [JsonPropertyName("shareThreshold")]
    public double? ShareThreshold { get; set; }

    [JsonPropertyName("isBoosted")]
    public bool IsBoosted { get; set; }

    [JsonPropertyName("boostStartsAt")]
    public string? BoostStartsAt { get; set; }

    [JsonPropertyName("boostEndsAt")]
    public string? BoostEndsAt { get; set; }

    [JsonPropertyName("polymarketConditionIds")]
    public List<string>? PolymarketConditionIds { get; set; }

    [JsonPropertyName("kalshiMarketTicker")]
    public string? KalshiMarketTicker { get; set; }

    [JsonPropertyName("categorySlug")]
    public string? CategorySlug { get; set; }

    [JsonPropertyName("createdAt")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("decimalPrecision")]
    public int? DecimalPrecision { get; set; }

    [JsonPropertyName("marketVariant")]
    public object? MarketVariant { get; set; }

    [JsonPropertyName("variantData")]
    public VariantData? VariantData { get; set; }

    [JsonPropertyName("team")] 
    public object? Team { get; set; }

    [JsonPropertyName("rewards")]
    public MarketRewards? Rewards { get; set; }
}

public class VariantData
{
    [JsonPropertyName("endPrice")] public decimal? EndPrice { get; set; }

    [JsonPropertyName("priceFeedProvider")] public string PriceFeedProvider { get; set; } = "";

    [JsonPropertyName("priceFeedSymbol")] public string PriceFeedSymbol { get; set; } = "";

    [JsonPropertyName("startPrice")] public decimal? StartPrice { get; set; }

    [JsonPropertyName("type")] public string Type { get; set; } = "";
}

public class MarketsQuery : PaginatedQuery
{
    public string? Status { get; set; }
    public bool? IsBoosted { get; set; }
    public string? TagIds { get; set; }
    public string? MarketVariant { get; set; }
    public string? Sort { get; set; }
    public bool? HasActiveRewards { get; set; }
}

public class MarketStatistics
{
    [JsonPropertyName("volume24h")]
    public string? Volume24h { get; set; }

    [JsonPropertyName("volumeTotal")]
    public string? VolumeTotal { get; set; }

    [JsonPropertyName("liquidity")]
    public string? Liquidity { get; set; }
}

public class LastOrderSettled
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("price")]
    public string Price { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("marketId")]
    public int MarketId { get; set; }

    [JsonPropertyName("side")]
    public string Side { get; set; } = "";

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = "";
}

public class OrderbookData
{
    [JsonPropertyName("marketId")]
    public int MarketId { get; set; }

    [JsonPropertyName("updateTimestampMs")]
    public long UpdateTimestampMs { get; set; }

    [JsonPropertyName("lastOrderSettled")]
    public LastOrderSettled? LastOrderSettled { get; set; }

    [JsonPropertyName("asks")]
    public List<List<double>> Asks { get; set; } = [];

    [JsonPropertyName("bids")]
    public List<List<double>> Bids { get; set; } = [];
}

public class MarketTimeseriesPoint
{
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("price")]
    public double Price { get; set; }
}
