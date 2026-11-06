using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Инфузия ДНК в ДНК-инфузере.</summary>
[Serializable, Robust.Shared.Serialization.NetSerializable]
public sealed partial class DnaInfuseDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
