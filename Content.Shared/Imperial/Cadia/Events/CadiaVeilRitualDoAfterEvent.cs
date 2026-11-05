using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Ритуал над завесой: колдуны Хаоса раскачивают её, жрецы Империума укрепляют у пилонов.</summary>
[Robust.Shared.Serialization.NetSerializable, Serializable]
public sealed partial class CadiaVeilRitualDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
