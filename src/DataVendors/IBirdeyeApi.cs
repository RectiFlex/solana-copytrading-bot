using Refit;

namespace SolanaTradingBot.DataVendors;

public interface IBirdeyeApi
{
    [Get("/defi/token_overview")]
    Task<BirdeyeTokenResponse> GetTokenOverviewAsync([Query] string address);

    [Get("/defi/history_price")]
    Task<BirdeyeHistoryResponse> GetPriceHistoryAsync(
        [Query] string address,
        [Query] string address_type = "token",
        [Query] string type = "1H",
        [Query] long? time_from = null,
        [Query] long? time_to = null);

    [Get("/defi/ohlcv")]
    Task<BirdeyeOhlcvResponse> GetOhlcvAsync(
        [Query] string address,
        [Query] string address_type = "token", 
        [Query] string type = "1m",
        [Query] long? time_from = null,
        [Query] long? time_to = null);

    [Get("/defi/ohlcv/base_quote")]
    Task<BirdeyeOhlcvResponse> GetOhlcvBaseQuoteAsync(
        [Query] string base_address,
        [Query] string quote_address,
        [Query] string type = "1m",
        [Query] long? time_from = null,
        [Query] long? time_to = null);

    [Get("/defi/txs/token")]
    Task<BirdeyeTradesResponse> GetTokenTradesAsync(
        [Query] string address,
        [Query] string tx_type = "swap",
        [Query] int limit = 50,
        [Query] int offset = 0,
        [Query] long? before_time = null,
        [Query] long? after_time = null);

    [Get("/defi/token_security")]
    Task<BirdeyeTokenResponse> GetTokenSecurityAsync([Query] string address);

    [Get("/defi/token_creation_info")]
    Task<BirdeyeTokenResponse> GetTokenCreationInfoAsync([Query] string address);

    [Get("/token/holder")]
    Task<BirdeyeHoldersResponse> GetTokenHoldersAsync(
        [Query] string address,
        [Query] int limit = 100,
        [Query] int offset = 0);

    [Get("/defi/history_liquidity")]
    Task<BirdeyeHistoryResponse> GetLiquidityHistoryAsync(
        [Query] string address,
        [Query] string address_type = "token",
        [Query] string type = "1H",
        [Query] long? time_from = null,
        [Query] long? time_to = null);

    [Get("/defi/history_volume")]
    Task<BirdeyeHistoryResponse> GetVolumeHistoryAsync(
        [Query] string address,
        [Query] string address_type = "token",
        [Query] string type = "1H",
        [Query] long? time_from = null,
        [Query] long? time_to = null);

    [Get("/defi/trending_tokens")]
    Task<BirdeyeTrendingResponse> GetTrendingTokensAsync(
        [Query] string sort_by = "volume24hUSD",
        [Query] string sort_type = "desc",
        [Query] int offset = 0,
        [Query] int limit = 50);

    [Get("/defi/top_traders")]
    Task<BirdeyeTradesResponse> GetTopTradersAsync(
        [Query] string address,
        [Query] string type = "24h",
        [Query] int limit = 50,
        [Query] int offset = 0);
}