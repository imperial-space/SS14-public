using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.KeyRing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticMysticCardComponent : Component
{
    /// <summary>Поглощённые карты: имя карты → список тегов доступа.</summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, List<string>> AbsorbedCards = new();

    /// <summary>Первая дверь при создании портала (промежуточное состояние).</summary>
    [DataField]
    public EntityUid? PendingDoor;

    /// <summary>Первый созданный портал.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? PortalOne;

    /// <summary>Второй созданный портал.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? PortalTwo;

    /// <summary>Если true — поведение инвертировано: язычники идут к партнёру, еретики — рандомно.</summary>
    [DataField, AutoNetworkedField]
    public bool Inverted;
}
