using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Полевая станция лечения: несколько секунд на месте — раны стянуты.</summary>
[RegisterComponent]
public sealed partial class CadiaHealStationComponent : Component
{
    [DataField]
    public float Heal = 40f;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4);

    [DataField]
    public int Charges = 15;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Items/Medical/ointment_end.ogg");
}
