namespace Content.Shared.Imperial.Research;

/// <summary>
/// Повреждённый модуль ядра TG: при загрузке даёт 1 случайный ионный закон и ещё по одному с шансом 75%.
/// Законы создаются при появлении платы и заново после каждого извлечения из консоли загрузки.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialDamagedLawBoardComponent : Component
{
    [DataField]
    public float ExtraLawChance = 0.75f;

    [DataField]
    public int MaxLaws = 8;
}
