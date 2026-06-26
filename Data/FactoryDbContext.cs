using Microsoft.EntityFrameworkCore;
using Project_ABC.Models;

namespace Project_ABC.Data;

public class FactoryDbContext : DbContext
{
    public FactoryDbContext(DbContextOptions<FactoryDbContext> options) : base(options) { }

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<RawMaterial> RawMaterials => Set<RawMaterial>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<DailyPlan> DailyPlans => Set<DailyPlan>();
    public DbSet<DailyActual> DailyActuals => Set<DailyActual>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<RawMaterial>().HasIndex(r => r.Name).IsUnique();
        modelBuilder.Entity<Recipe>().HasIndex(r => r.Name).IsUnique();

        modelBuilder.Entity<RecipeIngredient>()
            .HasOne(ri => ri.Recipe)
            .WithMany(r => r.Ingredients)
            .HasForeignKey(ri => ri.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RecipeIngredient>()
            .HasOne(ri => ri.Material)
            .WithMany()
            .HasForeignKey(ri => ri.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DailyPlan>()
            .HasOne(dp => dp.Recipe)
            .WithMany()
            .HasForeignKey(dp => dp.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DailyActual>()
            .HasOne(da => da.Recipe)
            .WithMany()
            .HasForeignKey(da => da.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
