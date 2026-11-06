using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Cult;

/// <summary>
/// Универсальные окна культа: замена tgui_input_list / show_radial_menu / tgui_input_text / tgui_alert из SS13.
/// Открываются на самом культисте.
/// </summary>
[Serializable, NetSerializable]
public enum CultMenuUiKey : byte
{
    Choice,
    Text,
}
