using Content.Shared.Item;
using Robust.Server.Player;

namespace Content.Server.Imperial.Medieval.PickupLock;

public sealed class PickupLockSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PickupLockComponent, PickupAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<PickupLockComponent, GettingPickedUpAttemptEvent>(OnGettingPickedUpAttempt);
    }

    private void OnPickupAttempt(EntityUid uid, PickupLockComponent component, PickupAttemptEvent args)
    {
        CheckPickupLock(uid, component, args.User, args);
    }

    private void OnGettingPickedUpAttempt(EntityUid uid, PickupLockComponent component, GettingPickedUpAttemptEvent args)
    {
        CheckPickupLock(uid, component, args.User, args);
    }

    private void CheckPickupLock(EntityUid itemUid, PickupLockComponent component, EntityUid user, BasePickupAttemptEvent args)
    {
        if (component.AllowedUserIds.Count == 0)
            return;

        if (!_playerManager.TryGetSessionByEntity(user, out var session))
        {
            args.Cancel();
            return;
        }

        var userId = session.UserId.ToString();

        if (!component.AllowedUserIds.Contains(userId))
        {
            args.Cancel();
        }
    }
}

[RegisterComponent]
public sealed partial class PickupLockComponent : Component
{
    /// <summary>
    /// Список UUID игрунов, которым разрешено поднимать этот предмет
    /// </summary>
    [DataField("holder")]
    public HashSet<string> AllowedUserIds { get; set; } = new();
}
