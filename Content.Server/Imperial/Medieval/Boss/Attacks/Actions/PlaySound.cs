using Robust.Server.Audio;
using Robust.Shared.Audio;

namespace Content.Server.Imperial.Medieval.Boss;

public sealed partial class PlaySound : BossAttackAction
{
    [DataField(required: true)]
    public SoundSpecifier Sound;

    [DataField]
    public BossSoundType SoundType = BossSoundType.Player;

    public override void Execute(EntityUid boss, IEnumerable<EntityUid> targets, IEntityManager entMan)
    {
        var audio = entMan.System<AudioSystem>();

        switch (SoundType)
        {
            case BossSoundType.Player:
                foreach (var target in targets)
                    audio.PlayGlobal(Sound, target);
                break;
            case BossSoundType.BossPvs:
                var bossXform = entMan.GetComponent<TransformComponent>(boss);
                audio.PlayPvs(Sound, bossXform.Coordinates);
                break;
            case BossSoundType.Pvs:
                foreach (var target in targets)
                    audio.PlayPvs(Sound, target);
                break;
            case BossSoundType.Position:
                foreach (var target in targets)
                {
                    var positionXform = entMan.GetComponent<TransformComponent>(target);
                    audio.PlayPvs(Sound, positionXform.Coordinates);
                }
                break;
            default:
                break;
        }
        foreach (var target in targets)
        {
            var xform = entMan.GetComponent<TransformComponent>(target);
            audio.PlayPvs(Sound, xform.Coordinates);
        }
    }

    public enum BossSoundType
    {
        Player,
        BossPvs,
        Position,
        Pvs
    }
}
