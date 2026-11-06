using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Откуда мутация в окне консоли (mutation.Source в tgui SS13).</summary>
[Serializable, NetSerializable]
public enum DnaMutationSource : byte
{
    Occupant,
    Console,
    Disk,
    Injector,
}
