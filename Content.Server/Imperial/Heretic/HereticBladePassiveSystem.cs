using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticBladePassiveSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem  _damageable = default!;
    [Dependency] private readonly SharedHandsSystem _hands      = default!;
    [Dependency] private readonly IGameTiming       _gameTiming = default!;
    [Dependency] private readonly TagSystem         _tag        = default!;

    private const string KnifeTag = "HereticBlade";
    private static readonly TimeSpan DefaultCooldown = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Level3Cooldown  = TimeSpan.FromSeconds(10);

    private readonly Dictionary<EntityUid, TimeSpan> _counterCooldowns = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticBladePassiveComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<HereticBladePassiveComponent, ComponentShutdown>(OnShutdown);
    }

    public void ApplyPassiveLevel1(EntityUid uid)
    {
        EnsureComp<HereticBladePassiveComponent>(uid);
    }

    private void OnShutdown(EntityUid uid, HereticBladePassiveComponent comp, ComponentShutdown args)
    {
        _counterCooldowns.Remove(uid);
    }

    private void OnAttacked(EntityUid uid, HereticBladePassiveComponent comp, AttackedEvent args)
    {
        if (!TryComp<HereticComponent>(uid, out var heretic))
            return;

        if (heretic.CurrentPath != HereticPath.Blade)
            return;

        var now = _gameTiming.CurTime;
        var cooldown = heretic.PassiveLevel >= 3 ? Level3Cooldown : DefaultCooldown;

        if (_counterCooldowns.TryGetValue(uid, out var lastUsed) && now < lastUsed + cooldown)
            return;

        var holdsKnife = false;
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (!_tag.HasTag(held, KnifeTag))
                continue;
            holdsKnife = true;
            break;
        }

        if (!holdsKnife)
            return;

        _counterCooldowns[uid] = now;

        var damage = new DamageSpecifier();
        damage.DamageDict["Slash"] = FixedPoint2.New(20);
        _damageable.TryChangeDamage(args.User, damage, true);
    }
}
