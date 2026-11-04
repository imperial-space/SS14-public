using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Одно из трёх произнесений руны.</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialGrandRuneInvokeDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;
