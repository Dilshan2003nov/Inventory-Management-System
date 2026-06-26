namespace Project_ABC.Models;

public class UserAccount
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class RawMaterial
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int StockQty { get; set; }
}

public class Recipe
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
}

public class RecipeIngredient
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public int MaterialId { get; set; }
    public RawMaterial? Material { get; set; }
    public int Quantity { get; set; }
}

public class DailyPlan
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public int TargetQty { get; set; }
}

public class DailyActual
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public int ActualQty { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
