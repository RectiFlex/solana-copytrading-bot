using System.ComponentModel.DataAnnotations;

namespace SolanaTradingBot.State;

public enum TradeSide
{
    Buy,
    Sell
}

public enum CloseReason
{
    TakeProfit,
    StopLoss,
    Stall,
    TimeLimit,
    Concentration,
    Manual
}

public enum TradingSleeve
{
    Scalps,
    Momentum,
    Swing
}

public class Position
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public required string TokenMint { get; set; }
    public TradeSide Side { get; set; }
    public decimal SizeSOL { get; set; }
    public decimal AvgEntry { get; set; }
    public decimal HighWaterMark { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public CloseReason? CloseReason { get; set; }
    public decimal RealizedPnlSOL { get; set; }
    public decimal RealizedPnlUSD { get; set; }
    public TradingSleeve Sleeve { get; set; }
    public bool IsOpen => !ClosedAt.HasValue;
    
    // Navigation property
    public List<Trade> Trades { get; set; } = new();
}

public class Trade
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid PositionId { get; set; }
    public TradeSide Side { get; set; }
    public decimal Qty { get; set; }
    public decimal Price { get; set; }
    public decimal Fees { get; set; }
    public DateTime Timestamp { get; set; }
    public string? TransactionId { get; set; }
    
    // Navigation property
    public Position Position { get; set; } = null!;
}

public class DailyPnL
{
    [Key]
    public DateOnly Date { get; set; }
    
    public decimal TotalPnlSOL { get; set; }
    public decimal TotalPnlUSD { get; set; }
    public decimal ScalpsPnlSOL { get; set; }
    public decimal MomentumPnlSOL { get; set; }
    public decimal SwingPnlSOL { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal MaxDrawdownPct { get; set; }
}
