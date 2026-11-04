using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Клиент → сервер: выбранный пункт (null — отмена).</summary>
[Serializable, NetSerializable]
public sealed class ImperialWizardListChoiceEvent(int id, string? choice) : EntityEventArgs
{
    public int Id = id;
    public string? Choice = choice;
}
