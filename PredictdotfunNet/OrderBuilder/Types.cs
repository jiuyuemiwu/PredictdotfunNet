using System.Text.Json.Serialization;

namespace PredictdotfunNet.OrderBuilder;

public class SignedOrder
{
    [JsonPropertyName("salt")]
    public string Salt { get; set; } = "";

    [JsonPropertyName("maker")]
    public string Maker { get; set; } = "";

    [JsonPropertyName("signer")]
    public string Signer { get; set; } = "";

    [JsonPropertyName("taker")]
    public string Taker { get; set; } = "0x0000000000000000000000000000000000000000";

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

public class Eip712TypedData
{
    public EIP712Domain Domain { get; set; } = new();
    public Dictionary<string, object> Message { get; set; } = [];
}

public class EIP712Domain
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public int ChainId { get; set; }
    public string VerifyingContract { get; set; } = "";
}

public class ContractWrapper
{
    public Nethereum.Contracts.Contract Contract { get; set; } = null!;
}
