using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.Maths;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticMoonIllusionMovementSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming      _timing    = default!;
    [Dependency] private readonly IRobustRandom    _random    = default!;
    [Dependency] private readonly SharedAudioSystem _audio    = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private static readonly SoundCollectionSpecifier FootstepSound = new("FootstepFloor");

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<HereticMoonIllusionMovementComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var move, out var xform))
        {
            if (now >= move.NextDirectionChange)
            {
                move.CurrentAngle        = _random.NextFloat(0f, MathF.PI * 2f);
                move.NextDirectionChange = now + TimeSpan.FromSeconds(_random.NextFloat(1.5f, 3.5f));
                _xform.SetLocalRotation(uid, new Angle(move.CurrentAngle), xform);
            }

            var dir    = new Vector2(MathF.Cos(move.CurrentAngle), MathF.Sin(move.CurrentAngle));
            var delta  = dir * move.Speed * frameTime;
            var newPos = xform.LocalPosition + delta;
            _xform.SetLocalPosition(uid, newPos, xform);

            if (now >= move.NextFootstep)
            {
                move.NextFootstep = now + TimeSpan.FromSeconds(0.5f);
                _audio.PlayPvs(FootstepSound, uid);
            }
        }
    }
}
