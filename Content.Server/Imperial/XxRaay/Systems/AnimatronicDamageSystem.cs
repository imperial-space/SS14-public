using Content.Shared.Imperial.XxRaay.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.XxRaay.Systems;

/// <summary>
/// Система для обработки урона при контакте аниматроника с другими сущностями.
/// </summary>
public sealed class AnimatronicDamageSystem : EntitySystem
{
	[Dependency] private readonly IGameTiming _timing = default!;
	[Dependency] private readonly DamageableSystem _damageable = default!;
	[Dependency] private readonly MobStateSystem _mobState = default!;
	[Dependency] private readonly IPrototypeManager _prototype = default!;

	public override void Initialize()
	{
		base.Initialize();
		SubscribeLocalEvent<AnimatronicDamageComponent, StartCollideEvent>(OnAnimatronicCollide);
		SubscribeLocalEvent<AnimatronicDamageComponent, EndCollideEvent>(OnAnimatronicCollideEnd);
	}

	private void OnAnimatronicCollide(Entity<AnimatronicDamageComponent> ent, ref StartCollideEvent args)
	{
		var otherUid = args.OtherEntity;

		if (!HasComp<MobStateComponent>(otherUid) || !HasComp<DamageableComponent>(otherUid))
			return;

		if (_mobState.IsDead(otherUid))
			return;

		var damageComp = ent.Comp;
		if (damageComp.LastContactDamage.TryGetValue(otherUid, out var lastDamage) &&
		    (_timing.CurTime - lastDamage) < damageComp.ContactDamageCooldown)
			return;

		if (!_prototype.TryIndex<DamageGroupPrototype>("Brute", out var bruteGroup))
		{
			Log.Error($"AnimatronicDamageSystem: Failed to find Brute damage group prototype");
			return;
		}

		var damage = new DamageSpecifier(bruteGroup, FixedPoint2.New(damageComp.ContactDamage));
		_damageable.TryChangeDamage(otherUid, damage, ignoreResistances: true, origin: ent.Owner);

		damageComp.LastContactDamage[otherUid] = _timing.CurTime;
	}

	private void OnAnimatronicCollideEnd(Entity<AnimatronicDamageComponent> ent, ref EndCollideEvent args)
	{
		var otherUid = args.OtherEntity;
		ent.Comp.LastContactDamage.Remove(otherUid);
	}
}

