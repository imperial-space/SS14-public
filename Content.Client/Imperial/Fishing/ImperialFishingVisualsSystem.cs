using System.Numerics;
using Content.Shared.Imperial.Fishing;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Fishing;

/// <summary>
/// Клиентская анимация рыбалки: покачивание поплавка, огонёк блесны,
/// трепыхание живой рыбы (flop_animation) и её маскировка.
/// </summary>
public sealed class ImperialFishingVisualsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private const string LureLayer = "lure";

    /// <summary>Сдвиг фазы трепыхания, чтобы рыбы не дёргались синхронно.</summary>
    private readonly Dictionary<EntityUid, float> _phase = new();

    /// <summary>Когда рыба последний раз двигалась (для маскировки).</summary>
    private readonly Dictionary<EntityUid, (Vector2 Pos, double Time)> _lastMove = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialFishComponent, ComponentShutdown>((uid, _, _) =>
        {
            _phase.Remove(uid);
            _lastMove.Remove(uid);
        });
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var time = (float) _timing.RealTime.TotalSeconds;

        var floats = EntityQueryEnumerator<ImperialFishingFloatComponent, SpriteComponent>();
        while (floats.MoveNext(out var uid, out var comp, out var sprite))
        {
            // WAIT_PHASE: ±1 px за секунду; BITING_PHASE: ±3 px за полсекунды.
            var amplitude = comp.Biting ? 3f : 1f;
            var period = comp.Biting ? 1f : 2f;
            var offset = MathF.Sin(time * MathF.Tau / period) * amplitude / 32f;
            _sprite.SetOffset((uid, sprite), new Vector2(0, offset));

            if (_sprite.LayerMapTryGet((uid, sprite), LureLayer, out var layer, false))
            {
                _sprite.LayerSetVisible((uid, sprite), layer, comp.HasLure);
                _sprite.LayerSetColor((uid, sprite), layer, comp.SpinReady ? Color.Lime : Color.Red);
            }
        }

        var fishes = EntityQueryEnumerator<ImperialFishComponent, SpriteComponent, TransformComponent>();
        while (fishes.MoveNext(out var uid, out var fish, out var sprite, out var xform))
        {
            UpdateFlop(uid, fish, sprite, time);
            UpdateCamouflage(uid, fish, sprite, xform, time);
        }
    }

    private void UpdateFlop(EntityUid uid, ImperialFishComponent fish, SpriteComponent sprite, float time)
    {
        var flopping = fish.Flops && !fish.Dead && !_container.IsEntityInContainer(uid);
        if (!flopping)
        {
            if (sprite.Rotation != Angle.Zero)
                _sprite.SetRotation((uid, sprite), Angle.Zero);
            return;
        }

        if (!_phase.TryGetValue(uid, out var phase))
            _phase[uid] = phase = Random.Shared.NextSingle() * 5f;

        // Пауза 1.5–2 с, затем два кивка носом на 20° (FLOP_COUNT, FLOP_DEGREE).
        var cycle = 1.5f + phase * 0.1f + 2 * (3 * 0.15f + 0.2f);
        var t = (time + phase) % cycle - (1.5f + phase * 0.1f);
        if (t < 0)
        {
            _sprite.SetRotation((uid, sprite), Angle.Zero);
            return;
        }

        var flop = t % 0.65f;
        var degrees = flop switch
        {
            < 0.15f => -20f * (flop / 0.15f),
            < 0.30f => -20f + 40f * ((flop - 0.15f) / 0.15f),
            < 0.45f => 20f * (1 - (flop - 0.30f) / 0.15f),
            _ => 0f,
        };
        _sprite.SetRotation((uid, sprite), Angle.FromDegrees(degrees));
    }

    private void UpdateCamouflage(EntityUid uid, ImperialFishComponent fish, SpriteComponent sprite, TransformComponent xform, float time)
    {
        var camo = !fish.Dead && fish.Traits.Exists(t => t.Id == "Camouflage");
        if (!camo)
        {
            if (sprite.Color.A < 1f)
                _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(1f));
            return;
        }

        var pos = xform.LocalPosition;
        if (!_lastMove.TryGetValue(uid, out var last) || (last.Pos - pos).LengthSquared() > 0.0001f)
        {
            _lastMove[uid] = (pos, time);
            _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(1f));
            return;
        }

        // Через 5 секунд неподвижности рыба тает до почти полной прозрачности.
        var still = time - (float) last.Time - 5f;
        if (still <= 0)
            return;

        var alpha = MathF.Max(1f - still * 10f / 255f, 10f / 255f);
        _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(alpha));
    }
}
