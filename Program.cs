using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Project_ABC.Data;
using Project_ABC.Models;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<FactoryDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("FactoryDb") ?? "Data Source=Database/factory.db"));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login.html";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FactoryDbContext>();
    db.Database.EnsureCreated();

    if (!db.UserAccounts.Any())
    {
        db.UserAccounts.AddRange(
            new UserAccount { Username = "admin", Password = "admin123", Role = "Admin" },
            new UserAccount { Username = "supervisor", Password = "super123", Role = "Supervisor" },
            new UserAccount { Username = "manager", Password = "mgr123", Role = "Manager" }
        );

        var materials = new List<RawMaterial>
        {
            new RawMaterial { Name = "Electric Motor", StockQty = 150 },
            new RawMaterial { Name = "Steel Tub", StockQty = 85 },
            new RawMaterial { Name = "Control PCB Array", StockQty = 200 },
            new RawMaterial { Name = "Compressor Unit", StockQty = 40 }
        };
        db.RawMaterials.AddRange(materials);
        db.SaveChanges();

        db.Recipes.AddRange(
            new Recipe
            {
                Name = "Eco Washing Machine",
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient { MaterialId = materials[0].Id, Quantity = 1 },
                    new RecipeIngredient { MaterialId = materials[1].Id, Quantity = 1 },
                    new RecipeIngredient { MaterialId = materials[2].Id, Quantity = 1 }
                }
            },
            new Recipe
            {
                Name = "Industrial Smart Refrigerator",
                Ingredients = new List<RecipeIngredient>
                {
                    new RecipeIngredient { MaterialId = materials[3].Id, Quantity = 1 },
                    new RecipeIngredient { MaterialId = materials[2].Id, Quantity = 2 }
                }
            }
        );

        db.SaveChanges();
    }
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/account/login", async (HttpContext context, FactoryDbContext db) =>
{
    var form = await context.Request.ReadFormAsync();
    string username = form["username"].ToString().Trim();
    string password = form["password"].ToString();

    var user = await db.UserAccounts.FirstOrDefaultAsync(u => u.Username == username && u.Password == password);

    if (user is null)
    {
        context.Response.Redirect("/login.html?error=1");
        return;
    }

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(ClaimTypes.Role, user.Role)
    };

    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
    context.Response.Redirect("/dashboard.html");
});

app.MapGet("/api/account/user-info", (HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var username = context.User.Identity.Name;
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value;

        return Results.Ok(new { IsAuthenticated = true, Username = username, Role = role });
    }

    return Results.Json(new { IsAuthenticated = false }, statusCode: 401);
}).RequireAuthorization();

app.MapGet("/api/factory/materials", async (FactoryDbContext db) =>
{
    var materials = await db.RawMaterials.OrderBy(m => m.Name).ToListAsync();
    return Results.Ok(materials.Select(m => new { id = m.Id, name = m.Name, stockQty = m.StockQty }));
}).RequireAuthorization();

app.MapPost("/api/factory/materials", async (HttpContext context, FactoryDbContext db) =>
{
    var form = await context.Request.ReadFormAsync();
    var name = form["name"].ToString().Trim();
    var qty = int.TryParse(form["qty"].ToString(), out var parsed) ? parsed : 0;

    if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest();

    var exists = await db.RawMaterials.AnyAsync(m => m.Name == name);
    if (exists) return Results.Conflict();

    var material = new RawMaterial { Name = name, StockQty = qty };
    db.RawMaterials.Add(material);
    await db.SaveChangesAsync();
    return Results.Ok(new { id = material.Id, name = material.Name, stockQty = material.StockQty });
}).RequireAuthorization();

app.MapPut("/api/factory/materials/{id:int}", async (int id, HttpContext context, FactoryDbContext db) =>
{
    var form = await context.Request.ReadFormAsync();
    var qty = int.TryParse(form["qty"].ToString(), out var parsed) ? parsed : 0;

    var material = await db.RawMaterials.FindAsync(id);
    if (material is null) return Results.NotFound();

    material.StockQty = qty;
    await db.SaveChangesAsync();
    return Results.Ok(new { id = material.Id, name = material.Name, stockQty = material.StockQty });
}).RequireAuthorization();

app.MapDelete("/api/factory/materials/{id:int}", async (int id, FactoryDbContext db) =>
{
    var material = await db.RawMaterials.FindAsync(id);
    if (material is null) return Results.NotFound();

    db.RawMaterials.Remove(material);
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

app.MapGet("/api/factory/recipes", async (FactoryDbContext db) =>
{
    var recipes = await db.Recipes
        .Include(r => r.Ingredients)
        .ThenInclude(i => i.Material)
        .OrderBy(r => r.Name)
        .ToListAsync();

    return Results.Ok(recipes.Select(r => new {
        id = r.Id,
        name = r.Name,
        ingredients = r.Ingredients.Select(i => new { name = i.Material!.Name, qty = i.Quantity })
    }));
}).RequireAuthorization();

app.MapPost("/api/factory/recipes", async (HttpContext context, FactoryDbContext db) =>
{
    var form = await context.Request.ReadFormAsync();
    var name = form["name"].ToString().Trim();
    var ingredientEntries = form["ingredients"].ToString().Split('|', StringSplitOptions.RemoveEmptyEntries);

    if (string.IsNullOrWhiteSpace(name) || ingredientEntries.Length == 0) return Results.BadRequest();

    var recipe = new Recipe { Name = name };
    foreach (var entry in ingredientEntries)
    {
        var parts = entry.Split(':');
        if (parts.Length != 2) continue;

        var materialId = int.TryParse(parts[0], out var parsedId) ? parsedId : 0;
        var qty = int.TryParse(parts[1], out var parsedQty) ? parsedQty : 1;

        if (materialId <= 0) continue;

        var material = await db.RawMaterials.FindAsync(materialId);
        if (material is null) continue;

        recipe.Ingredients.Add(new RecipeIngredient { MaterialId = material.Id, Quantity = qty });
    }

    if (recipe.Ingredients.Count == 0) return Results.BadRequest();

    db.Recipes.Add(recipe);
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

app.MapDelete("/api/factory/recipes/{id:int}", async (int id, FactoryDbContext db) =>
{
    var recipe = await db.Recipes.FindAsync(id);
    if (recipe is null) return Results.NotFound();

    db.Recipes.Remove(recipe);
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

app.MapGet("/api/factory/plans", async (FactoryDbContext db) =>
{
    var plans = await db.DailyPlans.Include(p => p.Recipe).OrderBy(p => p.Id).ToListAsync();
    return Results.Ok(plans.Select(p => new { id = p.Id, recipeId = p.RecipeId, product = p.Recipe!.Name, qty = p.TargetQty }));
}).RequireAuthorization();

app.MapPost("/api/factory/plans", async (HttpContext context, FactoryDbContext db) =>
{
    var form = await context.Request.ReadFormAsync();
    var recipeId = int.TryParse(form["recipeId"].ToString(), out var parsedId) ? parsedId : 0;
    var qty = int.TryParse(form["qty"].ToString(), out var parsedQty) ? parsedQty : 0;

    var recipe = await db.Recipes.FindAsync(recipeId);
    if (recipe is null || qty <= 0) return Results.BadRequest();

    db.DailyPlans.Add(new DailyPlan { RecipeId = recipeId, TargetQty = qty });
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

app.MapPost("/api/factory/summary", async (HttpContext context, FactoryDbContext db) =>
{
    var form = await context.Request.ReadFormAsync();
    var recipeId = int.TryParse(form["recipeId"].ToString(), out var parsedId) ? parsedId : 0;
    var qty = int.TryParse(form["qty"].ToString(), out var parsedQty) ? parsedQty : 0;

    var recipe = await db.Recipes.Include(r => r.Ingredients).ThenInclude(i => i.Material).FirstOrDefaultAsync(r => r.Id == recipeId);
    if (recipe is null || qty <= 0) return Results.BadRequest();

    foreach (var ingredient in recipe.Ingredients)
    {
        var material = await db.RawMaterials.FindAsync(ingredient.MaterialId);
        if (material is null) continue;

        if (material.StockQty < ingredient.Quantity * qty)
        {
            return Results.BadRequest(new { message = "Insufficient stock" });
        }
    }

    foreach (var ingredient in recipe.Ingredients)
    {
        var material = await db.RawMaterials.FindAsync(ingredient.MaterialId);
        if (material is null) continue;
        material.StockQty -= ingredient.Quantity * qty;
    }

    db.DailyActuals.Add(new DailyActual { RecipeId = recipeId, ActualQty = qty });
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

app.MapGet("/api/factory/metrics", async (FactoryDbContext db) =>
{
    var planned = await db.DailyPlans.SumAsync(p => p.TargetQty);
    var actual = await db.DailyActuals.SumAsync(a => a.ActualQty);
    return Results.Ok(new { planned, actual, efficiency = planned > 0 ? Math.Round((double)actual / planned * 100, 0) : 0 });
}).RequireAuthorization();

app.MapPost("/account/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    context.Response.Redirect("/login.html");
});

app.Run();