using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Xenomorph;

[Serializable, NetSerializable]
public enum XenoVisuals : byte
{
    /// <summary>Личинка: стадия роста 0..2.</summary>
    LarvaStage,

    /// <summary>Личинка прячется под столами.</summary>
    Hidden,

    /// <summary>Яйцо: XenoEggState.</summary>
    EggState,

    /// <summary>Лицехват: XenoFacehuggerState.</summary>
    FacehuggerState,
}
