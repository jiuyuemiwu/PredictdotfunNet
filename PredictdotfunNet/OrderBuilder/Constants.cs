using Nethereum.ABI.EIP712;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using PredictdotfunNet.Models;

namespace PredictdotfunNet.OrderBuilder;

public static class Constants
{
    public static class Addresses
    {
        public const string CtfExchange = "0x8BC070BEdAB741406F4B1Eb65A72bee27894B689";
        public const string NegRiskCtfExchange = "0x365fb81bd4A24D6303cd2F19c349dE6894D8d58A";
        public const string ConditionalTokens = "0x22DA1810B194ca018378464a58f6Ac2B10C9d244";
        public const string NegRiskConditionalTokens = "0x22DA1810B194ca018378464a58f6Ac2B10C9d244";
        public const string NegRiskAdapter = "0xc3Cf7c252f65E0d8D88537dF96569AE94a7F1A6E";
        public const string Usdt = "0x55d398326f99059fF775485246999027B3197955";
        public const string Kernel = "0xBAC849bB641841b44E965fB01A4Bf5F074f84b4D";
        public const string EcdsaValidator = "0x845ADb2C711129d4f3966735eD98a9F09fC4cE57";
        public const string YieldBearingCtfExchange = "0x6bEb5a40C032AFc305961162d8204CDA16DECFa5";
        public const string YieldBearingConditionalTokens = "0x9400F8Ad57e9e0F352345935d6D3175975eb1d9F";
        public const string YieldBearingNegRiskConditionalTokens = "0xF64b0b318AAf83BD9071110af24D24445719A07F";
        public const string YieldBearingNegRiskCtfExchange = "0x8A289d458f5a134bA40015085A8F50Ffb681B41d";
        public const string YieldBearingNegRiskAdapter = "0x41dCe1A4B8FB5e6327701750aF6231B7CD0B2A40";
    }

    public static class ChainId
    {
        public const int BnbMainnet = 56;
        public const int BnbTestnet = 97;
    }

    public const string BnbMainnetRpcUrl = "https://bsc-dataseed.bnbchain.org/";
    public const string BnbTestnetRpcUrl = "https://bsc-testnet-dataseed.bnbchain.org/";

    public static readonly Dictionary<int, string> ChainRpcUrls = new()
    {
        [ChainId.BnbMainnet] = BnbMainnetRpcUrl,
        [ChainId.BnbTestnet] = BnbTestnetRpcUrl
    };

    public const string Eip712DomainName = "predict.fun CTF Exchange";
    public const string Eip712DomainVersion = "1";
    public const string KernelDomainName = "Kernel";
    public const string KernelDomainVersion = "0.3.1";

    public static string GetVerifyingContract(bool isNegRisk, bool isYieldBearing) => (isNegRisk, isYieldBearing) switch
    {
        (false, false) => Addresses.CtfExchange,
        (true, false) => Addresses.NegRiskCtfExchange,
        (false, true) => Addresses.YieldBearingCtfExchange,
        (true, true) => Addresses.YieldBearingNegRiskCtfExchange,
    };

    public static string GetExchangeAddress(bool isNegRisk, bool isYieldBearing) => (isNegRisk, isYieldBearing) switch
    {
        (false, false) => Addresses.CtfExchange,
        (true, false) => Addresses.NegRiskCtfExchange,
        (false, true) => Addresses.YieldBearingCtfExchange,
        (true, true) => Addresses.YieldBearingNegRiskCtfExchange,
    };

    public static string GetConditionalTokensAddress(bool isNegRisk, bool isYieldBearing) => (isNegRisk, isYieldBearing) switch
    {
        (false, false) => Addresses.ConditionalTokens,
        (true, false) => Addresses.NegRiskConditionalTokens,
        (false, true) => Addresses.YieldBearingConditionalTokens,
        (true, true) => Addresses.YieldBearingNegRiskConditionalTokens,
    };

    public static string GetConditionalTokensAddress(bool isYieldBearing) =>
        GetConditionalTokensAddress(false, isYieldBearing);

    public static string GetNegRiskAdapterAddress(bool isYieldBearing) => isYieldBearing
        ? Addresses.YieldBearingNegRiskAdapter
        : Addresses.NegRiskAdapter;
}
