using SolanaTradingBot.Discovery;

namespace SolanaTradingBot.Execution;

public enum ExecuteResult
{
    Success,
    Failed,
    Rejected
}

public class ExecutionContext
{
    public required PoolCandidate Candidate { get; set; }
    public required string Sleeve { get; set; }
    public required decimal SizeSOL { get; set; }
    public required int SlippageBps { get; set; }
    public string? SimulationSignature { get; set; }
    public string? TransactionSignature { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IExecutor
{
    /// <summary>
    /// Execute a buy trade for the given candidate
    /// </summary>
    Task<ExecuteResult> ExecuteBuyAsync(ExecutionContext context);
    
    /// <summary>
    /// Execute a sell trade
    /// </summary>
    Task<ExecuteResult> ExecuteSellAsync(string tokenMint, decimal tokenAmount, int slippageBps);
    
    /// <summary>
    /// Simulate a transaction before execution
    /// </summary>
    Task<bool> SimulateTransactionAsync(ExecutionContext context);
}