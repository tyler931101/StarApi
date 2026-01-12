using Microsoft.EntityFrameworkCore;
using StarApi.Models;

namespace StarApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure ChatMessage entity
            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.HasIndex(m => m.SenderId);
                entity.HasIndex(m => m.ReceiverId);
                entity.HasIndex(m => m.CreatedAt);

                entity.HasOne(m => m.Sender)
                    .WithMany()
                    .HasForeignKey(m => m.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Receiver)
                    .WithMany()
                    .HasForeignKey(m => m.ReceiverId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.VerificationToken).IsUnique();
                entity.HasIndex(u => u.RefreshToken).IsUnique();

                entity.Property(u => u.Email).IsRequired().HasMaxLength(100);
                entity.Property(u => u.Username).IsRequired().HasMaxLength(50);
                entity.Property(u => u.PasswordHash).IsRequired();
                entity.Property(u => u.Role).IsRequired().HasMaxLength(20);

                // Configure new properties
                entity.Property(u => u.AvatarData)
                    .HasColumnType("BLOB"); // Explicitly set as BLOB for SQLite

                entity.Property(u => u.AvatarMimeType)
                    .HasMaxLength(50);

                entity.Property(u => u.AvatarUrl)
                    .HasMaxLength(500);

                entity.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                // Add default value for new columns in SQLite
                entity.Property(u => u.AvatarUpdatedAt)
                    .HasDefaultValue(null);
            });

            modelBuilder.Entity<Ticket>(entity =>
            {
                entity.HasIndex(t => t.Status);
                entity.HasIndex(t => t.Priority);
                entity.HasIndex(t => t.AssignedTo);
                entity.Property(t => t.Title).IsRequired().HasMaxLength(150);
                entity.Property(t => t.Description).HasMaxLength(4000);
                entity.Property(t => t.Status).IsRequired().HasMaxLength(20);
                entity.Property(t => t.Priority).IsRequired().HasMaxLength(20);
                entity.Property(t => t.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                entity.Property<Guid>("CreatedByUserId");

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(t => t.AssignedTo)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(t => t.CreatedByUser)
                    .WithMany()
                    .HasForeignKey("CreatedByUserId")
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
