using System.Text.Json.Serialization;

namespace PredictdotfunNet.Models;

public class Account
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("address")]
    public string Address { get; set; } = "";

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("referral")]
    public AccountReferral? Referral { get; set; }

    [JsonPropertyName("points")]
    public AccountPoints? Points { get; set; }
}

public class AccountReferral
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}

public class AccountPoints
{
    [JsonPropertyName("total")]
    public double Total { get; set; }
}

public class SetReferralRequest
{
    [JsonPropertyName("data")]
    public SetReferralData Data { get; set; } = new();
}

public class SetReferralData
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";
}

public class AccountActivity
{
    [JsonPropertyName("eventType")]
    public string? EventType { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}
