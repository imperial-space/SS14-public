using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

[Serializable, NetSerializable]
public sealed class ImperialSpellbookEntryState(string id, string name, int times, float? cooldown, bool requiresGarb)
{
    public string Id = id;

    /// <summary>Имя с приставкой уровня («Ускоренный Огненный шар»).</summary>
    public string Name = name;

    public int Times = times;
    public float? Cooldown = cooldown;
    public bool RequiresGarb = requiresGarb;
}
