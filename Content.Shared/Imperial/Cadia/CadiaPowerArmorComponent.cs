using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Силовой доспех: надевается только поверх чёрного панциря Астартес.</summary>
[RegisterComponent]
public sealed partial class CadiaPowerArmorComponent : Component
{
    /// <summary>Доспех сестёр битвы шьют на людей: панцирь ему не нужен.</summary>
    [DataField]
    public bool RequiresCarapace = true;
}
