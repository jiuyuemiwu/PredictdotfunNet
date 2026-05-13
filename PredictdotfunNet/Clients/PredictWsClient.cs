using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using PredictdotfunNet.Models;

namespace PredictdotfunNet.Clients;

public class PredictWsClient(string? apiKey = null) : IDisposable
{
    private const string WsEndpoint = "wss://ws.predict.fun/ws";
    private const int MaxReconnectAttempts = 20;
    private const int BaseReconnectDelayMs = 1000;
    private const int MaxReconnectDelayMs = 30000;
    private const int ConnectionTimeoutMs = 10000;
    private const int RecvBufferSize = 8192;

    private ClientWebSocket? _ws;
    private string? _jwt;
    private int _requestId;
    private int _reconnectAttempts;
    private bool _isConnecting;
    private bool _intentionalDisconnect;
    private bool _disposed;

    private readonly HashSet<int> _subscribedOrderbooks = [];
    private readonly object _subscriptionLock = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _walletSubscribed;
    private readonly ConcurrentDictionary<int, OrderbookSnapshot> _orderbookCache = new();

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public event EventHandler? Connected;
    public event EventHandler<(int Code, string Reason)>? Disconnected;
    public event EventHandler? MaxReconnectsReached;
    public event EventHandler<(int MarketId, OrderbookSnapshot Snapshot)>? OrderbookUpdate;
    public event EventHandler<(string EventType, WalletEvent Data)>? WalletEvent;
    public event EventHandler<(string FeedId, AssetPriceUpdate Data)>? AssetPriceUpdate;
    public event EventHandler<(string Topic, object? Data)>? Message;
    public event EventHandler<(int RequestId, bool Success, object? Data, WsError? Error)>? Response;
    public event EventHandler<Exception>? Error;

    public void SetJwt(string jwt) => _jwt = jwt;

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (IsConnected || _isConnecting) return;
        _isConnecting = true;
        _intentionalDisconnect = false;

        try
        {
            _ws?.Dispose();
            _ws = new ClientWebSocket();

            var url = apiKey != null
                ? $"{WsEndpoint}?apiKey={apiKey}"
                : WsEndpoint;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ConnectionTimeoutMs);

            await _ws.ConnectAsync(new Uri(url), cts.Token);

            _reconnectAttempts = 0;
            _isConnecting = false;

            Resubscribe();
            Connected?.Invoke(this, EventArgs.Empty);

            _ = ReceiveLoopAsync(ct);
        }
        catch
        {
            _isConnecting = false;
            throw;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[RecvBufferSize];
        var sb = new StringBuilder();

        try
        {
            while (_ws?.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                var messageText = sb.ToString();
                sb.Clear();

                try
                {
                    var msg = JsonSerializer.Deserialize<WsMessage>(messageText, _jsonOptions);
                    if (msg != null) HandleMessage(msg);
                }
                catch (JsonException ex)
                {
                    Error?.Invoke(this, ex);
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await DisconnectInternalAsync(1000, "Server close", ct);
                    return;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex)
        {
            Error?.Invoke(this, ex);
        }
        finally
        {
            if (!_disposed && !_intentionalDisconnect && !ct.IsCancellationRequested && _ws?.State != WebSocketState.Open)
            {
                Disconnected?.Invoke(this, (0, "Connection lost"));
                ScheduleReconnect(ct);
            }
        }
    }

    private void HandleMessage(WsMessage msg)
    {
        if (msg.Type == "M")
        {
            var topic = msg.Topic ?? "";

            if (topic == "heartbeat")
            {
                if (msg.Data is JsonElement elem && elem.ValueKind == JsonValueKind.Number)
                {
                    SendHeartbeat(elem.GetInt64());
                }
                return;
            }

            if (topic.StartsWith("predictOrderbook/"))
            {
                var marketIdStr = topic["predictOrderbook/".Length..];
                if (int.TryParse(marketIdStr, out var marketId))
                {
                    var snapshot = DeserializeData<OrderbookSnapshot>(msg.Data);
                    if (snapshot != null)
                    {
                        snapshot.MarketId = marketId;
                        _orderbookCache[marketId] = snapshot;
                        OrderbookUpdate?.Invoke(this, (marketId, snapshot));
                    }
                }
                return;
            }

            if (topic.StartsWith("predictWalletEvents/"))
            {
                var eventData = DeserializeData<WalletEvent>(msg.Data);
                if (eventData != null)
                {
                    var eventType = eventData.EventType ?? eventData.Type ?? "unknown";
                    WalletEvent?.Invoke(this, (eventType, eventData));
                }
                return;
            }

            if (topic.StartsWith("assetPriceUpdate/"))
            {
                var feedId = topic["assetPriceUpdate/".Length..];
                var priceData = DeserializeData<AssetPriceUpdate>(msg.Data);
                if (priceData != null)
                {
                    AssetPriceUpdate?.Invoke(this, (feedId, priceData));
                }
                return;
            }

            Message?.Invoke(this, (topic, msg.Data));
        }
        else if (msg.Type == "R")
        {
            Response?.Invoke(this, (msg.RequestId ?? 0, msg.Success ?? false, msg.Data, msg.Error));
        }
    }

    private T? DeserializeData<T>(object? data)
    {
        if (data is JsonElement elem)
        {
            return JsonSerializer.Deserialize<T>(elem.GetRawText(), _jsonOptions);
        }
        if (data is T typed) return typed;
        return default;
    }

    private void SendHeartbeat(long timestamp)
    {
        _ = SendAsync(new WsHeartbeatRequest { Data = timestamp }).ContinueWith(task =>
        {
            if (task.Exception != null)
                Error?.Invoke(this, task.Exception.GetBaseException());
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task SendAsync(object payload, CancellationToken ct = default)
    {
        if (_ws?.State != WebSocketState.Open) return;

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(ct);
        try
        {
            if (_ws?.State != WebSocketState.Open) return;
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task SubscribeOrderbookAsync(int marketId, CancellationToken ct = default)
    {
        lock (_subscriptionLock)
        {
            if (!_subscribedOrderbooks.Add(marketId))
                return;
        }

        try
        {
            await SendSubscribeOrderbookAsync(marketId, ct);
        }
        catch
        {
            lock (_subscriptionLock)
                _subscribedOrderbooks.Remove(marketId);
            throw;
        }
    }

    private async Task SendSubscribeOrderbookAsync(int marketId, CancellationToken ct = default)
    {
        var topic = $"predictOrderbook/{marketId}";
        var reqId = NextRequestId();

        await SendAsync(new WsRequest
        {
            Method = "subscribe",
            RequestId = reqId,
            Params = [topic]
        }, ct);
    }

    public async Task UnsubscribeOrderbookAsync(int marketId, CancellationToken ct = default)
    {
        lock (_subscriptionLock)
        {
            if (!_subscribedOrderbooks.Remove(marketId))
                return;
        }

        var topic = $"predictOrderbook/{marketId}";
        var reqId = NextRequestId();

        await SendAsync(new WsRequest
        {
            Method = "unsubscribe",
            RequestId = reqId,
            Params = [topic]
        }, ct);

        _orderbookCache.TryRemove(marketId, out _);
    }

    public async Task SubscribeWalletEventsAsync(CancellationToken ct = default)
    {
        if (_jwt == null) throw new InvalidOperationException("JWT token required for wallet events subscription");

        var topic = $"predictWalletEvents/{_jwt}";
        var reqId = NextRequestId();

        await SendAsync(new WsRequest
        {
            Method = "subscribe",
            RequestId = reqId,
            Params = [topic]
        }, ct);

        _walletSubscribed = true;
    }

    public async Task SubscribeAssetPriceAsync(string priceFeedId, CancellationToken ct = default)
    {
        var topic = $"assetPriceUpdate/{priceFeedId}";
        var reqId = NextRequestId();

        await SendAsync(new WsRequest
        {
            Method = "subscribe",
            RequestId = reqId,
            Params = [topic]
        }, ct);
    }

    public OrderbookSnapshot? GetCachedOrderbook(int marketId)
    {
        return _orderbookCache.TryGetValue(marketId, out var snapshot) ? snapshot : null;
    }

    public List<int> GetSubscribedMarketIds()
    {
        lock (_subscriptionLock)
            return [.. _subscribedOrderbooks];
    }

    private void Resubscribe()
    {
        List<int> subscribedOrderbooks;
        lock (_subscriptionLock)
            subscribedOrderbooks = [.. _subscribedOrderbooks];

        foreach (var marketId in subscribedOrderbooks)
        {
            _ = SendSubscribeOrderbookAsync(marketId);
        }

        if (_walletSubscribed && _jwt != null)
        {
            _ = SubscribeWalletEventsAsync();
        }
    }

    private void ScheduleReconnect(CancellationToken ct)
    {
        if (_reconnectAttempts >= MaxReconnectAttempts)
        {
            MaxReconnectsReached?.Invoke(this, EventArgs.Empty);
            return;
        }

        var delay = Math.Min(BaseReconnectDelayMs * (1 << _reconnectAttempts), MaxReconnectDelayMs);
        _reconnectAttempts++;

        _ = Task.Delay(delay, ct).ContinueWith(async _ =>
        {
            try
            {
                await ConnectAsync(ct);
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, ex);
            }
        }, ct);
    }

    private async Task DisconnectInternalAsync(int code, string reason, CancellationToken ct = default)
    {
        if (_ws?.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync((WebSocketCloseStatus)code, reason, ct);
            }
            catch { }
        }

        Disconnected?.Invoke(this, (code, reason));
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        _intentionalDisconnect = true;
        lock (_subscriptionLock)
            _subscribedOrderbooks.Clear();
        _orderbookCache.Clear();
        _walletSubscribed = false;

        await DisconnectInternalAsync(1000, "Client disconnect", ct);

        _ws?.Dispose();
        _ws = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _intentionalDisconnect = true;

        _ws?.Dispose();
        _ws = null;
        _sendLock.Dispose();
    }

    private int NextRequestId() => Interlocked.Increment(ref _requestId);
}
