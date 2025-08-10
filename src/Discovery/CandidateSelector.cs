using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SolanaTradingBot.Engine;
using System.Collections.Concurrent;

namespace SolanaTradingBot.Discovery;

public interface ICandidateSelector
{
    event Action<PoolCandidate>? CandidateSelected;
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    List<PoolCandidate> GetCurrentCandidates();
    void AddCandidate(PoolCandidate candidate);
}

public class CandidateSelector : BackgroundService, ICandidateSelector
{
    private readonly ILogger<CandidateSelector> _logger;
    private readonly TradingConfig _config;
    private readonly IPoolDetector _poolDetector;
    private readonly IFlowScanner _flowScanner;
    
    private readonly ConcurrentDictionary<string, PoolCandidate> _candidates = new();
    
    public event Action<PoolCandidate>? CandidateSelected;

    public CandidateSelector(
        ILogger<CandidateSelector> logger, 
        IOptions<TradingConfig> config,
        IPoolDetector poolDetector,
        IFlowScanner flowScanner)
    {
        _logger = logger;
        _config = config.Value;
        _poolDetector = poolDetector;
        _flowScanner = flowScanner;
        
        // Subscribe to events
        _poolDetector.NewPoolDetected += OnNewPoolDetected;
        _flowScanner.FlowDetected += OnFlowDetected;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Candidate selector started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Clean up expired candidates
                CleanupExpiredCandidates();
                
                // Rank and select candidates
                await ProcessCandidates();
                
                // Wait before next processing cycle
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Candidate selector stopping...");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in candidate selection process");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting candidate selector...");
        await base.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping candidate selector...");
        
        // Unsubscribe from events
        _poolDetector.NewPoolDetected -= OnNewPoolDetected;
        _flowScanner.FlowDetected -= OnFlowDetected;
        
        await base.StopAsync(cancellationToken);
    }

    public List<PoolCandidate> GetCurrentCandidates()
    {
        return _candidates.Values
            .Where(c => !c.IsExpired)
            .OrderByDescending(c => c.Score)
            .ToList();
    }

    public void AddCandidate(PoolCandidate candidate)
    {
        _candidates.AddOrUpdate(candidate.TokenMint, candidate, (key, existing) =>
        {
            // Merge candidates for the same token, keeping the highest score
            if (candidate.Score > existing.Score)
            {
                candidate.Metadata = MergeMetadata(existing.Metadata, candidate.Metadata);
                return candidate;
            }
            else
            {
                existing.Metadata = MergeMetadata(existing.Metadata, candidate.Metadata);
                return existing;
            }
        });

        _logger.LogDebug("Added/updated candidate: {TokenMint} (Score: {Score}, Source: {Source})", 
            candidate.TokenMint, candidate.Score, candidate.Source);
    }

    private void OnNewPoolDetected(NewPoolEvent poolEvent)
    {
        var candidate = new PoolCandidate
        {
            TokenMint = poolEvent.TokenMint,
            PoolAddress = poolEvent.PoolAddress,
            Source = CandidateSource.NewPool,
            Type = poolEvent.Type,
            Score = CalculateNewPoolScore(poolEvent),
            Metadata = new Dictionary<string, object>(poolEvent.PoolData)
            {
                { "detectedAt", poolEvent.Timestamp },
                { "transactionId", poolEvent.TransactionId ?? "" },
                { "programId", poolEvent.ProgramId }
            }
        };

        AddCandidate(candidate);
        
        _logger.LogInformation("New pool candidate: {TokenMint} from {Type} (Score: {Score})", 
            candidate.TokenMint, candidate.Type, candidate.Score);
    }

    private void OnFlowDetected(FlowEvent flowEvent)
    {
        var candidate = new PoolCandidate
        {
            TokenMint = flowEvent.TokenMint,
            PoolAddress = "", // Flow events don't have pool address
            Source = CandidateSource.Flow,
            Type = CandidateType.Other, // Will be determined later
            Score = CalculateFlowScore(flowEvent),
            Metadata = new Dictionary<string, object>(flowEvent.FlowData)
            {
                { "detectedAt", flowEvent.Timestamp },
                { "netInflow", flowEvent.NetInflow },
                { "volume24h", flowEvent.Volume24h },
                { "uniqueBuyers", flowEvent.UniqueBuyers }
            }
        };

        AddCandidate(candidate);
        
        _logger.LogInformation("Flow candidate: {TokenMint} (NetInflow: ${NetInflow:F0}, Score: {Score})", 
            candidate.TokenMint, flowEvent.NetInflow, candidate.Score);
    }

    private void CleanupExpiredCandidates()
    {
        var expiredKeys = _candidates
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            if (_candidates.TryRemove(key, out var expired))
            {
                _logger.LogDebug("Removed expired candidate: {TokenMint} (Age: {Age})", 
                    expired.TokenMint, DateTime.UtcNow - expired.DiscoveredAt);
            }
        }
    }

    private async Task ProcessCandidates()
    {
        var candidates = GetCurrentCandidates();
        
        if (!candidates.Any())
            return;

        _logger.LogDebug("Processing {Count} candidates", candidates.Count);

        // Select top candidates that meet minimum score threshold
        var selectedCandidates = candidates
            .Where(c => c.Score >= GetMinimumScore(c.Source))
            .Take(5) // Limit to top 5 to avoid overwhelming the system
            .ToList();

        foreach (var candidate in selectedCandidates)
        {
            try
            {
                _logger.LogInformation("Selected candidate: {TokenMint} (Score: {Score}, Source: {Source}, Type: {Type})", 
                    candidate.TokenMint, candidate.Score, candidate.Source, candidate.Type);

                CandidateSelected?.Invoke(candidate);
                
                // Remove selected candidate to avoid reprocessing
                _candidates.TryRemove(candidate.TokenMint, out _);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing candidate {TokenMint}", candidate.TokenMint);
            }
        }
    }

    private decimal CalculateNewPoolScore(NewPoolEvent poolEvent)
    {
        decimal score = 100; // Base score for new pools

        // Boost score based on pool type
        score += poolEvent.Type switch
        {
            CandidateType.PumpFun => 50,  // PumpFun pools get high priority
            CandidateType.Raydium => 30,  // Established DEX
            CandidateType.Orca => 25,     // Established DEX
            CandidateType.Jupiter => 20,  // Aggregator
            _ => 10
        };

        // Boost based on recency (fresher pools get higher scores)
        var ageMinutes = (DateTime.UtcNow - poolEvent.Timestamp).TotalMinutes;
        if (ageMinutes < 5) score += 30;
        else if (ageMinutes < 15) score += 20;
        else if (ageMinutes < 30) score += 10;

        return score;
    }

    private decimal CalculateFlowScore(FlowEvent flowEvent)
    {
        decimal score = 50; // Base score for flow events

        // Score based on net inflow
        if (flowEvent.NetInflow > 50000) score += 40;
        else if (flowEvent.NetInflow > 20000) score += 30;
        else if (flowEvent.NetInflow > 10000) score += 20;
        else if (flowEvent.NetInflow > 0) score += 10;

        // Score based on volume
        if (flowEvent.Volume24h > 500000) score += 30;
        else if (flowEvent.Volume24h > 200000) score += 20;
        else if (flowEvent.Volume24h > 100000) score += 10;

        // Score based on unique buyers
        if (flowEvent.UniqueBuyers > 200) score += 20;
        else if (flowEvent.UniqueBuyers > 100) score += 15;
        else if (flowEvent.UniqueBuyers > 50) score += 10;

        return score;
    }

    private decimal GetMinimumScore(CandidateSource source)
    {
        return source switch
        {
            CandidateSource.NewPool => 80,    // High threshold for new pools
            CandidateSource.Flow => 60,       // Medium threshold for flow
            CandidateSource.Trending => 70,   // Medium-high for trending
            _ => 50
        };
    }

    private static Dictionary<string, object> MergeMetadata(Dictionary<string, object> existing, Dictionary<string, object> newData)
    {
        var merged = new Dictionary<string, object>(existing);
        
        foreach (var kvp in newData)
        {
            merged[kvp.Key] = kvp.Value;
        }
        
        return merged;
    }
}