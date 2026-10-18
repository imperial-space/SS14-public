using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Lavaland.Legion;

/// <summary>
/// Посох бурь (obj/item/storm_staff из SS13): в руке рассеивает бурю, по клику
/// вызывает молнию. Три заряда, каждый восстанавливается 15 секунд.
/// </summary>
[RegisterComponent]
public sealed partial class StormStaffComponent : Component
{
    [DataField] public int MaxCharges = 3;
    [DataField] public float ChargeTime = 15f;
    [DataField] public float DispelTime = 3f;
    [DataField] public float BoltDelay = 1.5f;
    [DataField] public float BoltDamage = 15f;

    [DataField] public SoundSpecifier AimSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_lightningshock.ogg");
    [DataField] public SoundSpecifier BoltSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_lightningbolt.ogg");
    [DataField] public SoundSpecifier RechargeSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_charge.ogg");
    [DataField] public SoundSpecifier DispelSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_staff_change.ogg");

    [DataField] public EntProtoId Telegraph = "ImperialStormStaffTelegraph";
    [DataField] public EntProtoId Thunderbolt = "ImperialStormStaffThunderbolt";
    [DataField] public EntProtoId Electricity = "ImperialStormStaffElectricity";

    [ViewVariables] public int Charges = 3;
    [ViewVariables] public HashSet<(EntityUid, Vector2i)> Targeted = new();
}
