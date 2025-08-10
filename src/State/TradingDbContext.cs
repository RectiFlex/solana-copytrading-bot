using Microsoft.EntityFrameworkCore;

namespace SolanaTradingBot.State;

public class TradingDbContext : DbContext
{
    public TradingDbContext(DbContextOptions<TradingDbContext> options) : base(options) { }

    public DbSet<Position> Positions { get; set; }
    public DbSet<Trade> Trades { get; set; }
    public DbSet<DailyPnL> DailyPnLs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Position
        modelBuilder.Entity<Position>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenMint).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SizeSOL).HasPrecision(18, 9);
            entity.Property(e => e.AvgEntry).HasPrecision(18, 9);
            entity.Property(e => e.HighWaterMark).HasPrecision(18, 9);
            entity.Property(e => e.RealizedPnlSOL).HasPrecision(18, 9);
            entity.Property(e => e.RealizedPnlUSD).HasPrecision(18, 2);
            
            entity.HasIndex(e => e.TokenMint);
            entity.HasIndex(e => e.OpenedAt);
            entity.HasIndex(e => new { e.Sleeve, e.ClosedAt });
        });

        // Configure Trade
        modelBuilder.Entity<Trade>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Qty).HasPrecision(18, 9);
            entity.Property(e => e.Price).HasPrecision(18, 9);
            entity.Property(e => e.Fees).HasPrecision(18, 9);
            entity.Property(e => e.TransactionId).HasMaxLength(100);
            
            entity.HasOne(e => e.Position)
                  .WithMany(e => e.Trades)
                  .HasForeignKey(e => e.PositionId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasIndex(e => e.PositionId);
            entity.HasIndex(e => e.Timestamp);
        });

        // Configure DailyPnL
        modelBuilder.Entity<DailyPnL>(entity =>
        {
            entity.HasKey(e => e.Date);
            entity.Property(e => e.TotalPnlSOL).HasPrecision(18, 9);
            entity.Property(e => e.TotalPnlUSD).HasPrecision(18, 2);
            entity.Property(e => e.ScalpsPnlSOL).HasPrecision(18, 9);
            entity.Property(e => e.MomentumPnlSOL).HasPrecision(18, 9);
            entity.Property(e => e.SwingPnlSOL).HasPrecision(18, 9);
            entity.Property(e => e.MaxDrawdownPct).HasPrecision(5, 2);
        });
    }
}