using Microsoft.EntityFrameworkCore;
using SakaiBot.Models;

namespace SakaiBot.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Birthday> Birthdays => Set<Birthday>();
        public DbSet<Punishment> Punishments => Set<Punishment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Birthday>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.GuildId).IsRequired();
                entity.Property(e => e.BirthDate).HasColumnType("date").IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.HasIndex(e => new { e.GuildId, e.UserId }).IsUnique();
            });

            modelBuilder.Entity<Punishment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.GuildId).IsRequired();
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.ModeratorId).IsRequired();
                entity.Property(e => e.Action).IsRequired();
                entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.CaseId).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.CaseId).IsUnique();
            });
        }
    }
}
