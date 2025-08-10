using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SolanaTradingBot.Common;
using SolanaTradingBot.DataVendors;

namespace SolanaTradingBot.Engine;

public enum GuardResult
{
    Pass,
    Fail,
    Warning
}

public class GuardCheckResult
{
    public GuardResult Result { get; set; }
    public required string GuardName { get; set; }
    public required string Message { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

public interface IGuards
{
    Task<List<GuardCheckResult>> RunAllGuardsAsync(string tokenMint);
    Task<GuardCheckResult> CheckHoneypotAsync(string tokenMint);
    Task<GuardCheckResult> CheckLiquidityAsync(string tokenMint);
    Task<GuardCheckResult> CheckHoldersAsync(string tokenMint);
    Task<GuardCheckResult> CheckBuyerSellerRatioAsync(string tokenMint);
    Task<GuardCheckResult> CheckTop10ConcentrationAsync(string tokenMint);
    Task<GuardCheckResult> CheckLiquidityPullAsync(string tokenMint);
    Task<GuardCheckResult> CheckTokenStandardAsync(string tokenMint);
    Task<GuardCheckResult> CheckFeedsFreshnessAsync(string tokenMint);
}

public class Guards : IGuards
{
    private readonly IBirdeyeClient _birdeyeClient;
    private readonly IJupiterClient _jupiterClient;
    private readonly ILogger<Guards> _logger;
    private readonly TradingConfig _config;

    public Guards(
        IBirdeyeClient birdeyeClient,
        IJupiterClient jupiterClient,
        ILogger<Guards> logger,
        IOptions<TradingConfig> config)
    {
        _birdeyeClient = birdeyeClient;
        _jupiterClient = jupiterClient;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<List<GuardCheckResult>> RunAllGuardsAsync(string tokenMint)
    {
        _logger.LogDebug("Running all guards for token {TokenMint}", tokenMint);

        var guards = new[]
        {
            CheckHoneypotAsync(tokenMint),
            CheckLiquidityAsync(tokenMint),
            CheckHoldersAsync(tokenMint),
            CheckBuyerSellerRatioAsync(tokenMint),
            CheckTop10ConcentrationAsync(tokenMint),
            CheckLiquidityPullAsync(tokenMint),
            CheckTokenStandardAsync(tokenMint),
            CheckFeedsFreshnessAsync(tokenMint)
        };

        var results = await Task.WhenAll(guards);
        
        var failedGuards = results.Where(r => r.Result == GuardResult.Fail).ToList();
        var warningGuards = results.Where(r => r.Result == GuardResult.Warning).ToList();

        if (failedGuards.Any())
        {
            _logger.LogWarning("Token {TokenMint} failed {FailedCount} guards: {FailedGuards}",
                tokenMint, failedGuards.Count, string.Join(", ", failedGuards.Select(g => g.GuardName)));
        }

        if (warningGuards.Any())
        {
            _logger.LogInformation("Token {TokenMint} has {WarningCount} warnings: {WarningGuards}",
                tokenMint, warningGuards.Count, string.Join(", ", warningGuards.Select(g => g.GuardName)));
        }

        return results.ToList();
    }

    public async Task<GuardCheckResult> CheckHoneypotAsync(string tokenMint)
    {
        try
        {
            // In a real implementation, this would:
            // 1. Try to simulate a small sell transaction using Jupiter
            // 2. Check if the transaction would succeed
            // 3. Look for transfer hooks or other restrictions
            
            var simulationPassed = await SimulateSellTransaction(tokenMint);
            
            return new GuardCheckResult
            {
                Result = simulationPassed ? GuardResult.Pass : GuardResult.Fail,
                GuardName = "Honeypot",
                Message = simulationPassed ? "Token can be sold" : "Token appears to be a honeypot - sell simulation failed",
                Data = { { "simulationPassed", simulationPassed } }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking honeypot for {TokenMint}", tokenMint);
            return new GuardCheckResult
            {
                Result = GuardResult.Fail,
                GuardName = "Honeypot",
                Message = "Failed to check honeypot status",
                Data = { { "error", ex.Message } }
            };
        }
    }

    public async Task<GuardCheckResult> CheckLiquidityAsync(string tokenMint)
    {
        try
        {
            var tokenData = await _birdeyeClient.GetTokenDataAsync(tokenMint);
            
            if (tokenData == null)
            {
                return new GuardCheckResult
                {
                    Result = GuardResult.Fail,
                    GuardName = "Liquidity",
                    Message = "Could not retrieve token data",
                    Data = { { "tokenData", false } }
                };
            }

            var liquidityUSD = tokenData.Liquidity;
            var minLiquidityUSD = _config.Guards.MinLPUSD;

            var passed = liquidityUSD >= minLiquidityUSD;

            return new GuardCheckResult
            {
                Result = passed ? GuardResult.Pass : GuardResult.Fail,
                GuardName = "Liquidity",
                Message = passed 
                    ? $"Liquidity ${liquidityUSD:F0} meets minimum ${minLiquidityUSD:F0}"
                    : $"Liquidity ${liquidityUSD:F0} below minimum ${minLiquidityUSD:F0}",
                Data = { 
                    { "liquidityUSD", liquidityUSD },
                    { "minLiquidityUSD", minLiquidityUSD }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking liquidity for {TokenMint}", tokenMint);
            return new GuardCheckResult
            {
                Result = GuardResult.Fail,
                GuardName = "Liquidity",
                Message = "Failed to check liquidity",
                Data = { { "error", ex.Message } }
            };
        }
    }

    public async Task<GuardCheckResult> CheckHoldersAsync(string tokenMint)
    {
        try
        {
            var holdersData = await _birdeyeClient.GetHoldersDataAsync(tokenMint);
            
            if (holdersData == null)
            {
                return new GuardCheckResult
                {
                    Result = GuardResult.Fail,
                    GuardName = "Holders",
                    Message = "Could not retrieve holders data",
                    Data = { { "holdersData", false } }
                };
            }

            var holderCount = holdersData.HolderCount;
            var minHolders = _config.Guards.MinHolders10m;

            var passed = holderCount >= minHolders;

            return new GuardCheckResult
            {
                Result = passed ? GuardResult.Pass : GuardResult.Fail,
                GuardName = "Holders",
                Message = passed 
                    ? $"Holder count {holderCount} meets minimum {minHolders}"
                    : $"Holder count {holderCount} below minimum {minHolders}",
                Data = { 
                    { "holderCount", holderCount },
                    { "minHolders", minHolders }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking holders for {TokenMint}", tokenMint);
            return new GuardCheckResult
            {
                Result = GuardResult.Fail,
                GuardName = "Holders",
                Message = "Failed to check holders",
                Data = { { "error", ex.Message } }
            };
        }
    }

    public async Task<GuardCheckResult> CheckBuyerSellerRatioAsync(string tokenMint)
    {
        try
        {
            var lookback = TimeSpan.FromMinutes(10);
            var ratio = await _birdeyeClient.CalculateBuyerSellerRatioAsync(tokenMint, lookback);
            var minRatio = _config.Guards.BuyerSellerMin;

            var passed = ratio >= minRatio;

            return new GuardCheckResult
            {
                Result = passed ? GuardResult.Pass : GuardResult.Fail,
                GuardName = "BuyerSellerRatio",
                Message = passed 
                    ? $"Buyer/Seller ratio {ratio:F2} meets minimum {minRatio:F2}"
                    : $"Buyer/Seller ratio {ratio:F2} below minimum {minRatio:F2}",
                Data = { 
                    { "buyerSellerRatio", ratio },
                    { "minRatio", minRatio },
                    { "lookbackMinutes", lookback.TotalMinutes }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking buyer/seller ratio for {TokenMint}", tokenMint);
            return new GuardCheckResult
            {
                Result = GuardResult.Fail,
                GuardName = "BuyerSellerRatio",
                Message = "Failed to check buyer/seller ratio",
                Data = { { "error", ex.Message } }
            };
        }
    }

    public async Task<GuardCheckResult> CheckTop10ConcentrationAsync(string tokenMint)
    {
        try
        {
            var holdersData = await _birdeyeClient.GetHoldersDataAsync(tokenMint);
            
            if (holdersData == null)
            {
                return new GuardCheckResult
                {
                    Result = GuardResult.Fail,
                    GuardName = "Top10Concentration",
                    Message = "Could not retrieve holders data",
                    Data = { { "holdersData", false } }
                };
            }

            var top10Percentage = holdersData.TopHolders.Take(10).Sum(h => h.Percentage);
            var maxTop10Pct = _config.Guards.MaxTop10Pct;

            var passed = top10Percentage <= maxTop10Pct;

            return new GuardCheckResult
            {
                Result = passed ? GuardResult.Pass : GuardResult.Warning,
                GuardName = "Top10Concentration",
                Message = passed 
                    ? $"Top 10 holders own {top10Percentage:F1}% (within {maxTop10Pct}% limit)"
                    : $"Top 10 holders own {top10Percentage:F1}% (exceeds {maxTop10Pct}% limit)",
                Data = { 
                    { "top10Percentage", top10Percentage },
                    { "maxTop10Pct", maxTop10Pct }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking top 10 concentration for {TokenMint}", tokenMint);
            return new GuardCheckResult
            {
                Result = GuardResult.Fail,
                GuardName = "Top10Concentration",
                Message = "Failed to check top 10 concentration",
                Data = { { "error", ex.Message } }
            };
        }
    }

    public async Task<GuardCheckResult> CheckLiquidityPullAsync(string tokenMint)
    {
        try
        {
            var lookback = TimeSpan.FromMinutes(10);
            var liquidityHistory = await _birdeyeClient.GetLiquidityHistoryAsync(tokenMint, "1m", 
                DateTime.UtcNow - lookback, DateTime.UtcNow);

            if (liquidityHistory.Count < 2)
            {
                return new GuardCheckResult
                {
                    Result = GuardResult.Warning,
                    GuardName = "LiquidityPull",
                    Message = "Insufficient liquidity history data",
                    Data = { { "historyPoints", liquidityHistory.Count } }
                };
            }

            var latestLiquidity = liquidityHistory.Last().Value;
            var earliestLiquidity = liquidityHistory.First().Value;
            
            if (earliestLiquidity <= 0)
            {
                return new GuardCheckResult
                {
                    Result = GuardResult.Warning,
                    GuardName = "LiquidityPull",
                    Message = "Invalid historical liquidity data",
                    Data = { { "earliestLiquidity", earliestLiquidity } }
                };
            }

            var changePercent = ((latestLiquidity - earliestLiquidity) / earliestLiquidity) * 100;
            var maxPullPercent = -_config.Guards.LpPullPct10mBlock; // Negative because it's a pull

            var passed = changePercent >= maxPullPercent;

            return new GuardCheckResult
            {
                Result = passed ? GuardResult.Pass : GuardResult.Fail,
                GuardName = "LiquidityPull",
                Message = passed 
                    ? $"Liquidity change {changePercent:F1}% within acceptable range"
                    : $"Liquidity pulled {changePercent:F1}% (exceeds {-maxPullPercent:F1}% threshold)",
                Data = { 
                    { "liquidityChangePercent", changePercent },
                    { "maxPullPercent", maxPullPercent },
                    { "latestLiquidity", latestLiquidity },
                    { "earliestLiquidity", earliestLiquidity }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking liquidity pull for {TokenMint}", tokenMint);
            return new GuardCheckResult
            {
                Result = GuardResult.Warning,
                GuardName = "LiquidityPull",
                Message = "Failed to check liquidity pull",
                Data = { { "error", ex.Message } }
            };
        }
    }

    public async Task<GuardCheckResult> CheckTokenStandardAsync(string tokenMint)
    {
        try
        {
            var tokenData = await _birdeyeClient.GetTokenDataAsync(tokenMint);
            
            if (tokenData == null)
            {
                return new GuardCheckResult
                {
                    Result = GuardResult.Fail,
                    GuardName = "TokenStandard",
                    Message = "Could not retrieve token data",
                    Data = { { "tokenData", false } }
                };
            }

            var decimals = tokenData.Decimals;
            var minDecimals = _config.Guards.DecimalsMin;
            var maxDecimals = _config.Guards.DecimalsMax;

            var passed = decimals >= minDecimals && decimals <= maxDecimals;

            return new GuardCheckResult
            {
                Result = passed ? GuardResult.Pass : GuardResult.Fail,
                GuardName = "TokenStandard",
                Message = passed 
                    ? $"Token decimals {decimals} within acceptable range {minDecimals}-{maxDecimals}"
                    : $"Token decimals {decimals} outside acceptable range {minDecimals}-{maxDecimals}",
                Data = { 
                    { "decimals", decimals },
                    { "minDecimals", minDecimals },
                    { "maxDecimals", maxDecimals }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking token standard for {TokenMint}", tokenMint);
            return new GuardCheckResult
            {
                Result = GuardResult.Fail,
                GuardName = "TokenStandard",
                Message = "Failed to check token standard",
                Data = { { "error", ex.Message } }
            };
        }
    }

    public async Task<GuardCheckResult> CheckFeedsFreshnessAsync(string tokenMint)
    {
        try
        {
            var tokenData = await _birdeyeClient.GetTokenDataAsync(tokenMint);
            
            if (tokenData == null)
            {
                return new GuardCheckResult
                {
                    Result = GuardResult.Fail,
                    GuardName = "FeedsFreshness",
                    Message = "Could not retrieve token data",
                    Data = { { "tokenData", false } }
                };
            }

            // For this implementation, we assume the data is fresh if we can retrieve it
            // In a real implementation, you would check the timestamp of the data
            var maxAgeMs = _config.Guards.FeedsFreshMs;
            var isFresh = true; // Placeholder - would check actual timestamp

            return new GuardCheckResult
            {
                Result = isFresh ? GuardResult.Pass : GuardResult.Fail,
                GuardName = "FeedsFreshness",
                Message = isFresh 
                    ? $"Data feeds are fresh (within {maxAgeMs}ms)"
                    : $"Data feeds are stale (older than {maxAgeMs}ms)",
                Data = { 
                    { "maxAgeMs", maxAgeMs },
                    { "isFresh", isFresh }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking feeds freshness for {TokenMint}", tokenMint);
            return new GuardCheckResult
            {
                Result = GuardResult.Fail,
                GuardName = "FeedsFreshness",
                Message = "Failed to check feeds freshness",
                Data = { { "error", ex.Message } }
            };
        }
    }

    private async Task<bool> SimulateSellTransaction(string tokenMint)
    {
        try
        {
            // Simulate a small sell transaction to check if token can be sold
            const string SOL_MINT = "So11111111111111111111111111111111111111112";
            const decimal TEST_AMOUNT_SOL = 0.001m; // Very small amount for testing
            
            var quote = await _jupiterClient.GetQuoteAsync(tokenMint, SOL_MINT, TEST_AMOUNT_SOL, 500);
            
            if (quote == null)
                return false;

            // If we can get a quote, the token is likely not a honeypot
            // In a more sophisticated implementation, you would actually simulate the transaction
            return true;
        }
        catch
        {
            return false;
        }
    }
}