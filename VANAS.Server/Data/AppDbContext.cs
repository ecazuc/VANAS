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
        public DbSet<Article> Articles => Set<Article>();
        public DbSet<GameEvent> Events => Set<GameEvent>();
        public DbSet<Offer> Offers => Set<Offer>();
        public DbSet<Sale> Sales => Set<Sale>();
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<PassUsage> PassUsage => Set<PassUsage>();
        public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<User>().HasIndex(u => u.Username).IsUnique();
            b.Entity<User>().HasOne(u => u.Profile).WithOne().HasForeignKey<Profile>(p => p.UserId);
            b.Entity<Profile>().HasKey(p => p.UserId);
            b.Entity<Profile>().Property(p => p.DisplayName).UseCollation("NOCASE");
            b.Entity<Profile>().HasIndex(p => p.DisplayName).IsUnique();
            b.Entity<Station>().HasIndex(s => s.Name).IsUnique();
            b.Entity<Article>().HasIndex(a => a.Slug).IsUnique();
            b.Entity<Article>().HasIndex(a => new { a.IsPublished, a.PublishedAt });
            b.Entity<GameEvent>().HasIndex(e => e.StartsAt);
            b.Entity<Sale>().HasIndex(s => s.CreatedAt);
            b.Entity<Subscription>().HasIndex(s => new { s.UserId, s.EndsAt });
            b.Entity<PassUsage>().HasIndex(u => new { u.SubscriptionId, u.At });
            b.Entity<EventRegistration>().HasIndex(r => r.TicketCode).IsUnique();
            b.Entity<EventRegistration>().HasIndex(r => new { r.EventId, r.UserId }).IsUnique();
            b.Entity<EventRegistration>().HasOne<GameEvent>().WithMany().HasForeignKey(r => r.EventId).OnDelete(DeleteBehavior.Cascade);
            b.Entity<EventRegistration>().HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
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
