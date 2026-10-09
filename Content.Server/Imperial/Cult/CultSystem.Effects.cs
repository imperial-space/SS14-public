using Content.Server.Imperial.Cult.Components;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Cult;

public sealed partial class CultSystem
{
    [Dependency] private readonly SharedPointLightSystem _light = default!;

    public Entity<ImperialCultRuleComponent>? GetRule() => _rule.TryGetRule(out var rule) ? rule : null;

    /// <summary>mob_light(range, power, color, duration).</summary>
    public void FlashLight(EntityUid uid, Color color, float range, float power, float seconds)
    {
        var light = Spawn("CultLightFlash", Transform(uid).Coordinates);
        _transform.SetParent(light, uid);
        var comp = _light.EnsureLight(light);
        _light.SetColor(light, color, comp);
        _light.SetRadius(light, range, comp);
        _light.SetEnergy(light, power, comp);
        _light.SetEnabled(light, true, comp);
        EnsureComp<Robust.Shared.Spawners.TimedDespawnComponent>(light).Lifetime = seconds;
    }

    /// <summary>do_sparks().</summary>
    public void Sparks(EntityCoordinates coords)
    {
        Spawn("EffectSparks", coords);
        _audio.PlayPvs(CultSounds.Sparks, coords, AudioParams.Default.WithVariation(0.05f));
    }

    /// <summary>adjust_health(-amount): лечение всех типов урона.</summary>
    public void HealAll(EntityUid uid, float amount)
    {
        if (amount <= 0 || !TryComp<DamageableComponent>(uid, out var damageable))
            return;
        _damageable.HealDistributed((uid, damageable), -FixedPoint2.New(amount));
    }
}
