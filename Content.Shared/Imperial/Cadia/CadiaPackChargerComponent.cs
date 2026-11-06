using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Зарядная станция: пополняет вставленные обоймы, по выстрелу за шаг.</summary>
[RegisterComponent]
public sealed partial class CadiaPackChargerComponent : Component
{
    [DataField]
    public List<string> Slots = new() { "pack1", "pack2", "pack3", "pack4" };

    /// <summary>Сколько выстрелов в секунду на обойму.</summary>
    [DataField]
    public int ChargePerSecond = 2;

    [DataField]
    public SoundSpecifier DoneSound = new SoundPathSpecifier("/Audio/Imperial/Cadia/Weapons/charger_done.ogg");
}
