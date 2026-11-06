using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

// ═══════════════════════ Медали и памятка ═══════════════════════

/// <summary>Сколько медалей ещё может вручить этот офицер.</summary>
[RegisterComponent]
public sealed partial class CadiaMedalGiverComponent : Component
{
    [DataField]
    public int Left = 3;

    [ViewVariables]
    public EntityUid? Action;
}
