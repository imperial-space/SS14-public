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

[Serializable, NetSerializable]
public sealed class CultMenuOption(string id, string label, SpriteSpecifier? icon = null, string? tooltip = null, Color? color = null)
{
    public readonly string Id = id;
    public readonly string Label = label;
    public readonly SpriteSpecifier? Icon = icon;
    public readonly string? Tooltip = tooltip;
    public readonly Color? Color = color;
}

[Serializable, NetSerializable]
public sealed class CultChoiceState(string title, List<CultMenuOption> options, bool radial) : BoundUserInterfaceState
{
    public readonly string Title = title;
    public readonly List<CultMenuOption> Options = options;

    /// <summary>Радиальное меню (иначе список кнопок).</summary>
    public readonly bool Radial = radial;
}

[Serializable, NetSerializable]
public sealed class CultChoiceMessage(string id) : BoundUserInterfaceMessage
{
    public readonly string Id = id;
}

[Serializable, NetSerializable]
public sealed class CultTextState(string title, string prompt, int maxLength) : BoundUserInterfaceState
{
    public readonly string Title = title;
    public readonly string Prompt = prompt;
    public readonly int MaxLength = maxLength;
}

[Serializable, NetSerializable]
public sealed class CultTextMessage(string text) : BoundUserInterfaceMessage
{
    public readonly string Text = text;
}
