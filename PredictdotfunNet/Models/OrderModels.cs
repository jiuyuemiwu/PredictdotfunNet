using System.Text.Json.Serialization;

namespace PredictdotfunNet.Models;

public class Order
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = "";

    [JsonPropertyName("salt")]
    public string Salt { get; set; } = "";

    [JsonPropertyName("maker")]
    public string Maker { get; set; } = "";

    [JsonPropertyName("signer")]
    public string Signer { get; set; } = "";

    [JsonPropertyName("taker")]
    public string Taker { get; set; } = "";

    [JsonPropertyName("tokenId")]
    public string TokenId { get; set; } = "";

    [JsonPropertyName("makerAmount")]
    public string MakerAmount { get; set; } = "";

    [JsonPropertyName("takerAmount")]
    public string TakerAmount { get; set; } = "";

    [JsonPropertyName("expiration")]
    public long Expiration { get; set; }

    [JsonPropertyName("nonce")]
    public string Nonce { get; set; } = "";

    [JsonPropertyName("feeRateBps")]
    public string FeeRateBps { get; set; } = "";

    [JsonPropertyName("side")]
    public int Side { get; set; }

    [JsonPropertyName("signatureType")]
    public int SignatureType { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = "";
}

public class OrderEntry
{
    [JsonPropertyName("order")]
    public Order Order { get; set; } = new();

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("marketId")]
    public int MarketId { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "";

    [JsonPropertyName("amount")]
    public string Amount { get; set; } = "";

    [JsonPropertyName("amountFilled")]
    public string AmountFilled { get; set; } = "";

    [JsonPropertyName("isNegRisk")]
    public bool IsNegRisk { get; set; }

    [JsonPropertyName("isYieldBearing")]
    public bool IsYieldBearing { get; set; }

    [JsonPropertyName("strategy")]
    public string Strategy { get; set; } = "LIMIT";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "OPEN";

    [JsonPropertyName("rewardEarningRate")]
    public double RewardEarningRate { get; set; }
}

public class OrdersQuery : PaginatedQuery
{
    public string? Status { get; set; }
    public int? MarketId { get; set; }
}

public class CreateOrderRequest
{
    [JsonPropertyName("data")]
    public CreateOrderData Data { get; set; } = new();
}

public class CreateOrderData
{
    [JsonPropertyName("order")]
    public Order Order { get; set; } = new();

    [JsonPropertyName("pricePerShare")]
    public string PricePerShare { get; set; } = "";

    [JsonPropertyName("strategy")]
    public string Strategy { get; set; } = "LIMIT";

    [JsonPropertyName("slippageBps")]
    public string? SlippageBps { get; set; }

    [JsonPropertyName("isFillOrKill")]
    public bool? IsFillOrKill { get; set; }

    [JsonPropertyName("isPostOnly")]
    public bool? IsPostOnly { get; set; }

    [JsonPropertyName("reservedBalancePolicy")]
    public string? ReservedBalancePolicy { get; set; }

    [JsonPropertyName("isMinAmountOut")]
    public bool? IsMinAmountOut { get; set; }

    [JsonPropertyName("selfTradePrevention")]
    public string? SelfTradePrevention { get; set; }
}

public class CreateOrderResponse
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("orderId")]
    public string? OrderId { get; set; }

    [JsonPropertyName("orderHash")]
    public string? OrderHash { get; set; }
}

public class RemoveOrdersRequest
{
    [JsonPropertyName("data")]
    public RemoveOrdersData Data { get; set; } = new();
}

public class RemoveOrdersData
{
    [JsonPropertyName("ids")]
    public List<string> Ids { get; set; } = [];
}

public class RemoveOrdersResponse
{
    [JsonPropertyName("removed")]
    public List<string> Removed { get; set; } = [];

    [JsonPropertyName("noop")]
    public List<string> Noop { get; set; } = [];
}
