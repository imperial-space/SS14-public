using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Произнесение заклинания с поддержанием (каналом).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialSpellChannelDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
