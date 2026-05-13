using System.Text.Json.Serialization;

namespace PredictdotfunNet.Models;

public class BuildOrderInput
{
    public string Maker { get; set; } = "";
    public string Signer { get; set; } = "";
    public Side Side { get; set; }
    public string TokenId { get; set; } = "";
    public string MakerAmount { get; set; } = "";
    public string TakerAmount { get; set; } = "";
    public long Nonce { get; set; }
    public string? Salt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public int FeeRateBps { get; set; }
}

public class LimitHelperInput
{
    public Side Side { get; set; }
    public string PricePerShareWei { get; set; } = "";
    public string QuantityWei { get; set; } = "";
}

public class MarketHelperInput
{
    public Side Side { get; set; }
    public string QuantityWei { get; set; } = "";
    public string? SlippageBps { get; set; }
}

public class MarketHelperValueInput
{
    public Side Side { get; set; }
    public string ValueWei { get; set; } = "";
    public string? SlippageBps { get; set; }
}

public class OrderAmounts
{
    [JsonPropertyName("lastPrice")]
    public string LastPrice { get; set; } = "";

    [JsonPropertyName("pricePerShare")]
    public string PricePerShare { get; set; } = "";

    [JsonPropertyName("makerAmount")]
    public string MakerAmount { get; set; } = "";

    [JsonPropertyName("takerAmount")]
    public string TakerAmount { get; set; } = "";

    [JsonPropertyName("amount")]
    public string? Amount { get; set; }

    [JsonPropertyName("slippageBps")]
    public string? SlippageBps { get; set; }

    [JsonPropertyName("isMinAmountOut")]
    public bool? IsMinAmountOut { get; set; }
}

public class TransactionResult
{
    public bool Success { get; set; }
    public string? Receipt { get; set; }
    public string? Cause { get; set; }
}

public class SetApprovalsResult
{
    public bool Success { get; set; }
    public List<TransactionResult> Transactions { get; set; } = [];
}

public class CancelOrdersOptions
{
    public bool IsNegRisk { get; set; }
    public bool IsYieldBearing { get; set; }
}

public class OrderBuilderOptions
{
    public string? PredictAccount { get; set; }
}

public class RedeemPositionsInput
{
    public string ConditionId { get; set; } = "";
    public int IndexSet { get; set; }
    public string? Amount { get; set; }
    public bool IsNegRisk { get; set; }
    public bool IsYieldBearing { get; set; }
}

public class MergePositionsInput
{
    public string ConditionId { get; set; } = "";
    public string Amount { get; set; } = "";
    public bool IsNegRisk { get; set; }
    public bool IsYieldBearing { get; set; }
}
