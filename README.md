# PredictdotfunNet

A .NET 9 SDK for the [Predict.fun](https://predict.fun?ref=89BAA) prediction market protocol on BNB Chain.
Ported from the official [TypeScript SDK](https://github.com/PredictDotFun/sdk) and [Python SDK](https://github.com/PredictDotFun/sdk-python).

> 🎯 **Join [Predict.fun](https://predict.fun?ref=89BAA) and start trading prediction markets!**

## Installation

```bash
dotnet add package PredictdotfunNet
```

Or add to your `.csproj`:

```xml
<PackageReference Include="PredictdotfunNet" Version="1.0.0" />
```

## Quick Start

```csharp
using PredictdotfunNet;

// Create client with your API key and private key
var client = new PredictClient(
    apiKey: "YOUR_API_KEY",
    privateKey: "YOUR_PRIVATE_KEY",
    chainId: 56, // BNB Mainnet
    rpcUrl: "https://bsc-dataseed.binance.org"
);

// Initialize and authenticate
await client.InitializeAsync();
var jwt = await client.AuthenticateAsync();

// Get account info
var account = await client.Api.GetConnectedAccountAsync();
Console.WriteLine($"Points: {account.Points?.Total}");

// Subscribe to orderbook via WebSocket
client.WebSocket.OrderbookUpdate += (sender, e) =>
{
    Console.WriteLine($"Market {e.MarketId}: {e.Snapshot.Bids.Count} bids, {e.Snapshot.Asks.Count} asks");
};
await client.WebSocket.ConnectAsync();
await client.WebSocket.SubscribeOrderbookAsync(123);
```

## License

MIT
