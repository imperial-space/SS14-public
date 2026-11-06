using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.MachineParts;

/// <summary>Быстрый заменитель деталей (/obj/item/storage/part_replacer).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialPartReplacerComponent : Component
{
    /// <summary>Блюспейс-версия: работает на расстоянии и с закрытой панелью.</summary>
    [DataField]
    public bool Bluespace;

    [DataField]
    public float PlayEffectChance = 0.01f;
}
