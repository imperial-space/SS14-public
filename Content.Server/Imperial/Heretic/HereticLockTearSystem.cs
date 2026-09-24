using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Разрыв Замка: мёртвые игроки могут взять роль духа и войти в мир
/// в облике случайного еретического монстра. Разрыв уничтожается при гибели мастера.
/// </summary>
public sealed class HereticLockTearSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly EntProtoId[] MonsterPool =
    [
        "MobHereticRustWalker",
        "MobHereticFireShark",
        "MobHereticRawProphet",
    ];

    public override void Initialize()
    {
        base.Initialize();

        // Перехватываем ДО GhostRoleSystem, чтобы выставить случайный прототип моба
        SubscribeLocalEvent<HereticLockTearComponent, TakeGhostRoleEvent>(OnTakeRole,
            before: [typeof(GhostRoleSystem)]);
    }

    private void OnTakeRole(EntityUid uid, HereticLockTearComponent comp, ref TakeGhostRoleEvent args)
    {
        if (TryComp<GhostRoleMobSpawnerComponent>(uid, out var spawner))
            spawner.Prototype = _random.Pick(MonsterPool);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticLockTearComponent>();
        while (query.MoveNext(out var uid, out var tear))
        {
            if (!Exists(tear.Master) || _mobs.IsDead(tear.Master))
                QueueDel(uid);
        }
    }
}
