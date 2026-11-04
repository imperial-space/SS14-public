using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Выход из кровавой лужи (2 секунды).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialBloodCrawlExitDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
