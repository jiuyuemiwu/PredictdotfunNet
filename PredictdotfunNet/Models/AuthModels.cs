using System.Text.Json.Serialization;

namespace PredictdotfunNet.Models;

public class AuthMessageResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("siweMessage")]
    public string? SiweMessage { get; set; }
}

public class AuthRequest
{
    [JsonPropertyName("signature")]
    public string Signature { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("signer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Signer { get; set; }
}

public class AuthTokenResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}
