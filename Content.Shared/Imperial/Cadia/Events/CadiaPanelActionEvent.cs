using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public sealed class CadiaPanelActionEvent(CadiaPanelAction action, string text, Guid? target) : EntityEventArgs
{
    public readonly CadiaPanelAction Action = action;
    public readonly string Text = text;
    public readonly Guid? Target = target;
}
