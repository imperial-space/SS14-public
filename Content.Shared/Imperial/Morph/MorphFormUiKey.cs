using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Morph;

// ─── Окно выбора облика (tgui_input_list) ───────────────────────────────

[Serializable, NetSerializable]
public enum MorphFormUiKey : byte
{
    Key,
}
