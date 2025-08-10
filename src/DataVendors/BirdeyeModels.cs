using System.Text.Json.Serialization;

namespace SolanaTradingBot.DataVendors;

// Birdeye API models
public class BirdeyeTokenResponse
{
    [JsonPropertyName("data")]
    public TokenData? Data { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

public class TokenData
{
    [JsonPropertyName("address")]
    public required string Address { get; set; }

    [JsonPropertyName("symbol")]
    public required string Symbol { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("decimals")]
    public int Decimals { get; set; }

    [JsonPropertyName("supply")]
    public string? Supply { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("priceChange24h")]
    public decimal PriceChange24h { get; set; }

    [JsonPropertyName("volume24h")]
    public decimal Volume24h { get; set; }

    [JsonPropertyName("volume24hChangePercent")]
    public decimal Volume24hChangePercent { get; set; }

    [JsonPropertyName("liquidity")]
    public decimal Liquidity { get; set; }

    [JsonPropertyName("liquidityChangePercent24h")]
    public decimal LiquidityChangePercent24h { get; set; }

    [JsonPropertyName("mc")]
    public decimal MarketCap { get; set; }

    [JsonPropertyName("mcChangePercent24h")]
    public decimal MarketCapChangePercent24h { get; set; }

    [JsonPropertyName("v24hUSD")]
    public decimal Volume24hUSD { get; set; }

    [JsonPropertyName("trade24h")]
    public int Trade24h { get; set; }

    [JsonPropertyName("trade24hChangePercent")]
    public decimal Trade24hChangePercent { get; set; }

    [JsonPropertyName("buy24h")]
    public int Buy24h { get; set; }

    [JsonPropertyName("sell24h")]
    public int Sell24h { get; set; }

    [JsonPropertyName("uniqueWallet24h")]
    public int UniqueWallet24h { get; set; }

    [JsonPropertyName("uniqueWallet24hChangePercent")]
    public decimal UniqueWallet24hChangePercent { get; set; }
}

public class BirdeyeHistoryResponse
{
    [JsonPropertyName("data")]
    public HistoryData? Data { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

public class HistoryData
{
    [JsonPropertyName("items")]
    public List<HistoryItem> Items { get; set; } = new();
}

public class HistoryItem
{
    [JsonPropertyName("unixTime")]
    public long UnixTime { get; set; }

    [JsonPropertyName("value")]
    public decimal Value { get; set; }

    public DateTime Timestamp => DateTimeOffset.FromUnixTimeSeconds(UnixTime).DateTime;
}

public class BirdeyeOhlcvResponse
{
    [JsonPropertyName("data")]
    public OhlcvData? Data { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

public class OhlcvData
{
    [JsonPropertyName("items")]
    public List<OhlcvItem> Items { get; set; } = new();
}

public class OhlcvItem
{
    [JsonPropertyName("unixTime")]
    public long UnixTime { get; set; }

    [JsonPropertyName("o")]
    public decimal Open { get; set; }

    [JsonPropertyName("h")]
    public decimal High { get; set; }

    [JsonPropertyName("l")]
    public decimal Low { get; set; }

    [JsonPropertyName("c")]
    public decimal Close { get; set; }

    [JsonPropertyName("v")]
    public decimal Volume { get; set; }

    [JsonPropertyName("vBuy")]
    public decimal VolumeBuy { get; set; }

    [JsonPropertyName("vSell")]
    public decimal VolumeSell { get; set; }

    public DateTime Timestamp => DateTimeOffset.FromUnixTimeSeconds(UnixTime).DateTime;
    public decimal BuyerSellerRatio => VolumeSell > 0 ? VolumeBuy / VolumeSell : 0;
}

public class BirdeyeTradesResponse
{
    [JsonPropertyName("data")]
    public TradesData? Data { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

public class TradesData
{
    [JsonPropertyName("items")]
    public List<TradeItem> Items { get; set; } = new();
}

public class TradeItem
{
    [JsonPropertyName("txHash")]
    public required string TxHash { get; set; }

    [JsonPropertyName("blockUnixTime")]
    public long BlockUnixTime { get; set; }

    [JsonPropertyName("side")]
    public required string Side { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("volumeInUsd")]
    public decimal VolumeInUsd { get; set; }

    [JsonPropertyName("source")]
    public required string Source { get; set; }

    [JsonPropertyName("wallet")]
    public required string Wallet { get; set; }

    public DateTime Timestamp => DateTimeOffset.FromUnixTimeSeconds(BlockUnixTime).DateTime;
    public bool IsBuy => Side.Equals("buy", StringComparison.OrdinalIgnoreCase);
}

public class BirdeyeHoldersResponse
{
    [JsonPropertyName("data")]
    public HoldersData? Data { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

public class HoldersData
{
    [JsonPropertyName("holderCount")]
    public int HolderCount { get; set; }

    [JsonPropertyName("topHolders")]
    public List<HolderItem> TopHolders { get; set; } = new();
}

public class HolderItem
{
    [JsonPropertyName("address")]
    public required string Address { get; set; }

    [JsonPropertyName("percentage")]
    public decimal Percentage { get; set; }

    [JsonPropertyName("value")]
    public decimal Value { get; set; }

    [JsonPropertyName("uiAmount")]
    public decimal UiAmount { get; set; }
}

public class BirdeyeTrendingResponse
{
    [JsonPropertyName("data")]
    public TrendingData? Data { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

public class TrendingData
{
    [JsonPropertyName("tokens")]
    public List<TrendingToken> Tokens { get; set; } = new();
}

public class TrendingToken
{
    [JsonPropertyName("address")]
    public required string Address { get; set; }

    [JsonPropertyName("symbol")]
    public required string Symbol { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("decimals")]
    public int Decimals { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("priceChange24h")]
    public decimal PriceChange24h { get; set; }

    [JsonPropertyName("volume24hUSD")]
    public decimal Volume24hUSD { get; set; }

    [JsonPropertyName("volume24hChangePercent")]
    public decimal Volume24hChangePercent { get; set; }

    [JsonPropertyName("uniqueWallet24h")]
    public int UniqueWallet24h { get; set; }

    [JsonPropertyName("uniqueWallet24hChangePercent")]
    public decimal UniqueWallet24hChangePercent { get; set; }

    [JsonPropertyName("trade24h")]
    public int Trade24h { get; set; }

    [JsonPropertyName("liquidity")]
    public decimal Liquidity { get; set; }

    [JsonPropertyName("mc")]
    public decimal MarketCap { get; set; }
}