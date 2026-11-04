using Robust.Shared.Audio;

namespace Content.Server.Imperial.Lavaland.MiningShop;

/// <summary>
/// Браслеты Кхейрала (accessory/kheiral_cuffs): надетые вне станции дают GPS-сигнал владельца.
/// </summary>
[RegisterComponent]
public sealed partial class KheiralCuffsComponent : Component
{
    [DataField] public SoundSpecifier EquipSound = new SoundPathSpecifier("/Audio/Items/Handcuffs/cuff_end.ogg");

    [ViewVariables] public EntityUid? Wearer;
    [ViewVariables] public bool GpsEnabled;
    [ViewVariables] public TimeSpan NextCheck;
}
