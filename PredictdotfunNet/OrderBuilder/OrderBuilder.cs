using System.Numerics;
using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using PredictdotfunNet.Models;

namespace PredictdotfunNet.OrderBuilder;

public partial class OrderBuilder
{
    private readonly int _chainId;
    private readonly Nethereum.Web3.Accounts.Account? _account;
    private readonly Web3 _web3;
    private readonly string? _predictAccount;
    private readonly string _makerAddress;
    private static readonly BigInteger Precision = BigInteger.Pow(10, 18);
    private const decimal DecimalPrecision = 1_000_000_000_000_000_000m;
    private static readonly BigInteger MaxUint256 = BigInteger.Pow(2, 256) - 1;
    private static readonly BigInteger MaxInt256 = BigInteger.Pow(2, 255) - 1;

    private OrderBuilder(int chainId, Nethereum.Web3.Accounts.Account? account, string rpcUrl, string? predictAccount)
    {
        _chainId = chainId;
        _account = account;
        _predictAccount = NormalizeOptionalAddress(predictAccount);
        _makerAddress = _predictAccount ?? account?.Address ?? "";
        _web3 = new Web3(account, rpcUrl);
    }

    public string MakerAddress => _makerAddress;
    public string? PredictAccount => _predictAccount;

    public static async Task<OrderBuilder> MakeAsync(
        int chainId,
        string? privateKey = null,
        string? rpcUrl = null,
        OrderBuilderOptions? options = null)
    {
        rpcUrl ??= Constants.ChainRpcUrls.GetValueOrDefault(chainId, Constants.BnbMainnetRpcUrl);

        Nethereum.Web3.Accounts.Account? account = null;
        if (privateKey != null)
        {
            if (!privateKey.StartsWith("0x")) privateKey = "0x" + privateKey;
            account = new Nethereum.Web3.Accounts.Account(privateKey, chainId);
        }

        var builder = new OrderBuilder(chainId, account, rpcUrl, options?.PredictAccount);

        if (account != null)
        {
            // Nethereum v6 handles nonce management internally via NonceService
            var web3 = new Web3(account, rpcUrl);
            // Initialize the nonce service
            await web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(account.Address);
        }

        return builder;
    }

    #region Order Amounts

    public OrderAmounts GetLimitOrderAmounts(LimitHelperInput input)
    {
        var pricePerShareWei = RetainSignificantDigits(BigInteger.Parse(input.PricePerShareWei), 3);
        var quantityWei = RetainSignificantDigits(BigInteger.Parse(input.QuantityWei), 5);

        if (quantityWei < BigInteger.Parse("10000000000000000"))
            throw new InvalidOperationException("Quantity must be at least 0.01 shares");

        BigInteger makerAmount, takerAmount;

        if (input.Side == Side.BUY)
        {
            makerAmount = pricePerShareWei * quantityWei / BigInteger.Pow(10, 18);
            takerAmount = quantityWei;
        }
        else
        {
            makerAmount = quantityWei;
            takerAmount = pricePerShareWei * quantityWei / BigInteger.Pow(10, 18);
        }

        var lastPrice = makerAmount * BigInteger.Pow(10, 18) / takerAmount;
        var pricePerShare = pricePerShareWei.ToString();

        return new OrderAmounts
        {
            LastPrice = lastPrice.ToString(),
            PricePerShare = pricePerShare,
            MakerAmount = makerAmount.ToString(),
            TakerAmount = takerAmount.ToString()
        };
    }

    public OrderAmounts GetMarketOrderAmounts(MarketHelperInput input, OrderbookData book)
    {
        return CalculateMarketAmounts(input.Side, input.QuantityWei, null, book, input.SlippageBps);
    }

    public OrderAmounts GetMarketOrderAmounts(MarketHelperValueInput input, OrderbookData book)
    {
        return CalculateMarketAmounts(input.Side, null, input.ValueWei, book, input.SlippageBps);
    }

    private OrderAmounts CalculateMarketAmounts(Side side, string? quantityWeiStr, string? valueWeiStr, OrderbookData book, string? slippageBpsStr)
    {
        var slippageBps = slippageBpsStr != null ? BigInteger.Parse(slippageBpsStr) : BigInteger.Zero;

        if (quantityWeiStr != null)
        {
            var requestedQuantityWei = RetainSignificantDigits(BigInteger.Parse(quantityWeiStr), 5);
            if (requestedQuantityWei < BigInteger.Parse("10000000000000000"))
                throw new InvalidOperationException("Quantity must be at least 0.01 shares");

            var relevantOrders = side == Side.BUY ? book.Asks : book.Bids;
            var processed = ProcessBook(relevantOrders, requestedQuantityWei);
            var pricePerShareWei = processed.QuantityWei > 0
                ? processed.PriceWei / processed.QuantityWei
                : BigInteger.Zero;

            if (side == Side.BUY)
            {
                var baseMakerAmount = processed.LastPriceWei * processed.QuantityWei / Precision;
                var makerAmount = slippageBps > 0
                    ? BigInteger.Min(baseMakerAmount * (10_000 + slippageBps) / 10_000, processed.QuantityWei)
                    : baseMakerAmount;

                return new OrderAmounts
                {
                    LastPrice = processed.LastPriceWei.ToString(),
                    PricePerShare = pricePerShareWei.ToString(),
                    MakerAmount = makerAmount.ToString(),
                    TakerAmount = processed.QuantityWei.ToString(),
                    Amount = processed.QuantityWei.ToString(),
                    SlippageBps = slippageBps > 0 ? slippageBps.ToString() : null,
                    IsMinAmountOut = slippageBps > 0 ? false : null
                };
            }

            var baseTakerAmount = processed.LastPriceWei * processed.QuantityWei / Precision;
            var takerAmount = slippageBps > 0
                ? BigInteger.Max(baseTakerAmount * (10_000 - slippageBps) / 10_000, BigInteger.Zero)
                : baseTakerAmount;

            return new OrderAmounts
            {
                LastPrice = processed.LastPriceWei.ToString(),
                PricePerShare = pricePerShareWei.ToString(),
                MakerAmount = processed.QuantityWei.ToString(),
                TakerAmount = takerAmount.ToString(),
                Amount = processed.QuantityWei.ToString(),
                SlippageBps = slippageBps > 0 ? slippageBps.ToString() : null,
                IsMinAmountOut = slippageBps > 0 ? false : null
            };
        }

        if (valueWeiStr != null)
        {
            if (side != Side.BUY)
                throw new ArgumentException("Market order by value is only supported for BUY orders");

            var sharesWei = RetainSignificantDigits(CalculateMarketBuySharesByValue(BigInteger.Parse(valueWeiStr), book.Asks), 5);
            return CalculateMarketAmounts(Side.BUY, sharesWei.ToString(), null, book, slippageBpsStr);
        }

        throw new ArgumentException("Either quantityWei or valueWei must be provided");
    }

    private static ProcessedBookAmounts ProcessBook(List<List<double>> levels, BigInteger requestedQuantityWei)
    {
        if (levels.Count == 0)
            throw new InvalidOperationException("No orders in the orderbook to calculate market order amounts");

        var result = new ProcessedBookAmounts();
        foreach (var level in levels.Where(level => level.Count >= 2 && level[0] > 0 && level[1] > 0))
        {
            var remainingQuantityWei = requestedQuantityWei - result.QuantityWei;
            if (remainingQuantityWei <= 0)
                break;

            var priceWei = ToWeiBigInteger(level[0]);
            var levelQuantityWei = ToWeiBigInteger(level[1]);
            if (priceWei <= 0 || levelQuantityWei <= 0)
                continue;

            var fillQuantityWei = levelQuantityWei >= remainingQuantityWei ? remainingQuantityWei : levelQuantityWei;
            result.QuantityWei += fillQuantityWei;
            result.PriceWei += priceWei * fillQuantityWei;
            result.LastPriceWei = priceWei;
        }

        if (result.QuantityWei <= 0)
            throw new InvalidOperationException("No orders in the orderbook to calculate market order amounts");

        return result;
    }

    private static BigInteger CalculateMarketBuySharesByValue(BigInteger valueWei, List<List<double>> asks)
    {
        if (asks.Count == 0)
            throw new InvalidOperationException("No orders in the orderbook to calculate market order amounts");

        var sharesWei = BigInteger.Zero;
        var totalPriceWei = BigInteger.Zero;

        foreach (var level in asks.Where(level => level.Count >= 2 && level[0] > 0 && level[1] > 0))
        {
            var remainingSpendWei = valueWei - totalPriceWei;
            if (remainingSpendWei <= 0)
                break;

            var priceWei = ToWeiBigInteger(level[0]);
            var levelQuantityWei = ToWeiBigInteger(level[1]);
            if (priceWei <= 0 || levelQuantityWei <= 0)
                continue;

            var tierTotalPriceWei = priceWei * levelQuantityWei / Precision;
            if (tierTotalPriceWei <= remainingSpendWei)
            {
                sharesWei += levelQuantityWei;
                totalPriceWei += tierTotalPriceWei;
                continue;
            }

            var fractionalSharesWei = remainingSpendWei * Precision / priceWei;
            sharesWei += fractionalSharesWei;
            totalPriceWei += priceWei * fractionalSharesWei / Precision;
            break;
        }

        if (sharesWei <= 0)
            throw new InvalidOperationException("No orders in the orderbook to calculate market order amounts");

        return sharesWei;
    }

    private static BigInteger ToWeiBigInteger(double value)
    {
        if (value <= 0)
            return BigInteger.Zero;

        return new BigInteger((decimal)value * DecimalPrecision);
    }

    private sealed class ProcessedBookAmounts
    {
        public BigInteger QuantityWei { get; set; }
        public BigInteger PriceWei { get; set; }
        public BigInteger LastPriceWei { get; set; }
    }

    #endregion

    #region Build Order

    public Models.Order BuildOrder(string strategy, BuildOrderInput input)
    {
        var salt = input.Salt ?? GenerateSalt();
        var side = (int)input.Side;
        var normalizedStrategy = strategy.ToUpperInvariant();
        var now = DateTimeOffset.UtcNow;
        var expiration = normalizedStrategy == "MARKET"
            ? now.AddMinutes(5).ToUnixTimeSeconds()
            : (input.ExpiresAt ?? new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero)).ToUnixTimeSeconds();

        if (normalizedStrategy != "MARKET" && expiration <= now.ToUnixTimeSeconds())
            throw new InvalidOperationException("Limit order expiration must be in the future");

        // Match Python SDK: when predictAccount is set, it overrides maker and signer
        var signerAddress = _account?.Address ?? input.Signer;
        var effectiveMaker = _predictAccount ?? input.Maker ?? signerAddress;
        var effectiveSigner = _predictAccount ?? signerAddress;

        return new Models.Order
        {
            Salt = salt,
            Maker = effectiveMaker,
            Signer = effectiveSigner,
            Taker = "0x0000000000000000000000000000000000000000",
            TokenId = input.TokenId,
            MakerAmount = input.MakerAmount,
            TakerAmount = input.TakerAmount,
            Expiration = expiration,
            Nonce = input.Nonce.ToString(),
            FeeRateBps = input.FeeRateBps.ToString(),
            Side = side,
            SignatureType = (int)SignatureType.EOA,
            Signature = ""
        };
    }

    private static string GenerateSalt()
    {
        return System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, int.MaxValue).ToString();
    }

    #endregion

    #region Typed Data & Signing

    public Eip712TypedData BuildTypedData(Models.Order order, bool isNegRisk, bool isYieldBearing)
    {
        var verifyingContract = Constants.GetVerifyingContract(isNegRisk, isYieldBearing);

        var domain = new EIP712Domain
        {
            Name = Constants.Eip712DomainName,
            Version = Constants.Eip712DomainVersion,
            ChainId = _chainId,
            VerifyingContract = verifyingContract
        };

        var message = new Dictionary<string, object>
        {
            ["salt"] = order.Salt,
            ["maker"] = order.Maker,
            ["signer"] = order.Signer,
            ["taker"] = order.Taker,
            ["tokenId"] = order.TokenId,
            ["makerAmount"] = order.MakerAmount,
            ["takerAmount"] = order.TakerAmount,
            ["expiration"] = order.Expiration,
            ["nonce"] = order.Nonce,
            ["feeRateBps"] = order.FeeRateBps,
            ["side"] = order.Side,
            ["signatureType"] = order.SignatureType
        };

        return new Eip712TypedData
        {
            Domain = domain,
            Message = message
        };
    }

    public string BuildTypedDataHash(Eip712TypedData typedData)
    {
        var domainSeparator = ComputeDomainSeparator(typedData.Domain);
        var messageHash = ComputeStructHash(typedData.Message);

        // EIP-712: keccak256("\x19\x01" || domainSeparator || messageHash)
        var encoded = new byte[2 + 32 + 32];
        encoded[0] = 0x19;
        encoded[1] = 0x01;
        Array.Copy(domainSeparator, 0, encoded, 2, 32);
        Array.Copy(messageHash, 0, encoded, 34, 32);

        return "0x" + Sha3Keccack.Current.CalculateHash(encoded).ToHex();
    }

    /// <summary>
    /// Sign a plain text message using Kernel-wrapped signing (for predictAccount).
    /// Matches Python SDK sign_predict_account_message(string): hashMessage → eip712WrapHash → personalSign → prefix with validator.
    /// </summary>
    public string SignPredictAccountMessage(string message)
    {
        if (_account == null)
            throw new InvalidOperationException("Signer is required to sign orders");
        if (_predictAccount == null)
            throw new InvalidOperationException("Predict account is required");

        // Step 1: hashMessage (EIP-191 personal sign hash) — same as ethers hashMessage() / Python _hash_eip191_message
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var messageHash = new EthereumMessageSigner().HashPrefixedMessage(messageBytes);

        // Step 2: eip712WrapHash with Kernel domain (verifyingContract = predictAccount)
        var messageHashHex = "0x" + messageHash.ToHex();
        return SignPredictAccountRawHash(messageHashHex);
    }

    /// <summary>
    /// Sign a raw hash using Kernel-wrapped signing (for predictAccount).
    /// Matches Python SDK sign_predict_account_message({"raw": hash}): eip712WrapHash → personalSign → prefix with validator.
    /// Used for signing EIP-712 typed data hashes for orders.
    /// </summary>
    public string SignPredictAccountRawHash(string rawHash)
    {
        if (_account == null)
            throw new InvalidOperationException("Signer is required to sign orders");
        if (_predictAccount == null)
            throw new InvalidOperationException("Predict account is required");

        // Step 1: eip712WrapHash with Kernel domain (verifyingContract = predictAccount)
        var digest = BuildKernelWrappedHash(rawHash, _predictAccount);

        // Step 2: Sign the digest with EIP-191 personal sign (same as Python encode_defunct + sign_message)
        var signature = new EthereumMessageSigner().Sign(digest.HexToByteArray(), new EthECKey(_account.PrivateKey));

        // Step 3: Prefix with 0x01 + ECDSA validator address
        return "0x01" + RemoveHexPrefix(Constants.Addresses.EcdsaValidator) + RemoveHexPrefix(signature);
    }

    private string BuildKernelWrappedHash(string messageHash, string verifyingContract)
    {
        var domainSeparator = ComputeDomainSeparator(new EIP712Domain
        {
            Name = Constants.KernelDomainName,
            Version = Constants.KernelDomainVersion,
            ChainId = _chainId,
            VerifyingContract = verifyingContract
        });
        var kernelMessageHash = ComputeKernelMessageHash(messageHash);

        var encoded = new byte[2 + 32 + 32];
        encoded[0] = 0x19;
        encoded[1] = 0x01;
        Array.Copy(domainSeparator, 0, encoded, 2, 32);
        Array.Copy(kernelMessageHash, 0, encoded, 34, 32);

        return "0x" + Sha3Keccack.Current.CalculateHash(encoded).ToHex();
    }

    private static byte[] ComputeKernelMessageHash(string messageHash)
    {
        var hashBytes = messageHash.HexToByteArray();
        if (hashBytes.Length != 32)
            throw new InvalidOperationException("Predict account message hash must be 32 bytes");

        var typeHash = KeccakUTF8("Kernel(bytes32 hash)");
        return Sha3Keccack.Current.CalculateHash(typeHash.Concat(hashBytes).ToArray());
    }

    private static byte[] KeccakUTF8(string input)
    {
        return Sha3Keccack.Current.CalculateHash(Encoding.UTF8.GetBytes(input));
    }

    private static byte[] ComputeDomainSeparator(EIP712Domain domain)
    {
        var domainTypeHash = KeccakUTF8(
            "EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)");

        var nameHash = KeccakUTF8(domain.Name);
        var versionHash = KeccakUTF8(domain.Version);
        var chainIdBytes = ((BigInteger)domain.ChainId).ToBigEndianByteArray().PadLeft(32);
        var verifyingContractBytes = domain.VerifyingContract.HexToByteArray().PadLeft(32);

        return Sha3Keccack.Current.CalculateHash(
            domainTypeHash.Concat(nameHash).Concat(versionHash)
                .Concat(chainIdBytes).Concat(verifyingContractBytes).ToArray());
    }

    private static byte[] ComputeStructHash(Dictionary<string, object> message)
    {
        var orderTypeHash = KeccakUTF8(
            "Order(uint256 salt,address maker,address signer,address taker,uint256 tokenId," +
            "uint256 makerAmount,uint256 takerAmount,uint256 expiration,uint256 nonce," +
            "uint256 feeRateBps,uint8 side,uint8 signatureType)");

        var components = new List<byte[]> { orderTypeHash };

        // salt (uint256)
        var saltStr = message["salt"].ToString()!;
        components.Add(ParseHexOrDecimalToBigEndian(saltStr).PadLeft(32));
        // maker (address)
        components.Add(message["maker"].ToString()!.HexToByteArray().PadLeft(32));
        // signer (address)
        components.Add(message["signer"].ToString()!.HexToByteArray().PadLeft(32));
        // taker (address)
        components.Add(message["taker"].ToString()!.HexToByteArray().PadLeft(32));
        // tokenId (uint256)
        components.Add(ParseHexOrDecimalToBigEndian(message["tokenId"].ToString()!).PadLeft(32));
        // makerAmount (uint256)
        components.Add(BigInteger.Parse(message["makerAmount"].ToString()!).ToBigEndianByteArray().PadLeft(32));
        // takerAmount (uint256)
        components.Add(BigInteger.Parse(message["takerAmount"].ToString()!).ToBigEndianByteArray().PadLeft(32));
        // expiration (uint256)
        components.Add(BigInteger.Parse(message["expiration"].ToString()!).ToBigEndianByteArray().PadLeft(32));
        // nonce (uint256)
        components.Add(BigInteger.Parse(message["nonce"].ToString()!).ToBigEndianByteArray().PadLeft(32));
        // feeRateBps (uint256)
        components.Add(BigInteger.Parse(message["feeRateBps"].ToString()!).ToBigEndianByteArray().PadLeft(32));
        // side (uint8)
        components.Add(new byte[31].Concat(new byte[] { Convert.ToByte(message["side"]) }).ToArray());
        // signatureType (uint8)
        components.Add(new byte[31].Concat(new byte[] { Convert.ToByte(message["signatureType"]) }).ToArray());

        return Sha3Keccack.Current.CalculateHash(components.SelectMany(c => c).ToArray());
    }

    private static byte[] ParseHexOrDecimalToBigEndian(string value)
    {
        if (value.StartsWith("0x") || value.StartsWith("0X"))
        {
            return BigInteger.Parse(value[2..], System.Globalization.NumberStyles.HexNumber).ToBigEndianByteArray();
        }
        return BigInteger.Parse(value).ToBigEndianByteArray();
    }

    private static string? NormalizeOptionalAddress(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string RemoveHexPrefix(string value)
    {
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
    }

    private static BigInteger RetainSignificantDigits(BigInteger value, int significantDigits)
    {
        if (value.IsZero) return BigInteger.Zero;

        var sign = value.Sign < 0 ? -1 : 1;
        var absValue = BigInteger.Abs(value);
        var excessDigits = absValue.ToString().Length - significantDigits;
        if (excessDigits <= 0) return value;

        var divisor = BigInteger.Pow(10, excessDigits);
        var truncated = absValue / divisor * divisor;
        return sign < 0 ? -truncated : truncated;
    }

    public SignedOrder SignTypedDataOrder(Eip712TypedData typedData)
    {
        if (_account == null)
            throw new InvalidOperationException("Signer is required to sign orders");

        var typedDataHash = BuildTypedDataHash(typedData);
        var hashBytes = typedDataHash.HexToByteArray();

        var signature = _predictAccount == null
            ? new MessageSigner().Sign(hashBytes, new EthECKey(_account.PrivateKey))
            : SignPredictAccountRawHash(typedDataHash);

        var message = typedData.Message;
        return new SignedOrder
        {
            Salt = message["salt"].ToString()!,
            Maker = message["maker"].ToString()!,
            Signer = message["signer"].ToString()!,
            Taker = message["taker"].ToString()!,
            TokenId = message["tokenId"].ToString()!,
            MakerAmount = message["makerAmount"].ToString()!,
            TakerAmount = message["takerAmount"].ToString()!,
            Expiration = Convert.ToInt64(message["expiration"]),
            Nonce = message["nonce"].ToString()!,
            FeeRateBps = message["feeRateBps"].ToString()!,
            Side = Convert.ToInt32(message["side"]),
            SignatureType = Convert.ToInt32(message["signatureType"]),
            Signature = signature
        };
    }

    #endregion

    #region Approvals

    public async Task<SetApprovalsResult> SetApprovalsAsync(bool isYieldBearing = false, CancellationToken ct = default)
    {
        if (_account == null)
            throw new InvalidOperationException("Signer is required for approvals");

        var results = new List<TransactionResult>();

        try
        {
            results.Add(await SetCtfExchangeApprovalAsync(false, isYieldBearing, true, ct));
        }
        catch (Exception ex)
        {
            results.Add(new TransactionResult { Success = false, Cause = ex.Message });
        }

        try
        {
            results.Add(await SetCtfExchangeAllowanceAsync(false, isYieldBearing, ct: ct));
        }
        catch (Exception ex)
        {
            results.Add(new TransactionResult { Success = false, Cause = ex.Message });
        }

        try
        {
            results.Add(await SetCtfExchangeApprovalAsync(true, isYieldBearing, true, ct));
        }
        catch (Exception ex)
        {
            results.Add(new TransactionResult { Success = false, Cause = ex.Message });
        }

        try
        {
            results.Add(await SetCtfExchangeAllowanceAsync(true, isYieldBearing, ct: ct));
        }
        catch (Exception ex)
        {
            results.Add(new TransactionResult { Success = false, Cause = ex.Message });
        }

        try
        {
            results.Add(await SetNegRiskAdapterApprovalAsync(isYieldBearing, true, ct));
        }
        catch (Exception ex)
        {
            results.Add(new TransactionResult { Success = false, Cause = ex.Message });
        }

        return new SetApprovalsResult
        {
            Success = results.All(r => r.Success),
            Transactions = results
        };
    }

    public async Task<TransactionResult> SetCtfExchangeApprovalAsync(bool isNegRisk, bool isYieldBearing, bool approved = true, CancellationToken ct = default)
    {
        if (_account == null) throw new InvalidOperationException("Signer is required");

        var ctfAddress = Constants.GetConditionalTokensAddress(isNegRisk, isYieldBearing);
        var exchangeAddress = Constants.GetExchangeAddress(isNegRisk, isYieldBearing);

        var abi = GetConditionalTokensAbi();
        var contract = _web3.Eth.GetContract(abi, ctfAddress);
        var isApprovedForAll = contract.GetFunction("isApprovedForAll");
        var currentApproval = await isApprovedForAll.CallAsync<bool>(_makerAddress, exchangeAddress);
        if (currentApproval == approved)
            return new TransactionResult { Success = true };

        var setApprovalForAll = contract.GetFunction("setApprovalForAll");
        var callData = setApprovalForAll.GetData(exchangeAddress, approved);

        if (_predictAccount != null)
            return await ExecuteKernelAsync(ctfAddress, callData, ct);

        var gas = new Nethereum.Hex.HexTypes.HexBigInteger(60000);
        var txHash = await setApprovalForAll.SendTransactionAsync(_account.Address, gas, null, null, exchangeAddress, approved);
        var receipt = await WaitForTransactionReceiptAsync(txHash, ct);

        return new TransactionResult
        {
            Success = receipt.Status?.Value == 1,
            Receipt = txHash,
            Cause = receipt.Status?.Value == 1 ? null : $"Transaction failed: {txHash}"
        };
    }

    public async Task<TransactionResult> SetNegRiskAdapterApprovalAsync(bool isYieldBearing, bool approved = true, CancellationToken ct = default)
    {
        if (_account == null) throw new InvalidOperationException("Signer is required");

        var ctfAddress = Constants.GetConditionalTokensAddress(true, isYieldBearing);
        var adapterAddress = Constants.GetNegRiskAdapterAddress(isYieldBearing);

        var abi = GetConditionalTokensAbi();
        var contract = _web3.Eth.GetContract(abi, ctfAddress);
        var isApprovedForAll = contract.GetFunction("isApprovedForAll");
        var currentApproval = await isApprovedForAll.CallAsync<bool>(_makerAddress, adapterAddress);
        if (currentApproval == approved)
            return new TransactionResult { Success = true };

        var setApprovalForAll = contract.GetFunction("setApprovalForAll");
        var callData = setApprovalForAll.GetData(adapterAddress, approved);

        if (_predictAccount != null)
            return await ExecuteKernelAsync(ctfAddress, callData, ct);

        var gas = new Nethereum.Hex.HexTypes.HexBigInteger(60000);
        var txHash = await setApprovalForAll.SendTransactionAsync(_account.Address, gas, null, null, adapterAddress, approved);
        var receipt = await WaitForTransactionReceiptAsync(txHash, ct);

        return new TransactionResult
        {
            Success = receipt.Status?.Value == 1,
            Receipt = txHash,
            Cause = receipt.Status?.Value == 1 ? null : $"Transaction failed: {txHash}"
        };
    }

    public async Task<TransactionResult> SetCtfExchangeAllowanceAsync(
        bool isNegRisk,
        bool isYieldBearing,
        BigInteger? minAmount = null,
        BigInteger? maxAmount = null,
        CancellationToken ct = default)
    {
        if (_account == null) throw new InvalidOperationException("Signer is required");

        var exchangeAddress = Constants.GetExchangeAddress(isNegRisk, isYieldBearing);
        minAmount ??= MaxInt256;
        maxAmount ??= MaxUint256;

        var abi = GetErc20Abi();
        var contract = _web3.Eth.GetContract(abi, Constants.Addresses.Usdt);
        var allowance = contract.GetFunction("allowance");
        var currentAllowance = await allowance.CallAsync<BigInteger>(_makerAddress, exchangeAddress);
        if (currentAllowance >= minAmount.Value)
            return new TransactionResult { Success = true };

        var approve = contract.GetFunction("approve");
        var callData = approve.GetData(exchangeAddress, maxAmount.Value);

        if (_predictAccount != null)
            return await ExecuteKernelAsync(Constants.Addresses.Usdt, callData, ct);

        var gas = new Nethereum.Hex.HexTypes.HexBigInteger(60000);
        var txHash = await approve.SendTransactionAsync(_account.Address, gas, null, null, exchangeAddress, maxAmount.Value);
        var receipt = await WaitForTransactionReceiptAsync(txHash, ct);

        return new TransactionResult
        {
            Success = receipt.Status?.Value == 1,
            Receipt = txHash,
            Cause = receipt.Status?.Value == 1 ? null : $"Transaction failed: {txHash}"
        };
    }

    #endregion

    #region Transaction Helpers

    private async Task<Nethereum.RPC.Eth.DTOs.TransactionReceipt> WaitForTransactionReceiptAsync(
        string txHash,
        CancellationToken ct,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(120));
        var delay = pollInterval ?? TimeSpan.FromSeconds(2);

        while (DateTimeOffset.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
            if (receipt != null)
                return receipt;

            await Task.Delay(delay, ct);
        }

        throw new TimeoutException($"Timed out waiting for transaction receipt: {txHash}");
    }

    #endregion

    #region Kernel Execute

    /// <summary>
    /// Execute a contract call through the Predict Account Kernel smart wallet.
    /// The execution calldata format mirrors the official TS/Python SDK:
    /// target address (20 bytes) + value (32 bytes) + raw call data.
    /// </summary>
    private async Task<TransactionResult> ExecuteKernelAsync(string target, string callDataHex, CancellationToken ct = default)
    {
        if (_account == null) throw new InvalidOperationException("Signer is required");
        if (_predictAccount == null) throw new InvalidOperationException("Predict account is required");

        var kernelAbi = GetKernelAbi();
        var kernelContract = _web3.Eth.GetContract(kernelAbi, _predictAccount);
        var executeFunction = kernelContract.GetFunction("execute");
        var mode = new byte[32];
        var executionData = EncodeExecutionCalldata(target, BigInteger.Zero, callDataHex.HexToByteArray());

        var gas = new Nethereum.Hex.HexTypes.HexBigInteger(300000);
        var txHash = await executeFunction.SendTransactionAsync(
            _account.Address, gas, null, null, mode, executionData);
        var receipt = await WaitForTransactionReceiptAsync(txHash, ct);

        return new TransactionResult
        {
            Success = receipt.Status?.Value == 1,
            Receipt = txHash,
            Cause = receipt.Status?.Value == 1 ? null : $"Transaction failed: {txHash}"
        };
    }

    /// <summary>
    /// Encode Kernel execution calldata as target (20 bytes) + value (32 bytes) + calldata.
    /// </summary>
    private static byte[] EncodeExecutionCalldata(string target, BigInteger value, byte[] callData)
    {
        var targetBytes = target.HexToByteArray();
        var valueBytes = value.ToBigEndianByteArray().PadLeft(32);
        return targetBytes.Concat(valueBytes).Concat(callData).ToArray();
    }

    private static string GetKernelAbi() =>
        """[{"inputs":[{"name":"mode","type":"bytes32"},{"name":"executionData","type":"bytes"}],"name":"execute","outputs":[],"stateMutability":"payable","type":"function"}]""";

    #endregion

    #region Allowance Query

    public async Task<BigInteger> GetUsdtAllowanceAsync(string owner, string? spender = null, CancellationToken ct = default)
    {
        spender ??= Constants.Addresses.CtfExchange;
        var abi = GetErc20Abi();
        var contract = _web3.Eth.GetContract(abi, Constants.Addresses.Usdt);
        var allowance = contract.GetFunction("allowance");
        return await allowance.CallAsync<BigInteger>(owner, spender);
    }

    public async Task<bool> IsApprovedForAllAsync(
        string owner,
        string operatorAddress,
        bool isNegRisk = false,
        bool isYieldBearing = false,
        CancellationToken ct = default)
    {
        var ctfAddress = Constants.GetConditionalTokensAddress(isNegRisk, isYieldBearing);
        var abi = GetConditionalTokensAbi();
        var contract = _web3.Eth.GetContract(abi, ctfAddress);
        var isApprovedForAll = contract.GetFunction("isApprovedForAll");
        return await isApprovedForAll.CallAsync<bool>(owner, operatorAddress);
    }

    #endregion

    #region Balance

    public async Task<BigInteger> BalanceOfAsync(string token = "USDT", string? address = null, CancellationToken ct = default)
    {
        address ??= _makerAddress;

        if (token == "USDT")
        {
            var abi = GetErc20Abi();
            var contract = _web3.Eth.GetContract(abi, Constants.Addresses.Usdt);
            var balanceOf = contract.GetFunction("balanceOf");
            var result = await balanceOf.CallAsync<BigInteger>(address);
            return result;
        }

        throw new ArgumentException($"Unsupported token: {token}");
    }

    #endregion

    #region Redeem Positions

    public async Task<TransactionResult> RedeemPositionsAsync(RedeemPositionsInput input, CancellationToken ct = default)
    {
        if (_account == null) throw new InvalidOperationException("Signer is required");

        var ctfAddress = Constants.GetConditionalTokensAddress(input.IsYieldBearing);
        var abi = GetConditionalTokensAbi();
        var contract = _web3.Eth.GetContract(abi, ctfAddress);
        var redeemPositions = contract.GetFunction("redeemPositions");

        var conditionId = input.ConditionId;
        if (!conditionId.StartsWith("0x")) conditionId = "0x" + conditionId;

        var indexSet = new List<BigInteger> { new(input.IndexSet) };
        var amount = input.Amount != null ? BigInteger.Parse(input.Amount) : BigInteger.Zero;

        string txHash;

        if (input.IsNegRisk)
        {
            // For NegRisk markets, use the NegRiskAdapter
            var adapterAddress = Constants.GetNegRiskAdapterAddress(input.IsYieldBearing);
            var adapterAbi = GetNegRiskAdapterAbi();
            var adapterContract = _web3.Eth.GetContract(adapterAbi, adapterAddress);
            var redeemPositionsNegRisk = adapterContract.GetFunction("redeemPositions");

            if (amount > 0)
            {
                txHash = await redeemPositionsNegRisk.SendTransactionAsync(
                    _account.Address, conditionId, indexSet.First(), amount);
            }
            else
            {
                txHash = await redeemPositionsNegRisk.SendTransactionAsync(
                    _account.Address, conditionId, indexSet.First());
            }
        }
        else
        {
            txHash = await redeemPositions.SendTransactionAsync(
                _account.Address, conditionId, indexSet);
        }

        var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);

        return new TransactionResult
        {
            Success = receipt?.Status?.Value == 1,
            Receipt = txHash
        };
    }

    #endregion

    #region Merge Positions

    public async Task<TransactionResult> MergePositionsAsync(MergePositionsInput input, CancellationToken ct = default)
    {
        if (_account == null) throw new InvalidOperationException("Signer is required");

        var ctfAddress = Constants.GetConditionalTokensAddress(input.IsYieldBearing);
        var abi = GetConditionalTokensAbi();
        var contract = _web3.Eth.GetContract(abi, ctfAddress);
        var mergePositions = contract.GetFunction("mergePositions");

        var conditionId = input.ConditionId;
        if (!conditionId.StartsWith("0x")) conditionId = "0x" + conditionId;

        var amount = BigInteger.Parse(input.Amount);

        var txHash = await mergePositions.SendTransactionAsync(
            _account.Address, conditionId, amount);

        var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);

        return new TransactionResult
        {
            Success = receipt?.Status?.Value == 1,
            Receipt = txHash
        };
    }

    #endregion

    #region Cancel Orders

    public async Task<TransactionResult> CancelOrdersAsync(List<Models.Order> orders, CancelOrdersOptions options, CancellationToken ct = default)
    {
        if (_account == null) throw new InvalidOperationException("Signer is required");
        if (orders.Count == 0) return new TransactionResult { Success = true };

        var exchangeAddress = Constants.GetVerifyingContract(options.IsNegRisk, options.IsYieldBearing);
        var abi = GetCtfExchangeAbi();
        var contract = _web3.Eth.GetContract(abi, exchangeAddress);
        var cancel = contract.GetFunction("cancel");

        var orderStructs = orders.Select(o => new
        {
            Salt = BigInteger.Parse(o.Salt.StartsWith("0x") ? o.Salt[2..] : o.Salt, System.Globalization.NumberStyles.HexNumber),
            Maker = o.Maker,
            Signer = o.Signer,
            Taker = o.Taker,
            TokenId = BigInteger.Parse(o.TokenId.StartsWith("0x") ? o.TokenId[2..] : o.TokenId, System.Globalization.NumberStyles.HexNumber),
            MakerAmount = BigInteger.Parse(o.MakerAmount),
            TakerAmount = BigInteger.Parse(o.TakerAmount),
            Expiration = (BigInteger)o.Expiration,
            Nonce = BigInteger.Parse(o.Nonce),
            FeeRateBps = BigInteger.Parse(o.FeeRateBps),
            Side = (byte)o.Side,
            SignatureType = (byte)o.SignatureType,
            Signature = o.Signature
        }).ToList();

        var txHash = await cancel.SendTransactionAsync(_account.Address, orderStructs);
        var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);

        return new TransactionResult
        {
            Success = receipt?.Status?.Value == 1,
            Receipt = txHash
        };
    }

    #endregion

    #region Validate Token IDs

    public bool ValidateTokenIds(List<BigInteger> tokenIds, bool isNegRisk, bool isYieldBearing)
    {
        // Basic validation - token IDs should be valid ERC1155 token IDs
        // For NegRisk markets, token IDs follow a specific pattern
        return tokenIds.All(id => id > 0);
    }

    #endregion

    #region ABI Helpers

    private static string GetErc20Abi() =>
        """[{"inputs":[{"name":"owner","type":"address"}],"name":"balanceOf","outputs":[{"name":"","type":"uint256"}],"stateMutability":"view","type":"function"},{"inputs":[{"name":"spender","type":"address"},{"name":"amount","type":"uint256"}],"name":"approve","outputs":[{"name":"","type":"bool"}],"stateMutability":"nonpayable","type":"function"},{"inputs":[{"name":"owner","type":"address"},{"name":"spender","type":"address"}],"name":"allowance","outputs":[{"name":"","type":"uint256"}],"stateMutability":"view","type":"function"}]""";

    private static string GetConditionalTokensAbi() =>
        """[{"inputs":[{"name":"account","type":"address"},{"name":"operator","type":"address"}],"name":"isApprovedForAll","outputs":[{"name":"","type":"bool"}],"stateMutability":"view","type":"function"},{"inputs":[{"name":"operator","type":"address"},{"name":"approved","type":"bool"}],"name":"setApprovalForAll","outputs":[],"stateMutability":"nonpayable","type":"function"},{"inputs":[{"name":"conditionId","type":"bytes32"},{"name":"indexSets","type":"uint256[]"}],"name":"redeemPositions","outputs":[],"stateMutability":"nonpayable","type":"function"},{"inputs":[{"name":"conditionId","type":"bytes32"},{"name":"amount","type":"uint256"}],"name":"mergePositions","outputs":[],"stateMutability":"nonpayable","type":"function"}]""";

    private static string GetCtfExchangeAbi() =>
        """[{"inputs":[{"components":[{"name":"salt","type":"uint256"},{"name":"maker","type":"address"},{"name":"signer","type":"address"},{"name":"taker","type":"address"},{"name":"tokenId","type":"uint256"},{"name":"makerAmount","type":"uint256"},{"name":"takerAmount","type":"uint256"},{"name":"expiration","type":"uint256"},{"name":"nonce","type":"uint256"},{"name":"feeRateBps","type":"uint256"},{"name":"side","type":"uint8"},{"name":"signatureType","type":"uint8"},{"name":"signature","type":"bytes"}],"name":"orders","type":"tuple[]"}],"name":"cancel","outputs":[],"stateMutability":"nonpayable","type":"function"}]""";

    private static string GetNegRiskAdapterAbi() =>
        """[{"inputs":[{"name":"conditionId","type":"bytes32"},{"name":"indexSet","type":"uint256"},{"name":"amount","type":"uint256"}],"name":"redeemPositions","outputs":[],"stateMutability":"nonpayable","type":"function"},{"inputs":[{"name":"conditionId","type":"bytes32"},{"name":"indexSet","type":"uint256"}],"name":"redeemPositions","outputs":[],"stateMutability":"nonpayable","type":"function"}]""";

    #endregion
}

internal static class ByteArrayExtensions
{
    public static byte[] PadLeft(this byte[] bytes, int totalLength)
    {
        if (bytes.Length >= totalLength) return bytes;
        var result = new byte[totalLength];
        Array.Copy(bytes, 0, result, totalLength - bytes.Length, bytes.Length);
        return result;
    }

    public static byte[] ToBigEndianByteArray(this BigInteger value)
    {
        if (value.Sign < 0)
        {
            // Negative BigInteger cannot use isUnsigned=true.
            // For uint256 fields, negative values shouldn't occur — 
            // this typically happens when hex parsing treats high bit as sign.
            // Use absolute value and ensure proper encoding.
            var bytes = BigInteger.Abs(value).ToByteArray(isUnsigned: true, isBigEndian: true);
            return bytes;
        }
        var bytes2 = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        return bytes2;
    }
}
