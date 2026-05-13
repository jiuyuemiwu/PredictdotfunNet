using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using PredictdotfunNet.Clients;
using PredictdotfunNet.Models;
using PredictdotfunNet.OrderBuilder;

namespace PredictdotfunNet;

public class PredictClient : IDisposable
{
    public PredictApiClient Api { get; }
    public PredictWsClient WebSocket { get; }
    public OrderBuilder.OrderBuilder? Builder { get; private set; }

    private readonly string? _privateKey;
    private readonly int _chainId;
    private readonly string? _rpcUrl;
    private readonly OrderBuilderOptions? _options;

    public PredictClient(string apiKey, string? privateKey = null, int chainId = 56, string? rpcUrl = null, OrderBuilderOptions? options = null)
    {
        _privateKey = privateKey;
        _chainId = chainId;
        _rpcUrl = rpcUrl;
        _options = options;

        Api = new PredictApiClient(apiKey);
        WebSocket = new PredictWsClient(apiKey);
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_privateKey != null)
        {
            Builder = await OrderBuilder.OrderBuilder.MakeAsync(_chainId, _privateKey, _rpcUrl, _options);
        }
    }

    public async Task<string> AuthenticateAsync(CancellationToken ct = default)
    {
        if (Builder == null && _privateKey == null)
            throw new InvalidOperationException("Private key is required for authentication");

        // If Builder not yet initialized, create it
        Builder ??= await OrderBuilder.OrderBuilder.MakeAsync(_chainId, _privateKey, _rpcUrl, _options);

        // Auth always uses the EOA address (from private key), not predictAccount.
        // The API verifies the EIP-191 signature and issues JWT for the EOA.
        // When predictAccount is set, the order signer is predictAccount but the API
        // verifies the Kernel-wrapped order signature separately against predictAccount.
        var ecKey = new EthECKey(_privateKey!);
        var signerAddress = ecKey.GetPublicAddress();

        // Step 1: Get auth message
        var message = await Api.GetAuthMessageAsync(ct);

        // Step 2: Sign the message using EIP-191 personal sign (with EOA)
        var signer = new EthereumMessageSigner();
        var signature = signer.EncodeUTF8AndSign(message, ecKey);

        // Step 3: Get JWT — signer is always the EOA address
        var token = await Api.AuthenticateAsync(signerAddress, signature, message, ct);

        // Set JWT on WebSocket client too
        WebSocket.SetJwt(token);

        return token;
    }

    public void Dispose()
    {
        WebSocket.Dispose();
    }
}
