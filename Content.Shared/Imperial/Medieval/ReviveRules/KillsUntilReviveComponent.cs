using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Revive;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class KillsUntilReviveComponent : Component
{
    // Необходимое количество убийств для возрождения
    // todo: сделать изменение через CVar
    [DataField, AutoNetworkedField]
    public int RequiredKills = 12;

    // Текущее количество убийств игрока
    [DataField, AutoNetworkedField]
    public int CurrentKills = 0;
    // Алёрт (иконки в правой части GUI), отвечающий за отображение
    [DataField, AutoNetworkedField]
    public ProtoId<AlertPrototype> KillsAlert = "KilledSpirits";

    [DataField, AutoNetworkedField]
    public EntProtoId EffectProto = "BlackHoleSpellCastEffectBeginner";
}
