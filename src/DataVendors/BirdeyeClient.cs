using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SolanaTradingBot.Engine;

namespace SolanaTradingBot.DataVendors;

public interface IBirdeyeClient
{
    Task<TokenData?> GetTokenDataAsync(string tokenMint);
    Task<List<OhlcvItem>> GetCandlesAsync(string tokenMint, string timeframe, DateTime? from = null, DateTime? to = null);
    Task<List<TradeItem>> GetRecentTradesAsync(string tokenMint, int limit = 50, DateTime? since = null);
    Task<HoldersData?> GetHoldersDataAsync(string tokenMint);
    Task<List<HistoryItem>> GetLiquidityHistoryAsync(string tokenMint, string timeframe, DateTime? from = null, DateTime? to = null);
    Task<List<HistoryItem>> GetVolumeHistoryAsync(string tokenMint, string timeframe, DateTime? from = null, DateTime? to = null);
    Task<List<TrendingToken>> GetTrendingTokensAsync(int limit = 50);
    Task<decimal> CalculateBuyerSellerRatioAsync(string tokenMint, TimeSpan lookback);
    Task<decimal> CalculateNetInflowAsync(string tokenMint, TimeSpan lookback);
    Task<int> CalculateUniqueBuyersDeltaAsync(string tokenMint, TimeSpan lookback);
}

public class BirdeyeClient : IBirdeyeClient
{
    private readonly IBirdeyeApi _api;
    private readonly ILogger<BirdeyeClient> _logger;
    private readonly TradingConfig _config;

    public BirdeyeClient(IBirdeyeApi api, ILogger<BirdeyeClient> logger, IOptions<TradingConfig> config)
    {
        _api = api;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<TokenData?> GetTokenDataAsync(string tokenMint)
    {
        try
        {
            _logger.LogDebug("Getting token data for {TokenMint}", tokenMint);
            
            var response = await _api.GetTokenOverviewAsync(tokenMint);
            
            if (!response.Success || response.Data == null)
            {
                _logger.LogWarning("Failed to get token data for {TokenMint}: Success={Success}", 
                    tokenMint, response.Success);
                return null;
            }

            return response.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting token data for {TokenMint}", tokenMint);
            return null;
        }
    }

    public async Task<List<OhlcvItem>> GetCandlesAsync(string tokenMint, string timeframe, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            _logger.LogDebug("Getting candles for {TokenMint}, timeframe: {Timeframe}", tokenMint, timeframe);

            long? timeFrom = from?.ToUnixTimeSeconds();
            long? timeTo = to?.ToUnixTimeSeconds();

            var response = await _api.GetOhlcvAsync(tokenMint, "token", timeframe, timeFrom, timeTo);

            if (!response.Success || response.Data == null)
            {
                _logger.LogWarning("Failed to get candles for {TokenMint}", tokenMint);
                return new List<OhlcvItem>();
            }

            return response.Data.Items.OrderBy(x => x.UnixTime).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting candles for {TokenMint}", tokenMint);
            return new List<OhlcvItem>();
        }
    }

    public async Task<List<TradeItem>> GetRecentTradesAsync(string tokenMint, int limit = 50, DateTime? since = null)
    {
        try
        {
            _logger.LogDebug("Getting recent trades for {TokenMint}, limit: {Limit}", tokenMint, limit);

            long? afterTime = since?.ToUnixTimeSeconds();
            
            var response = await _api.GetTokenTradesAsync(tokenMint, "swap", limit, 0, null, afterTime);

            if (!response.Success || response.Data == null)
            {
                _logger.LogWarning("Failed to get trades for {TokenMint}", tokenMint);
                return new List<TradeItem>();
            }

            return response.Data.Items.OrderByDescending(x => x.BlockUnixTime).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trades for {TokenMint}", tokenMint);
            return new List<TradeItem>();
        }
    }

    public async Task<HoldersData?> GetHoldersDataAsync(string tokenMint)
    {
        try
        {
            _logger.LogDebug("Getting holders data for {TokenMint}", tokenMint);
            
            var response = await _api.GetTokenHoldersAsync(tokenMint, 100, 0);

            if (!response.Success || response.Data == null)
            {
                _logger.LogWarning("Failed to get holders data for {TokenMint}", tokenMint);
                return null;
            }

            return response.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting holders data for {TokenMint}", tokenMint);
            return null;
        }
    }

    public async Task<List<HistoryItem>> GetLiquidityHistoryAsync(string tokenMint, string timeframe, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            _logger.LogDebug("Getting liquidity history for {TokenMint}", tokenMint);

            long? timeFrom = from?.ToUnixTimeSeconds();
            long? timeTo = to?.ToUnixTimeSeconds();

            var response = await _api.GetLiquidityHistoryAsync(tokenMint, "token", timeframe, timeFrom, timeTo);

            if (!response.Success || response.Data == null)
            {
                _logger.LogWarning("Failed to get liquidity history for {TokenMint}", tokenMint);
                return new List<HistoryItem>();
            }

            return response.Data.Items.OrderBy(x => x.UnixTime).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting liquidity history for {TokenMint}", tokenMint);
            return new List<HistoryItem>();
        }
    }

    public async Task<List<HistoryItem>> GetVolumeHistoryAsync(string tokenMint, string timeframe, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            _logger.LogDebug("Getting volume history for {TokenMint}", tokenMint);

            long? timeFrom = from?.ToUnixTimeSeconds();
            long? timeTo = to?.ToUnixTimeSeconds();

            var response = await _api.GetVolumeHistoryAsync(tokenMint, "token", timeframe, timeFrom, timeTo);

            if (!response.Success || response.Data == null)
            {
                _logger.LogWarning("Failed to get volume history for {TokenMint}", tokenMint);
                return new List<HistoryItem>();
            }

            return response.Data.Items.OrderBy(x => x.UnixTime).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting volume history for {TokenMint}", tokenMint);
            return new List<HistoryItem>();
        }
    }

    public async Task<List<TrendingToken>> GetTrendingTokensAsync(int limit = 50)
    {
        try
        {
            _logger.LogDebug("Getting trending tokens, limit: {Limit}", limit);
            
            var response = await _api.GetTrendingTokensAsync("volume24hUSD", "desc", 0, limit);

            if (!response.Success || response.Data == null)
            {
                _logger.LogWarning("Failed to get trending tokens");
                return new List<TrendingToken>();
            }

            return response.Data.Tokens;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trending tokens");
            return new List<TrendingToken>();
        }
    }

    public async Task<decimal> CalculateBuyerSellerRatioAsync(string tokenMint, TimeSpan lookback)
    {
        try
        {
            var since = DateTime.UtcNow - lookback;
            var trades = await GetRecentTradesAsync(tokenMint, 500, since);

            if (!trades.Any())
                return 0m;

            var buyVolume = trades.Where(t => t.IsBuy).Sum(t => t.VolumeInUsd);
            var sellVolume = trades.Where(t => !t.IsBuy).Sum(t => t.VolumeInUsd);

            return sellVolume > 0 ? buyVolume / sellVolume : 0m;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating buyer/seller ratio for {TokenMint}", tokenMint);
            return 0m;
        }
    }

    public async Task<decimal> CalculateNetInflowAsync(string tokenMint, TimeSpan lookback)
    {
        try
        {
            var since = DateTime.UtcNow - lookback;
            var trades = await GetRecentTradesAsync(tokenMint, 500, since);

            if (!trades.Any())
                return 0m;

            var buyVolume = trades.Where(t => t.IsBuy).Sum(t => t.VolumeInUsd);
            var sellVolume = trades.Where(t => !t.IsBuy).Sum(t => t.VolumeInUsd);

            return buyVolume - sellVolume;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating net inflow for {TokenMint}", tokenMint);
            return 0m;
        }
    }

    public async Task<int> CalculateUniqueBuyersDeltaAsync(string tokenMint, TimeSpan lookback)
    {
        try
        {
            var since = DateTime.UtcNow - lookback;
            var trades = await GetRecentTradesAsync(tokenMint, 500, since);

            if (!trades.Any())
                return 0;

            var uniqueBuyers = trades.Where(t => t.IsBuy).Select(t => t.Wallet).Distinct().Count();
            var uniqueSellers = trades.Where(t => !t.IsBuy).Select(t => t.Wallet).Distinct().Count();

            return uniqueBuyers - uniqueSellers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating unique buyers delta for {TokenMint}", tokenMint);
            return 0;
        }
    }
}

// Extension methods for DateTime conversions
public static class DateTimeExtensions
{
    public static long ToUnixTimeSeconds(this DateTime dateTime)
    {
        return ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
    }
}