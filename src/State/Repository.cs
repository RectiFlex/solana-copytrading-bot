using Microsoft.EntityFrameworkCore;

namespace SolanaTradingBot.State;

public interface IRepository
{
    // Position operations
    Task<Position?> GetPositionAsync(Guid id);
    Task<List<Position>> GetOpenPositionsAsync(TradingSleeve? sleeve = null);
    Task<List<Position>> GetPositionsByTokenAsync(string tokenMint);
    Task<Position> CreatePositionAsync(Position position);
    Task<Position> UpdatePositionAsync(Position position);
    Task DeletePositionAsync(Guid id);

    // Trade operations
    Task<Trade> CreateTradeAsync(Trade trade);
    Task<List<Trade>> GetTradesByPositionAsync(Guid positionId);

    // PnL operations
    Task<DailyPnL?> GetDailyPnLAsync(DateOnly date);
    Task<DailyPnL> UpdateDailyPnLAsync(DailyPnL dailyPnL);
    Task<List<DailyPnL>> GetPnLRangeAsync(DateOnly fromDate, DateOnly toDate);

    // Analytics
    Task<decimal> GetCurrentExposureAsync(string tokenMint);
    Task<decimal> GetTotalExposureAsync();
    Task<decimal> GetDailyDrawdownAsync(DateOnly date);
    Task<int> GetRecentLossStreakAsync(TradingSleeve sleeve);
}

public class Repository : IRepository
{
    private readonly TradingDbContext _context;

    public Repository(TradingDbContext context)
    {
        _context = context;
    }

    // Position operations
    public async Task<Position?> GetPositionAsync(Guid id)
    {
        return await _context.Positions
            .Include(p => p.Trades)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Position>> GetOpenPositionsAsync(TradingSleeve? sleeve = null)
    {
        var query = _context.Positions
            .Include(p => p.Trades)
            .Where(p => p.ClosedAt == null);

        if (sleeve.HasValue)
        {
            query = query.Where(p => p.Sleeve == sleeve.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<List<Position>> GetPositionsByTokenAsync(string tokenMint)
    {
        return await _context.Positions
            .Include(p => p.Trades)
            .Where(p => p.TokenMint == tokenMint)
            .OrderByDescending(p => p.OpenedAt)
            .ToListAsync();
    }

    public async Task<Position> CreatePositionAsync(Position position)
    {
        _context.Positions.Add(position);
        await _context.SaveChangesAsync();
        return position;
    }

    public async Task<Position> UpdatePositionAsync(Position position)
    {
        _context.Positions.Update(position);
        await _context.SaveChangesAsync();
        return position;
    }

    public async Task DeletePositionAsync(Guid id)
    {
        var position = await _context.Positions.FindAsync(id);
        if (position != null)
        {
            _context.Positions.Remove(position);
            await _context.SaveChangesAsync();
        }
    }

    // Trade operations
    public async Task<Trade> CreateTradeAsync(Trade trade)
    {
        _context.Trades.Add(trade);
        await _context.SaveChangesAsync();
        return trade;
    }

    public async Task<List<Trade>> GetTradesByPositionAsync(Guid positionId)
    {
        return await _context.Trades
            .Where(t => t.PositionId == positionId)
            .OrderBy(t => t.Timestamp)
            .ToListAsync();
    }

    // PnL operations
    public async Task<DailyPnL?> GetDailyPnLAsync(DateOnly date)
    {
        return await _context.DailyPnLs.FindAsync(date);
    }

    public async Task<DailyPnL> UpdateDailyPnLAsync(DailyPnL dailyPnL)
    {
        var existing = await _context.DailyPnLs.FindAsync(dailyPnL.Date);
        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(dailyPnL);
        }
        else
        {
            _context.DailyPnLs.Add(dailyPnL);
        }
        
        await _context.SaveChangesAsync();
        return dailyPnL;
    }

    public async Task<List<DailyPnL>> GetPnLRangeAsync(DateOnly fromDate, DateOnly toDate)
    {
        return await _context.DailyPnLs
            .Where(p => p.Date >= fromDate && p.Date <= toDate)
            .OrderBy(p => p.Date)
            .ToListAsync();
    }

    // Analytics
    public async Task<decimal> GetCurrentExposureAsync(string tokenMint)
    {
        return await _context.Positions
            .Where(p => p.TokenMint == tokenMint && p.ClosedAt == null)
            .SumAsync(p => p.SizeSOL);
    }

    public async Task<decimal> GetTotalExposureAsync()
    {
        return await _context.Positions
            .Where(p => p.ClosedAt == null)
            .SumAsync(p => p.SizeSOL);
    }

    public async Task<decimal> GetDailyDrawdownAsync(DateOnly date)
    {
        var dailyPnL = await GetDailyPnLAsync(date);
        return dailyPnL?.MaxDrawdownPct ?? 0m;
    }

    public async Task<int> GetRecentLossStreakAsync(TradingSleeve sleeve)
    {
        var recentPositions = await _context.Positions
            .Where(p => p.Sleeve == sleeve && p.ClosedAt != null)
            .OrderByDescending(p => p.ClosedAt)
            .Take(10)
            .ToListAsync();

        int streak = 0;
        foreach (var position in recentPositions)
        {
            if (position.RealizedPnlSOL < 0)
                streak++;
            else
                break;
        }

        return streak;
    }
}