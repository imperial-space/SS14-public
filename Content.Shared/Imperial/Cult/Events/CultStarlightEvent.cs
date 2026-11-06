using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult;

/// <summary>started_narsie_summon / failed_narsie_summon: звёздный свет тускнеет и краснеет.</summary>
[Serializable, NetSerializable]
public sealed class CultStarlightEvent(bool summoning) : EntityEventArgs
{
    public readonly bool Summoning = summoning;
}
