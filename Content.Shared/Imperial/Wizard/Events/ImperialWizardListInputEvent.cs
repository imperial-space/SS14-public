using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Сервер → клиент: tgui_input_list — выбрать пункт из списка.</summary>
[Serializable, NetSerializable]
public sealed class ImperialWizardListInputEvent(int id, string title, string message, List<string> options) : EntityEventArgs
{
    public int Id = id;
    public string Title = title;
    public string Message = message;
    public List<string> Options = options;
}
