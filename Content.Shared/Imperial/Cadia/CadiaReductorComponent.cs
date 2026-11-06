using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Редуктор апотекария: извлекает прогеноид из павшего десантника.</summary>
[RegisterComponent]
public sealed partial class CadiaReductorComponent : Component
{
    [DataField]
    public TimeSpan ReductorDelay = TimeSpan.FromSeconds(6);
}
