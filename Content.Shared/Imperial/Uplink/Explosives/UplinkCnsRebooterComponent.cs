using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Explosives;

/// <summary>Имплант-перезагрузчик ЦНС: через секунду после оглушения снимает его и даёт 6 секунд иммунитета.</summary>
[RegisterComponent]
public sealed partial class UplinkCnsRebooterComponent : Component
{
    [ViewVariables]
    public TimeSpan ReadyAt;

    [ViewVariables]
    public TimeSpan? ClearAt;

    [ViewVariables]
    public TimeSpan ImmuneUntil;

    [DataField]
    public TimeSpan ImmuneDelay = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan ReadyDelay = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan ClearDelay = TimeSpan.FromSeconds(1);
}
