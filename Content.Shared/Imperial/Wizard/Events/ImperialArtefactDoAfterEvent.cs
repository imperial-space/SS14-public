using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Призыв, отталкивание или подрыв рунического торгомата скипетром.</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialArtefactDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
