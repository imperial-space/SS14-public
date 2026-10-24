using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>/obj/effect/particle_effect/fluid/smoke/bad: душащий дым.</summary>
[RegisterComponent]
public sealed partial class ImperialBadSmokeComponent : Component;

/// <summary>smoke/bad/smoke_mob(): без дыхательной маски — кашель, вещи из рук, немного удушья.</summary>
public sealed class ImperialWizardSmokeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedInternalsSystem _internals = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;

    private static readonly TimeSpan SmokeDelay = TimeSpan.FromSeconds(2);
    private readonly Dictionary<EntityUid, TimeSpan> _nextSmoke = new();
    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + TimeSpan.FromSeconds(0.5);
        var smoked = new HashSet<EntityUid>();
        var query = EntityQueryEnumerator<ImperialBadSmokeComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            foreach (var mob in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(xform.Coordinates, 0.5f))
            {
                smoked.Add(mob.Owner);
            }
        }

        foreach (var mob in smoked)
        {
            if (_nextSmoke.TryGetValue(mob, out var next) && now < next || !_mobs.IsAlive(mob) || _internals.AreInternalsWorking(mob))
                continue;

            _nextSmoke[mob] = now + SmokeDelay;
            foreach (var held in _hands.EnumerateHeld(mob).ToList())
            {
                _hands.TryDrop(mob, held, checkActionBlocker: false);
            }

            _damage.TryChangeDamage(mob, new DamageSpecifier { DamageDict = { ["Asphyxiation"] = 1 } }, true);
            _chat.TryEmoteWithChat(mob, "Cough");
        }

        if (_nextSmoke.Count > 256)
            _nextSmoke.Clear();
    }
}
