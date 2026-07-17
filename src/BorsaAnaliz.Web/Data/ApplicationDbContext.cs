using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using BorsaAnaliz.Web.Models;

namespace BorsaAnaliz.Web.Data;

public class ApplicationDbContext : IdentityDbContext, IDataProtectionKeyContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Portfolio> Portfolios => Set<Portfolio>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Portfolio>(entity =>
        {
            entity.Property(portfolio => portfolio.UserId).HasMaxLength(450).IsRequired();
            entity.Property(portfolio => portfolio.Name).HasMaxLength(100).IsRequired();
            entity.Property(portfolio => portfolio.InitialCash).HasPrecision(18, 4);
            entity.HasIndex(portfolio => portfolio.UserId);
        });

        builder.Entity<Transaction>(entity =>
        {
            entity.Property(transaction => transaction.Symbol).HasMaxLength(32).IsRequired();
            entity.Property(transaction => transaction.Quantity).HasPrecision(18, 4);
            entity.Property(transaction => transaction.Price).HasPrecision(18, 4);
            entity.HasIndex(transaction => new { transaction.PortfolioId, transaction.ExecutedAt });
            entity.HasOne(transaction => transaction.Portfolio)
                .WithMany(portfolio => portfolio.Transactions)
                .HasForeignKey(transaction => transaction.PortfolioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WatchlistItem>(entity =>
        {
            entity.Property(item => item.UserId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.Symbol).HasMaxLength(32).IsRequired();
            entity.HasIndex(item => new { item.UserId, item.Symbol }).IsUnique();
            entity.HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
