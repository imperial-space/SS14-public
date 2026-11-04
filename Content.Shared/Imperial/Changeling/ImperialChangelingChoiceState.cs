using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>tgui_alert / tgui_input_list: вопрос с вариантами ответа.</summary>
[Serializable, NetSerializable]
public sealed class ImperialChangelingChoiceState(string title, string message, List<string> options) : BoundUserInterfaceState
{
    public readonly string Title = title;
    public readonly string Message = message;
    public readonly List<string> Options = options;
}
