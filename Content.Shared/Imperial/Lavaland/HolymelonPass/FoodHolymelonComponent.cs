namespace Content.Shared.Imperial.Lavaland;

/// <summary>
/// Маркер на сущностях хелибы: позволяет перехватить IngestedEvent без конфликта с IngestionSystem.
/// </summary>
[RegisterComponent]
public sealed partial class FoodHolymelonComponent : Component;
