using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Blade;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic.Paths.Blade;

public sealed class HereticBladePassiveSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private const string KnifeTag = "HereticBlade";
    private static readonly TimeSpan DefaultCooldown = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan Level3Cooldown = TimeSpan.FromSeconds(10);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticBladePassiveComponent, AttackedEvent>(OnAttacked);
    }

    public void ApplyPassiveLevel1(EntityUid uid)
    {
        EnsureComp<HereticBladePassiveComponent>(uid);
    }

    private void OnAttacked(EntityUid uid, HereticBladePassiveComponent comp, AttackedEvent args)
    {
        if (!TryComp<HereticComponent>(uid, out var heretic))
            return;

        if (heretic.CurrentPath != HereticPath.Blade)
            return;

        var now = _gameTiming.CurTime;
        var cooldown = heretic.PassiveLevel >= 3 ? Level3Cooldown : DefaultCooldown;

        if (comp.LastCounterTime is { } lastUsed && now < lastUsed + cooldown)
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

        comp.LastCounterTime = now;

        var damage = new DamageSpecifier();
        damage.DamageDict["Slash"] = FixedPoint2.New(20);
        _damageable.TryChangeDamage(args.User, damage, true);
    }
}
