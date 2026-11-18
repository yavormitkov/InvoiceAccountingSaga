using Financial.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Financial.Api.Data;

public class FinancialDbContext : DbContext
{
    public FinancialDbContext(DbContextOptions<FinancialDbContext> options) : base(options)
    {
    }

    public DbSet<UserBalance> UserBalances { get; set; } = null!;
    public DbSet<Bailiff> Bailiffs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserBalance>(eb =>
        {
            eb.ToTable("UserBalances");
            eb.HasKey(e => e.Id);
            eb.Property(e => e.UserIdentifier).IsRequired();
            eb.HasIndex(e => e.UserIdentifier).IsUnique();
            eb.Property(e => e.Balance).HasPrecision(18, 4);
            eb.Property(e => e.RowVersion).IsRowVersion();

            eb.HasOne<Bailiff>()
              .WithOne()
              .HasForeignKey<UserBalance>(e => e.UserIdentifier)
              .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Bailiff>(eb =>
        {
            eb.ToTable("Bailiffs");
            eb.HasKey(e => e.Id);
            eb.Property(e => e.LicenseNumber).IsRequired().HasMaxLength(50);
            eb.HasIndex(e => e.LicenseNumber).IsUnique();
            eb.Property(e => e.Name).IsRequired().HasMaxLength(100);
        });
    }
}
