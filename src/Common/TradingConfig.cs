namespace SolanaTradingBot.Common;

public class BankrollConfig
{
    public decimal TotalSOL { get; set; }
    public int ScalpsPct { get; set; }
    public int MomentumPct { get; set; }
    public int SwingPct { get; set; }
}

public class SleeveConfig
{
    public decimal PosSOLMin { get; set; }
    public decimal PosSOLMax { get; set; }
    public List<int> TpPercents { get; set; } = new();
    public int SlPctOfPosition { get; set; }
    public int? StallSecs { get; set; }
    public int? TrailPctFromHigh { get; set; }
    public int MaxConcurrent { get; set; }
    public long? TargetMcapMaxUSD { get; set; }
    public long? McapMinUSD { get; set; }
    public long? McapMaxUSD { get; set; }
    public int? DecayHoursDeRisk { get; set; }
}

public class SleevesConfig
{
    public SleeveConfig Scalps { get; set; } = new();
    public SleeveConfig Momentum { get; set; } = new();
    public SleeveConfig Swing { get; set; } = new();
}

public class GuardsConfig
{
    public long MinLPUSD { get; set; }
    public int MinHolders10m { get; set; }
    public decimal BuyerSellerMin { get; set; }
    public int MaxTop10Pct { get; set; }
    public int LpPullPct10mBlock { get; set; }
    public int DecimalsMin { get; set; }
    public int DecimalsMax { get; set; }
    public int FeedsFreshMs { get; set; }
}

public class SlippageConfig
{
    public int Base { get; set; }
    public decimal KVol { get; set; }
    public int Min { get; set; }
    public int Max { get; set; }
}

public class SlippageBpsConfig
{
    public SlippageConfig Scalps { get; set; } = new();
    public SlippageConfig Momentum { get; set; } = new();
    public SlippageConfig Swing { get; set; } = new();
}

public class RiskConfig
{
    public int MaxDailyDrawdownPctOfBankroll { get; set; }
    public int PerTokenExposurePctOfBankroll { get; set; }
    public int LossStreakCooloffTrades { get; set; }
    public int CooloffMinutes { get; set; }
}

public class JupiterConfig
{
    public required string BaseUrl { get; set; }
    public bool Simulate { get; set; }
    public int MaxRouteHops { get; set; }
}

public class HeliusConfig
{
    public required string Http { get; set; }
    public required string Ws { get; set; }
}

public class JitoConfig
{
    public bool Enabled { get; set; }
    public long TipLamports { get; set; }
}

public class TelegramConfig
{
    public required string BotToken { get; set; }
    public required string ChatId { get; set; }
}

public class AlertsConfig
{
    public required string DiscordWebhook { get; set; }
    public TelegramConfig Telegram { get; set; } = new() { BotToken = "", ChatId = "" };
}

public class TradingConfig
{
    public BankrollConfig Bankroll { get; set; } = new();
    public SleevesConfig Sleeves { get; set; } = new();
    public GuardsConfig Guards { get; set; } = new();
    public SlippageBpsConfig SlippageBps { get; set; } = new();
    public RiskConfig Risk { get; set; } = new();
    public List<string> DiscoveryPrograms { get; set; } = new();
    public JupiterConfig Jupiter { get; set; } = new() { BaseUrl = "" };
    public HeliusConfig Helius { get; set; } = new() { Http = "", Ws = "" };
    public JitoConfig Jito { get; set; } = new();
    public AlertsConfig Alerts { get; set; } = new() { DiscordWebhook = "" };
}
