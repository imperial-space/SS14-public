using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>buy_word: «Выучить», «Призвать», «Произнести».</summary>
[Serializable, NetSerializable]
public enum ImperialSpellbookBuyWord : byte
{
    Learn,
    Summon,
    Cast,
    Accept,
}
