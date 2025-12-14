using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.StatusEffect;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class StatusEffect : BossAttackAction
{
    [DataField(required: true)]
    public string Key = default!;

    [DataField]
    public string Component = String.Empty;

    [DataField]
    public float Time = 2.0f;

    [DataField]
    public bool Refresh = true;

    [DataField]
    public StatusEffectMetabolismType Type = StatusEffectMetabolismType.Add;

    public override void Execute(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
    {
        var statusSys = entMan.EntitySysManager.GetEntitySystem<StatusEffectsSystem>();

        var time = Time;

        foreach (var target in targets)
        {
            if (Type == StatusEffectMetabolismType.Add && Component != String.Empty)
            {
                statusSys.TryAddStatusEffect(target, Key, TimeSpan.FromSeconds(time), Refresh, Component);
            }
            else if (Type == StatusEffectMetabolismType.Remove)
            {
                statusSys.TryRemoveTime(target, Key, TimeSpan.FromSeconds(time));
            }
            else if (Type == StatusEffectMetabolismType.Set)
            {
                statusSys.TrySetTime(target, Key, TimeSpan.FromSeconds(time));
            }
        }

    }
}
