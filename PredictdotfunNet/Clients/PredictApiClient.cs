using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PredictdotfunNet.Exceptions;
using PredictdotfunNet.Models;

namespace PredictdotfunNet.Clients;

public class PredictApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private string? _jwtToken;
    private DateTime _jwtExpiry = DateTime.MinValue;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Create with IHttpClientFactory (recommended for DI — avoids socket exhaustion)
    /// </summary>
    public PredictApiClient(string apiKey, IHttpClientFactory httpClientFactory)
    {
        _apiKey = apiKey;
        _httpClient = httpClientFactory.CreateClient("predict-api");
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
    }

    /// <summary>
    /// Create with explicit HttpClient (for backward compat / non-DI usage)
    /// </summary>
    public PredictApiClient(string apiKey, string baseUrl = "https://api.predict.fun", HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _httpClient = httpClient ?? new HttpClient { BaseAddress = new Uri(baseUrl) };
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
    }

    public void SetJwtToken(string token)
    {
        _jwtToken = token;
        _jwtExpiry = DateTime.UtcNow.AddHours(24);
    }

    public string? GetJwtToken() => _jwtToken;

    public void ClearJwtToken()
    {
        _jwtToken = null;
        _jwtExpiry = DateTime.MinValue;
    }

    private bool IsJwtValid => _jwtToken != null && DateTime.UtcNow < _jwtExpiry;

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, bool requireAuth = false)
    {
        var req = new HttpRequestMessage(method, path);
        if (requireAuth && _jwtToken != null)
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);
        }
        return req;
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct = default)
    {
        var resp = await _httpClient.SendAsync(request, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            throw new PredictApiException(
                $"API request failed: {(int)resp.StatusCode} {resp.ReasonPhrase}",
                (int)resp.StatusCode,
                body
            );
        }

        var result = JsonSerializer.Deserialize<ApiResponse<T>>(body, _jsonOptions);
        if (result == null || !result.Success)
        {
            throw new PredictApiException($"API returned unsuccessful response: {body}", (int)resp.StatusCode, body);
        }

        return result.Data!;
    }

    private async Task SendAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        var resp = await _httpClient.SendAsync(request, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new PredictApiException(
                $"API request failed: {(int)resp.StatusCode}",
                (int)resp.StatusCode,
                body
            );
        }
    }

    #region Auth

    public async Task<string> GetAuthMessageAsync(CancellationToken ct = default)
    {
        var req = CreateRequest(HttpMethod.Get, $"/v1/auth/message");
        var result = await SendAsync<AuthMessageResponse>(req, ct);
        return result.Message ?? result.SiweMessage ?? throw new PredictApiException("No auth message in response");
    }

    public async Task<string> AuthenticateAsync(string signer, string signature, string message, CancellationToken ct = default)
    {
        // The signer must match the account that subsequent authenticated order actions use.
        // For Predict Account mode this is the smart wallet/deposit address, not the Privy EOA.
        var authBody = new { signer, message, signature };
        var body = JsonSerializer.Serialize(authBody, _jsonOptions);

        var req = CreateRequest(HttpMethod.Post, "/v1/auth");
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var resp = await _httpClient.SendAsync(req, ct);
        var responseBody = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            throw new PredictApiException(
                $"Auth failed: {(int)resp.StatusCode} - {responseBody}",
                (int)resp.StatusCode,
                responseBody
            );
        }

        // Parse response - try { data: { token } } wrapper first, then flat { token }
        string? token = null;
        try
        {
            var apiResp = JsonSerializer.Deserialize<ApiResponse<AuthTokenResponse>>(responseBody, _jsonOptions);
            token = apiResp?.Data?.Token;
        }
        catch { }

        if (string.IsNullOrEmpty(token))
        {
            try
            {
                var flatResp = JsonSerializer.Deserialize<AuthTokenResponse>(responseBody, _jsonOptions);
                token = flatResp?.Token;
            }
            catch { }
        }

        if (string.IsNullOrEmpty(token))
            throw new PredictApiException($"No token in auth response: {responseBody}", 0, responseBody);

        SetJwtToken(token);
        return token;
    }

    #endregion

    #region Markets

    public async Task<(List<Market> Markets, string? Cursor)> GetMarketsAsync(MarketsQuery? query = null, CancellationToken ct = default)
    {
        var queryParams = BuildQueryParams(query);
        var req = CreateRequest(HttpMethod.Get, $"/v1/markets?{queryParams}");

        var resp = await _httpClient.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            throw new PredictApiException($"Failed to get markets: {(int)resp.StatusCode}", (int)resp.StatusCode, body);
        }

        var result = JsonSerializer.Deserialize<ApiResponse<List<Market>>>(body, _jsonOptions);
        return (result?.Data ?? [], result?.Cursor);
    }

    public async Task<List<Market>> GetAllMarketsAsync(MarketsQuery? query = null, CancellationToken ct = default)
    {
        var allMarkets = new List<Market>();
        string? cursor = null;

        do
        {
            query ??= new MarketsQuery();
            query.After = cursor;
            query.First ??= 50;

            var (markets, nextCursor) = await GetMarketsAsync(query, ct);
            allMarkets.AddRange(markets);
            cursor = nextCursor;

            if (markets.Count < 50) break;
        } while (cursor != null);

        return allMarkets;
    }

    public async Task<Market> GetMarketByIdAsync(int marketId, CancellationToken ct = default)
    {
        var req = CreateRequest(HttpMethod.Get, $"/v1/markets/{marketId}");
        return await SendAsync<Market>(req, ct);
    }

    public async Task<OrderbookData> GetOrderbookAsync(int marketId, CancellationToken ct = default)
    {
        var req = CreateRequest(HttpMethod.Get, $"/v1/markets/{marketId}/orderbook");
        return await SendAsync<OrderbookData>(req, ct);
    }

    public async Task<MarketStatistics> GetMarketStatisticsAsync(int marketId, CancellationToken ct = default)
    {
        var req = CreateRequest(HttpMethod.Get, $"/v1/markets/{marketId}/statistics");
        return await SendAsync<MarketStatistics>(req, ct);
    }

    public async Task<object> GetMarketLastSaleAsync(int marketId, CancellationToken ct = default)
    {
        var req = CreateRequest(HttpMethod.Get, $"/v1/markets/{marketId}/last-sale");
        return await SendAsync<object>(req, ct);
    }

    public async Task<object> GetMarketTimeseriesAsync(int marketId, CancellationToken ct = default)
    {
        var req = CreateRequest(HttpMethod.Get, $"/v1/markets/{marketId}/timeseries");
        return await SendAsync<object>(req, ct);
    }

    public async Task<object> GetMarketLatestTimeseriesAsync(int marketId, CancellationToken ct = default)
    {
        var req = CreateRequest(HttpMethod.Get, $"/v1/markets/{marketId}/timeseries/latest");
        return await SendAsync<object>(req, ct);
    }

    #endregion

    #region Orders

    public async Task<(List<OrderEntry> Orders, string? Cursor)> GetOrdersAsync(OrdersQuery? query = null, CancellationToken ct = default)
    {
        var queryParams = BuildQueryParams(query);
        var req = CreateRequest(HttpMethod.Get, $"/v1/orders?{queryParams}", requireAuth: true);

        var resp = await _httpClient.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            throw new PredictApiException($"Failed to get orders: {(int)resp.StatusCode}", (int)resp.StatusCode, body);
        }

        var result = JsonSerializer.Deserialize<ApiResponse<List<OrderEntry>>>(body, _jsonOptions);
        return (result?.Data ?? [], result?.Cursor);
    }

    public async Task<List<OrderEntry>> GetAllOrdersAsync(OrdersQuery? query = null, CancellationToken ct = default)
    {
        var allOrders = new List<OrderEntry>();
        string? cursor = null;

        do
        {
            query ??= new OrdersQuery();
            query.After = cursor;
            query.First ??= 50;

            var (orders, nextCursor) = await GetOrdersAsync(query, ct);
            allOrders.AddRange(orders);
            cursor = nextCursor;

            if (orders.Count < 50) break;
        } while (cursor != null);

        return allOrders;
    }

    public async Task<OrderEntry> GetOrderByHashAsync(string hash, CancellationToken ct = default)
    {
        var req = CreateRequest(HttpMethod.Get, $"/v1/orders/{hash}", requireAuth: true);
        return await SendAsync<OrderEntry>(req, ct);
    }

    public async Task<object> GetOrderMatchEventsAsync(CancellationToken ct = default)
    {
        var req = CreateRequest(HttpMethod.Get, "/v1/orders/match-events", requireAuth: true);
        return await SendAsync<object>(req, ct);
    }

    public async Task<CreateOrderResponse> CreateOrderAsync(CreateOrderRequest orderRequest, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(orderRequest, _jsonOptions);
        var req = CreateRequest(HttpMethod.Post, "/v1/orders", requireAuth: true);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await SendAsync<CreateOrderResponse>(req, ct);
    }

    public async Task<RemoveOrdersResponse> RemoveOrdersAsync(List<string> orderIds, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new RemoveOrdersRequest
        {
            Data = new RemoveOrdersData { Ids = orderIds }
        }, _jsonOptions);

        var req = CreateRequest(HttpMethod.Post, "/v1/orders/remove", requireAuth: true);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        var result = await SendAsync<RemoveOrdersResponse>(req, ct);
        return new RemoveOrdersResponse
        {
            Removed = result?.Removed ?? [],
            Noop = result?.Noop ?? []
        };
    }

    #endregion

    #region Positions

    public async Task<(List<Position> Positions, string? Cursor)> GetPositionsAsync(PositionsQuery? query = null, CancellationToken ct = default)
    {
        var queryParams = BuildQueryParams(query);
        var req = CreateRequest(HttpMethod.Get, $"/v1/positions?{queryParams}", requireAuth: true);

        var resp = await _httpClient.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            throw new PredictApiException($"Failed to get positions: {(int)resp.StatusCode}", (int)resp.StatusCode, body);
        }

        var result = JsonSerializer.Deserialize<ApiResponse<List<Position>>>(body, _jsonOptions);
        return (result?.Data ?? [], result?.Cursor);
    }

    public async Task<List<Position>> GetAllPositionsAsync(PositionsQuery? query = null, CancellationToken ct = default)
    {
        var allPositions = new List<Position>();
        string? cursor = null;

        do
        {
            query ??= new PositionsQuery();
            query.After = cursor;
            query.First ??= 50;

            var (positions, nextCursor) = await GetPositionsAsync(query, ct);
            allPositions.AddRange(positions);
            cursor = nextCursor;

            if (positions.Count < 50) break;
        } while (cursor != null);

        return allPositions;
    }

    public async Task<(List<Position> Positions, string? Cursor)> GetPositionsByAddressAsync(string address, PositionsQuery? query = null, CancellationToken ct = default)
    {
        var queryParams = BuildQueryParams(query);
        var req = CreateRequest(HttpMethod.Get, $"/v1/positions/{address}?{queryParams}", requireAuth: true);

        var resp = await _httpClient.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            throw new PredictApiException($"Failed to get positions: {(int)resp.StatusCode}", (int)resp.StatusCode, body);
        }

        var result = JsonSerializer.Deserialize<ApiResponse<List<Position>>>(body, _jsonOptions);
        return (result?.Data ?? [], result?.Cursor);
    }

    #endregion

    #region Account

    public async Task<Account> GetConnectedAccountAsync(CancellationToken ct = default)
    {
        var req = CreateRequest(HttpMethod.Get, "/v1/account", requireAuth: true);
        return await SendAsync<Account>(req, ct);
    }

    public async Task<List<AccountActivity>> GetAccountActivityAsync(CancellationToken ct = default)
    {
        var req = CreateRequest(HttpMethod.Get, "/v1/account/activity", requireAuth: true);
        var result = await SendAsync<List<AccountActivity>>(req, ct);
        return result;
    }

    public async Task SetReferralAsync(string code, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new SetReferralRequest
        {
            Data = new SetReferralData { Code = code }
        }, _jsonOptions);

        var req = CreateRequest(HttpMethod.Post, "/v1/account/referral", requireAuth: true);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        await SendAsync(req, ct);
    }

    #endregion

    #region Categories

    public async Task<List<Category>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var req = CreateRequest(HttpMethod.Get, "/v1/categories");
        return await SendAsync<List<Category>>(req, ct);
    }

    public async Task<Category> GetCategoryBySlugAsync(string slug, CancellationToken ct = default)
    {
        var req = CreateRequest(HttpMethod.Get, $"/v1/categories/{slug}");
        return await SendAsync<Category>(req, ct);
    }

    public async Task<List<Tag>> GetAllTagsAsync(CancellationToken ct = default)
    {
        var req = CreateRequest(HttpMethod.Get, "/v1/tags");
        return await SendAsync<List<Tag>>(req, ct);
    }

    #endregion

    #region Search

    public async Task<SearchResult> SearchAsync(string query, CancellationToken ct = default)
    {
        var req = CreateRequest(HttpMethod.Get, $"/v1/search?query={Uri.EscapeDataString(query)}");
        return await SendAsync<SearchResult>(req, ct);
    }

    #endregion

    #region Helpers

    private static string BuildQueryParams(object? query)
    {
        if (query == null) return "";

        var pairs = new List<string>();
        var type = query.GetType();

        foreach (var prop in type.GetProperties())
        {
            var value = prop.GetValue(query);
            if (value == null) continue;

            var name = char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..];
            var strValue = value switch
            {
                bool b => b.ToString().ToLower(),
                _ => value.ToString()
            };

            pairs.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(strValue!)}");
        }

        return string.Join("&", pairs);
    }

    #endregion
}
