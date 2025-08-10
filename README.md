# Solana Autonomous Meme-Coin Trading Bot

A powerful and autonomous trading bot built in **C#/.NET 8** for the **Solana blockchain**. This bot has been completely refactored from a copy-trading system to an **autonomous strategy** that discovers, analyzes, and trades meme-coins without relying on other wallets.

## 🚨 Complete Refactor Notice

**This repository has been completely transformed** from a copy-trading bot to an autonomous trading system. All copy-trading functionality has been removed and replaced with sophisticated autonomous trading strategies.

## 🎯 Key Features

### Autonomous Discovery
- **Real-time pool detection** via Helius WebSocket monitoring of DEX programs
- **Flow scanning** from data vendors to identify tokens with unusual volume/inflow patterns
- **Candidate scoring** with TTL management and automatic cleanup

### Three Trading Sleeves
1. **Scalps (70% allocation)**: Target new pools < $200k market cap
   - Entry: 3 green 15s candles + rising net inflow + buyer/seller ratio ≥ 1.3
   - Exit: Take profit at +20%/+40%/+80%, hard stop-loss at -100%

2. **Momentum (20% allocation)**: $10M-$100M market cap range
   - Entry: Rising volume + price > VWAP + EMA trend + expanding net inflow
   - Exit: Take profit at +40%/+80%/+120%, trailing stop from high

3. **Swing (10% allocation)**: High conviction trades
   - Entry: Deep liquidity + higher low + MA20 reclaim + sustained inflow
   - Exit: Take profit at +100%/+200%/+400%, de-risk on decay

### Comprehensive Risk Management
- **Hard stop-loss** at -100% per position (no position can lose more than its initial size)
- **Daily drawdown limits** (default 5% of bankroll)
- **Per-token exposure caps** (default 3% of bankroll)
- **Loss streak cooldowns** (30-minute timeout after 3 consecutive losses)

### Advanced Safety Guards
- **Honeypot detection** via simulation
- **Liquidity validation** (minimum $50k LP)
- **Holder distribution checks** (minimum 30 holders in 10m)
- **Concentration limits** (top 10 holders < 45%)
- **LP pull protection** (block if >15% LP removed in 10m)
- **Data freshness monitoring** (block trades if feeds stale)

## 🛠 Technology Stack

- **.NET 8** with C# 12
- **Solnet** for Solana RPC/WebSocket communication
- **Entity Framework Core** with SQLite for persistence
- **Refit** for HTTP API clients (Jupiter, Birdeye)
- **Serilog** for structured logging
- **ASP.NET Core** for health endpoints and monitoring

## 📁 Project Structure

```
/src
  /Common              # Shared configuration models
  /Discovery           # Pool detection and candidate selection
    PoolDetector.cs      # WebSocket monitoring of DEX programs
    FlowScanner.cs       # Volume/inflow analysis from data vendors
    CandidateSelector.cs # Candidate scoring, merging, and TTL management
  /DataVendors         # External API clients
    JupiterClient.cs     # Jupiter quotes, swaps, and simulation
    BirdeyeClient.cs     # Market data, candles, trades, holders
  /Engine              # Core trading logic
    Indicators.cs        # Technical analysis (EMA, VWAP, ATR, RSI, etc.)
    Guards.cs           # Safety checks and risk validation
    Strategy.cs         # Three-sleeve trading strategies
  /Execution           # Trade execution
    Executor.cs         # Jupiter swap execution with adaptive slippage
  /State               # Data persistence
    Models.cs           # Position, Trade, DailyPnL entities
    Repository.cs       # CRUD operations and analytics
  /CLI                 # Command-line interface
    Commands.cs         # paper, start, backtest, report, watch
/tests                 # Unit and integration tests
```

## ⚙️ Configuration

All trading parameters are configurable via `appsettings.json`:

```json
{
  "Bankroll": {
    "TotalSOL": 5,
    "ScalpsPct": 70,
    "MomentumPct": 20,
    "SwingPct": 10
  },
  "Sleeves": {
    "Scalps": {
      "PosSOLMin": 0.02,
      "PosSOLMax": 0.05,
      "TpPercents": [20, 40, 80],
      "MaxConcurrent": 6
    }
    // ... more sleeve configurations
  },
  "Guards": {
    "MinLPUSD": 50000,
    "MinHolders10m": 30,
    "BuyerSellerMin": 1.2,
    "MaxTop10Pct": 45
  },
  "Risk": {
    "MaxDailyDrawdownPctOfBankroll": 5,
    "PerTokenExposurePctOfBankroll": 3
  }
}
```

## 🚀 Getting Started

### Prerequisites
- .NET 8 SDK
- Helius API key for WebSocket access
- Optional: Birdeye API key for enhanced market data

### Setup
1. Clone the repository
2. Configure API keys in `appsettings.Development.json`
3. Build the solution: `dotnet build`
4. Run in paper trading mode: `dotnet run --project src/CLI -- paper`

### CLI Commands
- `paper` - Paper trading with simulated fills
- `start` - Live trading (requires wallet setup)
- `backtest` - Historical strategy testing
- `report` - Daily P&L and performance metrics
- `watch` - Live market monitoring

## 📊 Trading Strategy Details

### Entry Criteria by Sleeve

**Scalps** (fast, small positions):
- New pool detected OR significant flow signal
- 3 consecutive green 15-second candles
- Rising net inflow over 1-3 minutes
- Buyer/seller ratio ≥ 1.3
- Optional: Price above EMA(9) > EMA(20)

**Momentum** (medium-term trends):
- 3 periods of rising 5-minute volume
- Price above VWAP(30m) and EMA(20) trending up
- Net inflow positive and expanding over 10-20 minutes
- Rising unique buyer count

**Swing** (high conviction):
- Deep liquidity pool (2x minimum threshold)
- Higher low formation + MA(20) reclaim
- Sustained positive net inflow over 2-6 hours
- Optional: Volume squeeze breakout

### Exit Management
All sleeves enforce the **-100% hard stop-loss**. Additional exits include:
- **Take profit levels**: Laddered exits at predefined profit targets
- **Trailing stops**: Lock in profits on momentum trades
- **Stall detection**: Exit if momentum fades (scalps only)
- **Time-based exits**: Maximum holding periods
- **Concentration monitoring**: Exit if whale activity increases

## 🛡 Risk Management

### Position-Level Risk
- **Maximum loss per position**: 100% of position size (hard coded)
- **Position sizing**: Dynamic based on sleeve allocation and confidence
- **Exposure limits**: Maximum 3% of bankroll per token

### Portfolio-Level Risk
- **Daily drawdown limit**: 5% of total bankroll
- **Sleeve allocation**: 70% scalps, 20% momentum, 10% swing
- **Concurrent position limits**: 6 scalps, 3 momentum, 2 swing
- **Cool-down periods**: 30 minutes after 3 consecutive losses

### Data & Execution Risk
- **Feed monitoring**: Block new trades if data feeds are stale
- **Simulation**: Optional pre-trade simulation via Jupiter
- **Slippage protection**: Adaptive slippage based on volatility
- **Retry logic**: Smart retry with increasing slippage tolerance

## 📈 Performance Monitoring

The bot tracks comprehensive metrics:
- **Daily P&L** by sleeve and overall
- **Win/loss ratios** and average trade size
- **Maximum drawdown** and recovery periods
- **Execution quality** (slippage, fill rates)
- **Discovery effectiveness** (candidate → trade conversion)

## ⚠️ Important Notes

### Migration from Copy-Trading
This bot **NO LONGER** supports copy-trading functionality. Key changes:
- **Removed**: Wallet monitoring, transaction mirroring, smart-money following
- **Added**: Autonomous discovery, technical analysis, risk management
- **Configuration**: Completely new configuration structure

### Risk Disclaimer
- **Educational purposes only** - use at your own risk
- **Cryptocurrency trading** carries significant financial risk
- **Meme-coins** are highly volatile and speculative
- **Start with paper trading** to understand the system
- **Never risk more than you can afford to lose**

### Development Status
This is a comprehensive refactor with the following status:
- ✅ **Core architecture**: Complete and functional
- ✅ **Discovery system**: Implemented with simulation
- ✅ **Trading strategies**: All three sleeves implemented
- ✅ **Risk management**: Comprehensive safety systems
- ⚠️ **Real trading**: Requires wallet integration and testing
- ⚠️ **Production**: Needs thorough testing and monitoring

## 🤝 Contributing

This refactor represents a complete architectural change. Future contributions should focus on:
- Strategy refinement and backtesting
- Additional data vendor integrations
- Enhanced risk management features
- Performance optimizations
- Monitoring and alerting improvements

## 📄 License

Educational and research purposes only. See LICENSE file for details.

---

**⚡ This bot trades autonomously based on market microstructure, not wallet copying. Always start with paper trading and understand the risks involved.**