using Content.Shared.Item;
using Content.Shared.Popups;

namespace Content.Shared.Imperial.Xenomorph;

/// <summary>
/// facehugger/can_mob_unequip: присосавшегося живого лицехвата не может снять сам носитель
/// («Get help or wait for it to let go!»), а другие — могут.
/// </summary>
[RegisterComponent, Robust.Shared.GameStates.NetworkedComponent]
public sealed partial class XenoLatchedComponent : Component;
