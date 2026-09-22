using System.Numerics;
using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticStarTouchBeamOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    private readonly IEntityManager _ent;
    private readonly SharedTransformSystem _xform;
    private readonly IGameTiming _timing;
    private readonly ShaderInstance? _unshadedShader;

    private readonly Texture[]? _frames;
    private readonly float[]? _delays;
    private readonly float _cycleDuration;

    private static readonly ProtoId<ShaderPrototype> UnshadedId = "unshaded";

    public HereticStarTouchBeamOverlay(IEntityManager ent, IGameTiming timing, IResourceCache cache, IPrototypeManager proto)
    {
        _ent = ent;
        _xform = ent.System<SharedTransformSystem>();
        _timing = timing;
        _unshadedShader = proto.Index(UnshadedId).InstanceUnique();

        if (cache.TryGetResource<RSIResource>(new ResPath("/Textures/Imperial/heretic/beam.rsi"), out var res))
        {
            var rsi = res.RSI;
            if (rsi.TryGetState("cosmic_beam", out var state))
            {
                _frames = state.GetFrames(RsiDirection.South);
                _delays = state.GetDelays();
                _cycleDuration = 0f;
                foreach (var d in _delays)
                    _cycleDuration += d;
            }
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_frames == null || _frames.Length == 0)
            return;

        var worldHandle = args.WorldHandle;
        var xformQuery = _ent.GetEntityQuery<TransformComponent>();
        var texture = GetCurrentFrame();

        worldHandle.UseShader(_unshadedShader);
        worldHandle.SetTransform(Matrix3x2.Identity);

        var query = _ent.EntityQueryEnumerator<HereticStarTouchBeamComponent>();
        while (query.MoveNext(out var uid, out var beam))
        {
            if (beam.Target == EntityUid.Invalid)
                continue;

            if (!xformQuery.TryGetComponent(uid, out var xform) ||
                !xformQuery.TryGetComponent(beam.Target, out var targetXform))
                continue;

            if (xform.MapID != args.MapId || xform.MapID != targetXform.MapID)
                continue;

            var posA = _xform.GetWorldPosition(xform);
            var posB = _xform.GetWorldPosition(targetXform);
            var diff = posB - posA;
            var length = diff.Length();

            if (length < 0.01f)
                continue;

            var angle = diff.ToWorldAngle();
            var dir = diff / length;
            var numTiles = (int)Math.Ceiling(length);

            for (var i = 0; i < numTiles; i++)
            {
                var tileCenter = posA + dir * (i + 0.5f);
                var box = new Box2(-0.5f, -0.5f, 0.5f, 0.5f);
                var rotated = new Box2Rotated(box.Translated(tileCenter), angle, tileCenter);
                worldHandle.DrawTextureRect(texture, rotated);
            }
        }

        worldHandle.UseShader(null);
    }

    private Texture GetCurrentFrame()
    {
        if (_delays == null || _frames == null)
            return _frames![0];

        var t = (float)(_timing.CurTime.TotalSeconds % _cycleDuration);
        float acc = 0f;
        for (var i = 0; i < _delays.Length; i++)
        {
            acc += _delays[i];
            if (t < acc)
                return _frames[i];
        }
        return _frames[_frames.Length - 1];
    }
}
