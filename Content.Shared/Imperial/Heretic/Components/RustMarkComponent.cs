using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Components;

/// <summary>
/// Mark of Corruption: applied by a Mansus Grasp hit when Mark of Rust is known. A follow-up hit with
/// the Rusty Blade on a marked target consumes the mark and staggers/stuns it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RustMarkComponent : Component { }
