using System.Text.Json.Serialization;

namespace PredictdotfunNet.Models;

public class WsMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("requestId")]
    public int? RequestId { get; set; }

    [JsonPropertyName("success")]
    public bool? Success { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("error")]
    public WsError? Error { get; set; }

    [JsonPropertyName("topic")]
    public string? Topic { get; set; }
}

public class WsError
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class WsRequest
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("requestId")]
    public int RequestId { get; set; }

    [JsonPropertyName("params")]
    public List<string> Params { get; set; } = [];
}

public class WsHeartbeatRequest
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "heartbeat";

    [JsonPropertyName("data")]
    public long Data { get; set; }
}

public class OrderbookSnapshot
{
    [JsonPropertyName("marketId")]
    public int MarketId { get; set; }
    
    [JsonPropertyName("orderCount")]
    public int OrderCount { get; set; }

    [JsonPropertyName("updateTimestampMs")]
    public decimal UpdateTimestampMs { get; set; } 

    [JsonPropertyName("lastOrderSettled")]
    public LastOrderSettled? LastOrderSettled { get; set; }

    [JsonPropertyName("asks")]
    public List<List<double>> Asks { get; set; } = [];

    [JsonPropertyName("bids")]
    public List<List<double>> Bids { get; set; } = [];
}

public class AssetPriceUpdate
{
    [JsonPropertyName("price")]
    public double Price { get; set; }

    [JsonPropertyName("publishTime")]
    public long PublishTime { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}

public class WalletEvent
{
    [JsonPropertyName("eventType")]
    public string? EventType { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}
