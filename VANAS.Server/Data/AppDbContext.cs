using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VANAS.Shared.Models;

namespace VANAS.Server.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Profile> Profiles => Set<Profile>();
        public DbSet<TimeLedgerEntry> TimeLedger => Set<TimeLedgerEntry>();
        public DbSet<Station> Stations => Set<Station>();
        public DbSet<GameSession> Sessions => Set<GameSession>();
        public DbSet<CatalogGame> Games => Set<CatalogGame>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<User>().HasIndex(u => u.Username).IsUnique();
            b.Entity<User>().HasOne(u => u.Profile).WithOne().HasForeignKey<Profile>(p => p.UserId);
            b.Entity<Profile>().HasKey(p => p.UserId);
            b.Entity<Profile>().Property(p => p.DisplayName).UseCollation("NOCASE");
            b.Entity<Profile>().HasIndex(p => p.DisplayName).IsUnique();
            b.Entity<Station>().HasIndex(s => s.Name).IsUnique();
            b.Entity<CatalogGame>().HasIndex(g => new { g.Launcher, g.ExternalId }).IsUnique();
            b.Entity<TimeLedgerEntry>().HasIndex(t => t.UserId);
            b.Entity<GameSession>().HasIndex(s => new { s.UserId, s.EndedAt });
            b.Entity<GameSession>().HasOne(s => s.Station).WithMany().HasForeignKey(s => s.StationId);
        }

        /// <summary>SQLite stores dates without a time zone; everything we store is UTC, so say so when reading it back.</summary>
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
            configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
        }

        private class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
        {
            public UtcDateTimeConverter() : base(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc)) { }
        }

        private class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
        {
            public NullableUtcDateTimeConverter() : base(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v) { }
        }
    }
}
