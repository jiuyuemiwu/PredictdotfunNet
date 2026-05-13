namespace PredictdotfunNet.Models;

public enum ChainId
{
    BnbMainnet = 56,
    BnbTestnet = 97
}

public enum Side
{
    BUY = 0,
    SELL = 1
}

public enum SignatureType
{
    EOA = 0
}

public enum OrderStrategy
{
    LIMIT,
    MARKET
}

public enum OrderStatus
{
    OPEN,
    FILLED,
    CANCELLED,
    EXPIRED
}

public enum ReservedBalancePolicy
{
    REJECT_MARKET_ORDER,
    SKIP_RESERVED_BALANCE_CHECKS
}

public enum SelfTradePrevention
{
    CANCEL_MAKER
}

public enum ReferralStatus
{
    LOCKED
}
