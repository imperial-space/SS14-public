using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fugitives;

/// <summary>fugitive_capture/mouse_drop_receive(): 5 секунд на заталкивание беглеца.</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialFugitiveCaptureDoAfterEvent : SimpleDoAfterEvent;
