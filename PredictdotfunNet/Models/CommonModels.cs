using System.Text.Json.Serialization;

namespace PredictdotfunNet.Models;

public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

public class PaginatedQuery
{
    public int? First { get; set; }
    public string? After { get; set; }
}

public class ApiError
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
