using System.Diagnostics.CodeAnalysis;
using Engine.Server.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Engine.Server.Persistence;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via the EF Core DbContext factory.")]
internal sealed class IncrementalEngineDbContext : DbContext
{
    public IncrementalEngineDbContext(DbContextOptions<IncrementalEngineDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<UniverseEntity> Universes => Set<UniverseEntity>();

    public DbSet<CharacterEntity> Characters => Set<CharacterEntity>();

    public DbSet<ModuleStateEntity> ModuleStates => Set<ModuleStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.Email).HasMaxLength(320).IsRequired();
            entity.Property(e => e.NormalizedEmail).HasMaxLength(320).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.NormalizedEmail).IsUnique();
            entity.HasMany(e => e.Universes)
                .WithOne(u => u.User!)
                .HasForeignKey(u => u.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UniverseEntity>(entity =>
        {
            entity.ToTable("universes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.UserId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.Name });
            entity.HasMany(e => e.Characters)
                .WithOne(c => c.Universe!)
                .HasForeignKey(c => c.UniverseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterEntity>(entity =>
        {
            entity.ToTable("characters");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(64);
            entity.Property(e => e.UniverseId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
            entity.Property(e => e.SpriteAssetId).HasMaxLength(256).IsRequired();
            entity.Property(e => e.EquipmentJson).HasColumnType("TEXT").IsRequired();
            entity.HasIndex(e => e.UniverseId);
        });

        modelBuilder.Entity<ModuleStateEntity>(entity =>
        {
            entity.ToTable("module_state");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ModuleId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.StateKey).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Payload).HasColumnType("BLOB").IsRequired();
            entity.HasIndex(e => new { e.ModuleId, e.StateKey }).IsUnique();
        });
    }
}
