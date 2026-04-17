using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderRoyalComponent : Component
{
	[DataField]
	public bool StompEnabled = true;

	[DataField]
	public EntProtoId StompAction = "ActionTerrorSpiderRoyalStomp";

	[DataField]
	public EntityUid? StompActionEntity;

	[DataField]
	public float StompRadius = 5f;

	[DataField]
	public float StompDamage = 20f;

	[DataField]
	public string StompDamageType = "Blunt";

	[DataField]
	public float StompSlowDuration = 10f;

	[DataField]
	public float StompSlowMultiplier = 0.5f;

	[DataField]
	public EntProtoId StompSlowStatusEffect = "TerrorSpiderRoyalStompSlowStatusEffect";

	[DataField]
	public SoundSpecifier? StompSound = new SoundPathSpecifier("/Audio/Imperial/TerrorSpider/slam.ogg");
}
