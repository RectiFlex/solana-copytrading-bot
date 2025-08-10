namespace SolanaTradingBot.Discovery;

public enum CandidateSource
{
    NewPool,
    Flow,
    Trending
}

public enum CandidateType
{
    PumpFun,
    Raydium,
    Orca,
    Jupiter,
    Other
}

public class PoolCandidate
{
    public required string TokenMint { get; set; }
    public required string PoolAddress { get; set; }
    public CandidateSource Source { get; set; }
    public CandidateType Type { get; set; }
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    public decimal Score { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    // TTL - candidates expire after this time
    public DateTime ExpiresAt => DiscoveredAt.AddMinutes(GetTtlMinutes());
    
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    
    private int GetTtlMinutes()
    {
        return Source switch
        {
            CandidateSource.NewPool => 30, // New pools get highest priority, expire in 30 minutes
            CandidateSource.Flow => 15,    // Flow signals expire faster
            CandidateSource.Trending => 60, // Trending can stay longer
            _ => 15
        };
    }
}

public class NewPoolEvent
{
    public required string TokenMint { get; set; }
    public required string PoolAddress { get; set; }
    public required string ProgramId { get; set; }
    public CandidateType Type { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? TransactionId { get; set; }
    public Dictionary<string, object> PoolData { get; set; } = new();
}

public class FlowEvent
{
    public required string TokenMint { get; set; }
    public decimal NetInflow { get; set; }
    public decimal Volume24h { get; set; }
    public int UniqueBuyers { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> FlowData { get; set; } = new();
}
