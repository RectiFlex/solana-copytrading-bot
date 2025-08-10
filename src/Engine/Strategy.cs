using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SolanaTradingBot.Common;
using SolanaTradingBot.DataVendors;
using SolanaTradingBot.Discovery;
using SolanaTradingBot.State;

namespace SolanaTradingBot.Engine;

public enum StrategySignal
{
    None,
    Buy,
    Sell,
    Exit
}

public class StrategyResult
{
    public StrategySignal Signal { get; set; }
    public TradingSleeve Sleeve { get; set; }
    public decimal Confidence { get; set; }
    public decimal SuggestedSizeSOL { get; set; }
    public required string Reason { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public interface IStrategy
{
    Task<StrategyResult> EvaluateEntryAsync(PoolCandidate candidate);
    Task<StrategyResult> EvaluateExitAsync(Position position);
    Task<List<StrategyResult>> EvaluateAllExitsAsync(List<Position> openPositions);
}

public class Strategy : IStrategy
{
    private readonly IBirdeyeClient _birdeyeClient;
    private readonly IJupiterClient _jupiterClient;
    private readonly IGuards _guards;
    private readonly ILogger<Strategy> _logger;
    private readonly TradingConfig _config;

    public Strategy(
        IBirdeyeClient birdeyeClient,
        IJupiterClient jupiterClient,
        IGuards guards,
        ILogger<Strategy> logger,
        IOptions<TradingConfig> config)
    {
        _birdeyeClient = birdeyeClient;
        _jupiterClient = jupiterClient;
        _guards = guards;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<StrategyResult> EvaluateEntryAsync(PoolCandidate candidate)
    {
        try
        {
            _logger.LogDebug("Evaluating entry for candidate {TokenMint} from {Source}", 
                candidate.TokenMint, candidate.Source);

            // First, run all guards
            var guardResults = await _guards.RunAllGuardsAsync(candidate.TokenMint);
            var failedGuards = guardResults.Where(g => g.Result == GuardResult.Fail).ToList();
            
            if (failedGuards.Any())
            {
                return new StrategyResult
                {
                    Signal = StrategySignal.None,
                    Sleeve = TradingSleeve.Scalps,
                    Confidence = 0m,
                    SuggestedSizeSOL = 0m,
                    Reason = $"Failed guards: {string.Join(", ", failedGuards.Select(g => g.GuardName))}",
                    Metadata = { { "failedGuards", failedGuards.Select(g => g.GuardName).ToList() } }
                };
            }

            // Get token data and market cap
            var tokenData = await _birdeyeClient.GetTokenDataAsync(candidate.TokenMint);
            if (tokenData == null)
            {
                return new StrategyResult
                {
                    Signal = StrategySignal.None,
                    Sleeve = TradingSleeve.Scalps,
                    Confidence = 0m,
                    SuggestedSizeSOL = 0m,
                    Reason = "Could not retrieve token data",
                    Metadata = { { "tokenData", false } }
                };
            }

            var marketCapUSD = tokenData.MarketCap;

            // Determine which sleeve to use based on market cap and candidate source
            var sleeve = DetermineSleeve(candidate, marketCapUSD);
            var sleeveConfig = GetSleeveConfig(sleeve);

            // Check if market cap is within sleeve range
            if (!IsMarketCapInRange(marketCapUSD, sleeve))
            {
                return new StrategyResult
                {
                    Signal = StrategySignal.None,
                    Sleeve = sleeve,
                    Confidence = 0m,
                    SuggestedSizeSOL = 0m,
                    Reason = $"Market cap ${marketCapUSD:F0} outside {sleeve} range",
                    Metadata = { { "marketCapUSD", marketCapUSD }, { "sleeve", sleeve.ToString() } }
                };
            }

            // Evaluate specific sleeve strategy
            var strategyResult = sleeve switch
            {
                TradingSleeve.Scalps => await EvaluateScalpsEntryAsync(candidate, tokenData, sleeveConfig),
                TradingSleeve.Momentum => await EvaluateMomentumEntryAsync(candidate, tokenData, sleeveConfig),
                TradingSleeve.Swing => await EvaluateSwingEntryAsync(candidate, tokenData, sleeveConfig),
                _ => new StrategyResult 
                { 
                    Signal = StrategySignal.None, 
                    Sleeve = sleeve, 
                    Confidence = 0m, 
                    SuggestedSizeSOL = 0m, 
                    Reason = "Unknown sleeve" 
                }
            };

            _logger.LogInformation("Entry evaluation for {TokenMint}: {Signal} ({Sleeve}, Confidence: {Confidence:F1}%, Size: {Size:F3} SOL) - {Reason}",
                candidate.TokenMint, strategyResult.Signal, strategyResult.Sleeve, strategyResult.Confidence, 
                strategyResult.SuggestedSizeSOL, strategyResult.Reason);

            return strategyResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating entry for candidate {TokenMint}", candidate.TokenMint);
            return new StrategyResult
            {
                Signal = StrategySignal.None,
                Sleeve = TradingSleeve.Scalps,
                Confidence = 0m,
                SuggestedSizeSOL = 0m,
                Reason = $"Evaluation error: {ex.Message}",
                Metadata = { { "error", ex.Message } }
            };
        }
    }

    public async Task<StrategyResult> EvaluateExitAsync(Position position)
    {
        try
        {
            _logger.LogDebug("Evaluating exit for position {PositionId} ({TokenMint})", 
                position.Id, position.TokenMint);

            var sleeveConfig = GetSleeveConfig(position.Sleeve);
            
            // Get current market data
            var tokenData = await _birdeyeClient.GetTokenDataAsync(position.TokenMint);
            if (tokenData == null)
            {
                return new StrategyResult
                {
                    Signal = StrategySignal.Exit,
                    Sleeve = position.Sleeve,
                    Confidence = 100m,
                    SuggestedSizeSOL = position.SizeSOL,
                    Reason = "Cannot retrieve token data - force exit",
                    Metadata = { { "tokenData", false } }
                };
            }

            var currentPrice = tokenData.Price;
            var currentValueSOL = position.SizeSOL * (currentPrice / position.AvgEntry);
            var pnlPercent = ((currentPrice - position.AvgEntry) / position.AvgEntry) * 100m;

            // Check hard stop loss (-100%)
            if (pnlPercent <= -100m)
            {
                return new StrategyResult
                {
                    Signal = StrategySignal.Exit,
                    Sleeve = position.Sleeve,
                    Confidence = 100m,
                    SuggestedSizeSOL = position.SizeSOL,
                    Reason = $"Hard stop loss hit: {pnlPercent:F1}%",
                    Metadata = { { "pnlPercent", pnlPercent }, { "trigger", "hard_stop_loss" } }
                };
            }

            // Evaluate sleeve-specific exit strategy
            var exitResult = position.Sleeve switch
            {
                TradingSleeve.Scalps => await EvaluateScalpsExitAsync(position, tokenData, sleeveConfig),
                TradingSleeve.Momentum => await EvaluateMomentumExitAsync(position, tokenData, sleeveConfig),
                TradingSleeve.Swing => await EvaluateSwingExitAsync(position, tokenData, sleeveConfig),
                _ => new StrategyResult 
                { 
                    Signal = StrategySignal.None, 
                    Sleeve = position.Sleeve, 
                    Confidence = 0m, 
                    SuggestedSizeSOL = 0m, 
                    Reason = "Unknown sleeve" 
                }
            };

            if (exitResult.Signal == StrategySignal.Exit || exitResult.Signal == StrategySignal.Sell)
            {
                _logger.LogInformation("Exit signal for position {PositionId}: {Signal} ({Confidence:F1}% confidence) - {Reason}",
                    position.Id, exitResult.Signal, exitResult.Confidence, exitResult.Reason);
            }

            return exitResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating exit for position {PositionId}", position.Id);
            return new StrategyResult
            {
                Signal = StrategySignal.Exit,
                Sleeve = position.Sleeve,
                Confidence = 100m,
                SuggestedSizeSOL = position.SizeSOL,
                Reason = $"Evaluation error - force exit: {ex.Message}",
                Metadata = { { "error", ex.Message } }
            };
        }
    }

    public async Task<List<StrategyResult>> EvaluateAllExitsAsync(List<Position> openPositions)
    {
        var exitTasks = openPositions.Select(EvaluateExitAsync);
        var results = await Task.WhenAll(exitTasks);
        return results.ToList();
    }

    private async Task<StrategyResult> EvaluateScalpsEntryAsync(PoolCandidate candidate, TokenData tokenData, SleeveConfig config)
    {
        // SCALPS Strategy: Target mcap < $200k or NEW_POOL < 24h
        // Entry: NEW_POOL or FLOW + 3 green 15s candles + NetInflow rising (1-3m) + Buyer:Seller ≥ 1.3

        var candles = await _birdeyeClient.GetCandlesAsync(candidate.TokenMint, "15s", DateTime.UtcNow.AddMinutes(-15));
        
        if (candles.Count < 3)
        {
            return CreateNoSignalResult(TradingSleeve.Scalps, "Insufficient candle data");
        }

        var confidence = 0m;
        var reasons = new List<string>();

        // Check for 3 consecutive green candles
        var hasGreenCandles = Indicators.HasConsecutiveGreenCandles(candles, 3);
        if (hasGreenCandles)
        {
            confidence += 30m;
            reasons.Add("3 green 15s candles");
        }

        // Check net inflow rising
        var netInflow1m = await _birdeyeClient.CalculateNetInflowAsync(candidate.TokenMint, TimeSpan.FromMinutes(1));
        var netInflow3m = await _birdeyeClient.CalculateNetInflowAsync(candidate.TokenMint, TimeSpan.FromMinutes(3));
        
        if (netInflow1m > 0 && netInflow1m > netInflow3m / 3)
        {
            confidence += 25m;
            reasons.Add($"NetInflow rising (${netInflow1m:F0})");
        }

        // Check buyer/seller ratio
        var buyerSellerRatio = await _birdeyeClient.CalculateBuyerSellerRatioAsync(candidate.TokenMint, TimeSpan.FromMinutes(5));
        if (buyerSellerRatio >= 1.3m)
        {
            confidence += 20m;
            reasons.Add($"B/S ratio {buyerSellerRatio:F2}");
        }

        // Bonus for new pool
        if (candidate.Source == CandidateSource.NewPool)
        {
            confidence += 15m;
            reasons.Add("New pool");
        }

        // Check EMA trend (optional)
        var prices = candles.Select(c => c.Close).ToList();
        if (Indicators.IsTrendingUp(prices, 5, 10))
        {
            confidence += 10m;
            reasons.Add("EMA trending up");
        }

        var signal = confidence >= 60m ? StrategySignal.Buy : StrategySignal.None;
        var suggestedSize = signal == StrategySignal.Buy ? 
            Math.Min(config.PosSOLMax, Math.Max(config.PosSOLMin, config.PosSOLMin * (confidence / 50m))) : 0m;

        return new StrategyResult
        {
            Signal = signal,
            Sleeve = TradingSleeve.Scalps,
            Confidence = confidence,
            SuggestedSizeSOL = suggestedSize,
            Reason = reasons.Any() ? string.Join(", ", reasons) : "Criteria not met",
            Metadata = {
                { "hasGreenCandles", hasGreenCandles },
                { "netInflow1m", netInflow1m },
                { "netInflow3m", netInflow3m },
                { "buyerSellerRatio", buyerSellerRatio },
                { "candleCount", candles.Count }
            }
        };
    }

    private async Task<StrategyResult> EvaluateMomentumEntryAsync(PoolCandidate candidate, TokenData tokenData, SleeveConfig config)
    {
        // MOMENTUM Strategy: $10M–$100M mcap
        // Entry: 3×5m rising volume + rising unique buyers; price > VWAP(30m) & EMA20(5m) up; NetInflow positive & expanding 10–20m

        var candles = await _birdeyeClient.GetCandlesAsync(candidate.TokenMint, "5m", DateTime.UtcNow.AddMinutes(-60));
        
        if (candles.Count < 20)
        {
            return CreateNoSignalResult(TradingSleeve.Momentum, "Insufficient candle data");
        }

        var confidence = 0m;
        var reasons = new List<string>();

        // Check rising volume (3x5m)
        var isVolumeRising = Indicators.IsVolumeRising(candles, 3);
        if (isVolumeRising)
        {
            confidence += 25m;
            reasons.Add("Rising volume");
        }

        // Check price above VWAP(30m)
        var currentPrice = tokenData.Price;
        var isPriceAboveVWAP = Indicators.IsPriceAboveVWAP(currentPrice, candles, 6); // 6 * 5m = 30m
        if (isPriceAboveVWAP)
        {
            confidence += 20m;
            reasons.Add("Price > VWAP");
        }

        // Check EMA trend
        var prices = candles.Select(c => c.Close).ToList();
        var isTrendingUp = Indicators.IsTrendingUp(prices, 9, 20);
        if (isTrendingUp)
        {
            confidence += 20m;
            reasons.Add("EMA trending up");
        }

        // Check net inflow expanding
        var netInflow10m = await _birdeyeClient.CalculateNetInflowAsync(candidate.TokenMint, TimeSpan.FromMinutes(10));
        var netInflow20m = await _birdeyeClient.CalculateNetInflowAsync(candidate.TokenMint, TimeSpan.FromMinutes(20));
        
        if (netInflow10m > 0 && netInflow10m > netInflow20m / 2)
        {
            confidence += 25m;
            reasons.Add($"NetInflow expanding (${netInflow10m:F0})");
        }

        // Check unique buyers delta
        var uniqueBuyersDelta = await _birdeyeClient.CalculateUniqueBuyersDeltaAsync(candidate.TokenMint, TimeSpan.FromMinutes(10));
        if (uniqueBuyersDelta > 0)
        {
            confidence += 10m;
            reasons.Add($"Unique buyers +{uniqueBuyersDelta}");
        }

        var signal = confidence >= 70m ? StrategySignal.Buy : StrategySignal.None;
        var suggestedSize = signal == StrategySignal.Buy ? 
            Math.Min(config.PosSOLMax, Math.Max(config.PosSOLMin, config.PosSOLMin * (confidence / 60m))) : 0m;

        return new StrategyResult
        {
            Signal = signal,
            Sleeve = TradingSleeve.Momentum,
            Confidence = confidence,
            SuggestedSizeSOL = suggestedSize,
            Reason = reasons.Any() ? string.Join(", ", reasons) : "Criteria not met",
            Metadata = {
                { "isVolumeRising", isVolumeRising },
                { "isPriceAboveVWAP", isPriceAboveVWAP },
                { "isTrendingUp", isTrendingUp },
                { "netInflow10m", netInflow10m },
                { "netInflow20m", netInflow20m },
                { "uniqueBuyersDelta", uniqueBuyersDelta },
                { "candleCount", candles.Count }
            }
        };
    }

    private async Task<StrategyResult> EvaluateSwingEntryAsync(PoolCandidate candidate, TokenData tokenData, SleeveConfig config)
    {
        // SWING Strategy: High conviction, deeper LP
        // Entry: deeper LP; higher low + MA20(1h) reclaim; NetInflow positive 2–6h; optional vol squeeze breakout

        var candles = await _birdeyeClient.GetCandlesAsync(candidate.TokenMint, "1h", DateTime.UtcNow.AddHours(-24));
        
        if (candles.Count < 20)
        {
            return CreateNoSignalResult(TradingSleeve.Swing, "Insufficient candle data");
        }

        var confidence = 0m;
        var reasons = new List<string>();

        // Check deeper LP (higher threshold)
        var liquidityUSD = tokenData.Liquidity;
        if (liquidityUSD >= _config.Guards.MinLPUSD * 2) // Double the minimum
        {
            confidence += 20m;
            reasons.Add($"Deep liquidity ${liquidityUSD:F0}");
        }

        // Check higher low pattern
        var recentLows = candles.TakeLast(5).Select(c => c.Low).ToList();
        if (recentLows.Count >= 2 && recentLows.Last() > recentLows[^2])
        {
            confidence += 25m;
            reasons.Add("Higher low");
        }

        // Check MA20 reclaim
        var prices = candles.Select(c => c.Close).ToList();
        var ma20 = prices.TakeLast(20).Average();
        var currentPrice = tokenData.Price;
        if (currentPrice > ma20)
        {
            confidence += 20m;
            reasons.Add("Price > MA20");
        }

        // Check net inflow positive over longer timeframe
        var netInflow2h = await _birdeyeClient.CalculateNetInflowAsync(candidate.TokenMint, TimeSpan.FromHours(2));
        var netInflow6h = await _birdeyeClient.CalculateNetInflowAsync(candidate.TokenMint, TimeSpan.FromHours(6));
        
        if (netInflow2h > 0 && netInflow6h > 0)
        {
            confidence += 25m;
            reasons.Add($"Sustained inflow (${netInflow2h:F0})");
        }

        // Volume squeeze breakout (optional)
        var volatility = Indicators.CalculateVolatility(prices, 10);
        if (volatility < 5m) // Low volatility indicates squeeze
        {
            confidence += 10m;
            reasons.Add("Low volatility (squeeze)");
        }

        var signal = confidence >= 75m ? StrategySignal.Buy : StrategySignal.None;
        var suggestedSize = signal == StrategySignal.Buy ? 
            Math.Min(config.PosSOLMax, Math.Max(config.PosSOLMin, config.PosSOLMin * (confidence / 70m))) : 0m;

        return new StrategyResult
        {
            Signal = signal,
            Sleeve = TradingSleeve.Swing,
            Confidence = confidence,
            SuggestedSizeSOL = suggestedSize,
            Reason = reasons.Any() ? string.Join(", ", reasons) : "Criteria not met",
            Metadata = {
                { "liquidityUSD", liquidityUSD },
                { "currentPrice", currentPrice },
                { "ma20", ma20 },
                { "netInflow2h", netInflow2h },
                { "netInflow6h", netInflow6h },
                { "volatility", volatility },
                { "candleCount", candles.Count }
            }
        };
    }

    // Exit strategy methods would be implemented here...
    // For brevity, I'll implement simplified versions

    private async Task<StrategyResult> EvaluateScalpsExitAsync(Position position, TokenData tokenData, SleeveConfig config)
    {
        var currentPrice = tokenData.Price;
        var pnlPercent = ((currentPrice - position.AvgEntry) / position.AvgEntry) * 100m;
        
        // Take profit levels: 20%, 40%, 80%
        var tpLevels = config.TpPercents;
        var exitPercent = 0m;
        
        foreach (var tpLevel in tpLevels)
        {
            if (pnlPercent >= tpLevel)
            {
                exitPercent = 100m / tpLevels.Count; // Equal splits
                break;
            }
        }

        if (exitPercent > 0)
        {
            return new StrategyResult
            {
                Signal = StrategySignal.Sell,
                Sleeve = position.Sleeve,
                Confidence = 100m,
                SuggestedSizeSOL = position.SizeSOL * (exitPercent / 100m),
                Reason = $"Take profit at +{pnlPercent:F1}%",
                Metadata = { { "pnlPercent", pnlPercent }, { "trigger", "take_profit" } }
            };
        }

        // Check stall condition
        var age = DateTime.UtcNow - position.OpenedAt;
        if (age.TotalSeconds > config.StallSecs && pnlPercent < 5m)
        {
            return new StrategyResult
            {
                Signal = StrategySignal.Exit,
                Sleeve = position.Sleeve,
                Confidence = 90m,
                SuggestedSizeSOL = position.SizeSOL,
                Reason = $"Stall exit after {age.TotalSeconds:F0}s",
                Metadata = { { "ageSeconds", age.TotalSeconds }, { "trigger", "stall" } }
            };
        }

        return CreateNoSignalResult(position.Sleeve, "No exit criteria met");
    }

    private async Task<StrategyResult> EvaluateMomentumExitAsync(Position position, TokenData tokenData, SleeveConfig config)
    {
        var currentPrice = tokenData.Price;
        var pnlPercent = ((currentPrice - position.AvgEntry) / position.AvgEntry) * 100m;
        
        // Take profit levels: 40%, 80%, 120%
        // Simplified implementation
        if (pnlPercent >= 40m)
        {
            return new StrategyResult
            {
                Signal = StrategySignal.Sell,
                Sleeve = position.Sleeve,
                Confidence = 100m,
                SuggestedSizeSOL = position.SizeSOL * 0.33m, // Sell 1/3
                Reason = $"Take profit at +{pnlPercent:F1}%",
                Metadata = { { "pnlPercent", pnlPercent }, { "trigger", "take_profit" } }
            };
        }

        return CreateNoSignalResult(position.Sleeve, "No exit criteria met");
    }

    private async Task<StrategyResult> EvaluateSwingExitAsync(Position position, TokenData tokenData, SleeveConfig config)
    {
        var currentPrice = tokenData.Price;
        var pnlPercent = ((currentPrice - position.AvgEntry) / position.AvgEntry) * 100m;
        
        // Take profit levels: 100%, 200%, 400%
        // Simplified implementation
        if (pnlPercent >= 100m)
        {
            return new StrategyResult
            {
                Signal = StrategySignal.Sell,
                Sleeve = position.Sleeve,
                Confidence = 100m,
                SuggestedSizeSOL = position.SizeSOL * 0.33m, // Sell 1/3
                Reason = $"Take profit at +{pnlPercent:F1}%",
                Metadata = { { "pnlPercent", pnlPercent }, { "trigger", "take_profit" } }
            };
        }

        return CreateNoSignalResult(position.Sleeve, "No exit criteria met");
    }

    private TradingSleeve DetermineSleeve(PoolCandidate candidate, decimal marketCapUSD)
    {
        // NEW_POOL with small mcap -> Scalps
        if (candidate.Source == CandidateSource.NewPool && marketCapUSD < 200_000)
            return TradingSleeve.Scalps;

        // Medium mcap range -> Momentum
        if (marketCapUSD >= 10_000_000 && marketCapUSD <= 100_000_000)
            return TradingSleeve.Momentum;

        // High conviction flow signals -> Swing
        if (candidate.Source == CandidateSource.Flow && candidate.Score > 80)
            return TradingSleeve.Swing;

        // Default to Scalps for small cap
        return TradingSleeve.Scalps;
    }

    private SleeveConfig GetSleeveConfig(TradingSleeve sleeve)
    {
        return sleeve switch
        {
            TradingSleeve.Scalps => _config.Sleeves.Scalps,
            TradingSleeve.Momentum => _config.Sleeves.Momentum,
            TradingSleeve.Swing => _config.Sleeves.Swing,
            _ => _config.Sleeves.Scalps
        };
    }

    private bool IsMarketCapInRange(decimal marketCapUSD, TradingSleeve sleeve)
    {
        var sleeveConfig = GetSleeveConfig(sleeve);
        
        return sleeve switch
        {
            TradingSleeve.Scalps => sleeveConfig.TargetMcapMaxUSD == null || marketCapUSD <= sleeveConfig.TargetMcapMaxUSD,
            TradingSleeve.Momentum => marketCapUSD >= (sleeveConfig.McapMinUSD ?? 0) && 
                                    marketCapUSD <= (sleeveConfig.McapMaxUSD ?? decimal.MaxValue),
            TradingSleeve.Swing => true, // No specific market cap limits for swing
            _ => true
        };
    }

    private static StrategyResult CreateNoSignalResult(TradingSleeve sleeve, string reason)
    {
        return new StrategyResult
        {
            Signal = StrategySignal.None,
            Sleeve = sleeve,
            Confidence = 0m,
            SuggestedSizeSOL = 0m,
            Reason = reason
        };
    }
}