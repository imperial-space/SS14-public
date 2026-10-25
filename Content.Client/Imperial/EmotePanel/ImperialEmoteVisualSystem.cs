using System.Numerics;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.EmotePanel;
using Content.Shared.Standing;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.EmotePanel;

/// <summary>Анимации run_emote TG (кувырок, кручение, дрожь, прыжок) и наложения emote_visuals.dmi.</summary>
public sealed class ImperialEmoteVisualSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly ResPath Visuals = new("/Textures/Imperial/EmotePanel/emote_visuals.rsi");
    private const string AnimationKey = "imperial-emote";

    /// <summary>1 пиксель BYOND в тайлах.</summary>
    private const float Pixel = 1f / 32f;

    /// <summary>FLIP_EMOTE_DURATION.</summary>
    private const float FlipDuration = 0.7f;

    private sealed class Spin
    {
        public TimeSpan End;
        public TimeSpan Next;
        public float Step;
        public bool RandomStep;
        public int Index;
        public bool HadOverride;
        public Direction OldDirection;
    }

    private readonly Dictionary<EntityUid, Spin> _spins = new();
    private readonly List<(TimeSpan End, EntityUid Uid, string Key)> _overlays = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ImperialEmoteVisualMessage>(OnVisual);
    }

    private void OnVisual(ImperialEmoteVisualMessage msg)
    {
        if (!TryGetEntity(msg.Entity, out var entity) || !TryComp<SpriteComponent>(entity, out var sprite))
            return;

        var uid = entity.Value;
        switch (msg.Effect)
        {
            case ImperialEmoteEffect.Flip:
            case ImperialEmoteEffect.Backflip:
                PlayFlip(uid, msg.Effect == ImperialEmoteEffect.Flip);
                break;
            case ImperialEmoteEffect.Spin:
                StartSpin(uid, sprite, 2f, 0.1f, false);
                break;
            case ImperialEmoteEffect.Taunt:
                StartSpin(uid, sprite, 1.6f, 0.1f, false);
                break;
            case ImperialEmoteEffect.Dance:
                StartSpin(uid, sprite, 5f, 0.1f, true);
                PlayDanceJitter(uid, sprite);
                break;
            case ImperialEmoteEffect.Jump:
                PlayOffsets(uid, sprite, true, (4, 0.1f), (0, 0.1f));
                break;
            case ImperialEmoteEffect.Shiver:
                PlayWiggle(uid, sprite, 1, 2, 5, 0.1f, 0.1f);
                break;
            case ImperialEmoteEffect.Sway:
                PlayWiggle(uid, sprite, 2, 6, 2, 0.5f, 1f);
                break;
            case ImperialEmoteEffect.Tremble:
                PlayWiggle(uid, sprite, 2, 4, 11, 0.2f, 0.2f);
                break;
            case ImperialEmoteEffect.Twitch:
                PlayOffsets(uid, sprite, false, (1, 0.1f), (-1, 0.1f), (-1, 0.1f), (1, 0.1f), (0, 0.1f));
                break;
            case ImperialEmoteEffect.TwitchSlight:
                PlayOffsets(uid, sprite, false, (-1, 0.1f), (0, 0.1f));
                break;
            case ImperialEmoteEffect.Tongue:
                AddOverlay(uid, sprite, "tongue", Color.FromHex("#ff6ea0"), 5.2f);
                break;
            case ImperialEmoteEffect.Blush:
                AddOverlay(uid, sprite, "blush", Color.FromHex("#DE5D83"), 5.2f);
                break;
            case ImperialEmoteEffect.Cry:
                AddOverlay(uid, sprite, "cry", Color.FromHex("#00A2FF"), 12.8f);
                break;
            case ImperialEmoteEffect.Sigh:
                AddOverlay(uid, sprite, "sigh", Color.White, 2f);
                break;
            case ImperialEmoteEffect.Glasses:
                AddOverlay(uid, sprite, "glasses", Color.White, 1.6f);
                break;
        }
    }

    #region Анимации

    /// <summary>SpinAnimation: оборот спрайта. Лежащих не крутим, их поворот задаёт падение.</summary>
    private void PlayFlip(EntityUid uid, bool clockwise)
    {
        if (TryComp<StandingStateComponent>(uid, out var standing) && !standing.Standing || _animation.HasRunningAnimation(uid, AnimationKey))
            return;

        var sign = clockwise ? -1 : 1;
        var animation = new Animation
        {
            Length = TimeSpan.FromSeconds(FlipDuration),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(Angle.Zero, 0f),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(120 * sign), FlipDuration / 3),
                        new AnimationTrackProperty.KeyFrame(Angle.FromDegrees(240 * sign), FlipDuration / 3),
                        new AnimationTrackProperty.KeyFrame(Angle.Zero, FlipDuration / 3),
                    },
                },
            },
        };

        _animation.Play(uid, animation, AnimationKey);
    }

    /// <summary>Сдвиги pixel_w/pixel_z по шагам (в пикселях от исходного положения, время шага в секундах).</summary>
    private void PlayOffsets(EntityUid uid, SpriteComponent sprite, bool vertical, params (int Pixels, float Time)[] steps)
    {
        if (_animation.HasRunningAnimation(uid, AnimationKey))
            return;

        var origin = sprite.Offset;
        var track = new AnimationTrackComponentProperty
        {
            ComponentType = typeof(SpriteComponent),
            Property = nameof(SpriteComponent.Offset),
            InterpolationMode = AnimationInterpolationMode.Linear,
            KeyFrames = { new AnimationTrackProperty.KeyFrame(origin, 0f) },
        };

        var length = 0f;
        foreach (var (pixels, time) in steps)
        {
            var shift = vertical ? new Vector2(0, pixels * Pixel) : new Vector2(pixels * Pixel, 0);
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(origin + shift, time));
            length += time;
        }

        _animation.Play(uid, new Animation { Length = TimeSpan.FromSeconds(length), AnimationTracks = { track } }, AnimationKey);
    }

    /// <summary>Покачивание как у shiver/sway/tremble: полшага вбок, loops раз туда-обратно, возврат.</summary>
    private void PlayWiggle(EntityUid uid, SpriteComponent sprite, int half, int full, int loops, float edgeTime, float stepTime)
    {
        var steps = new List<(int, float)> { (half, edgeTime) };
        for (var i = 0; i < loops; i++)
        {
            steps.Add((half - full, stepTime));
            steps.Add((half, stepTime));
        }

        steps.Add((0, edgeTime));
        PlayOffsets(uid, sprite, false, steps.ToArray());
    }

    /// <summary>do_jitter_animation танца: случайные подёргивания на всё время танца.</summary>
    private void PlayDanceJitter(EntityUid uid, SpriteComponent sprite)
    {
        var steps = new List<(int, float)>();
        for (var i = 0; i < 25; i++)
            steps.Add((_random.Next(-2, 3), 0.2f));
        steps.Add((0, 0.1f));
        PlayOffsets(uid, sprite, false, steps.ToArray());
    }

    /// <summary>spin(): смена направления взгляда по кругу.</summary>
    private void StartSpin(EntityUid uid, SpriteComponent sprite, float seconds, float step, bool randomStep)
    {
        var now = _timing.CurTime;
        if (!_spins.TryGetValue(uid, out var spin))
        {
            spin = new Spin { HadOverride = sprite.EnableDirectionOverride, OldDirection = sprite.DirectionOverride };
            _spins[uid] = spin;
        }

        spin.End = now + TimeSpan.FromSeconds(seconds);
        spin.Next = now;
        spin.Step = step;
        spin.RandomStep = randomStep;
    }

    private static readonly Direction[] SpinOrder = { Direction.South, Direction.West, Direction.North, Direction.East };

    #endregion

    #region Наложения

    private void AddOverlay(EntityUid uid, SpriteComponent sprite, string state, Color color, float seconds)
    {
        if (!HasComp<HumanoidProfileComponent>(uid))
            return;

        var key = $"imperial-emote-{state}";
        var ent = (uid, sprite);
        _sprite.RemoveLayer(ent, key, false);

        var index = _sprite.AddLayer(ent, new SpriteSpecifier.Rsi(Visuals, state));
        _sprite.LayerMapSet(ent, key, index);
        _sprite.LayerSetColor(ent, index, color);

        _overlays.RemoveAll(o => o.Uid == uid && o.Key == key);
        _overlays.Add((_timing.CurTime + TimeSpan.FromSeconds(seconds), uid, key));
    }

    #endregion

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var now = _timing.CurTime;

        foreach (var (uid, spin) in _spins)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite))
            {
                _spins.Remove(uid);
                continue;
            }

            if (now >= spin.End)
            {
                sprite.EnableDirectionOverride = spin.HadOverride;
                sprite.DirectionOverride = spin.OldDirection;
                _spins.Remove(uid);
                continue;
            }

            if (now < spin.Next)
                continue;

            sprite.EnableDirectionOverride = true;
            sprite.DirectionOverride = SpinOrder[spin.Index++ % SpinOrder.Length];
            var step = spin.RandomStep ? (_random.Prob(0.5f) ? 0.1f : 0.2f) : spin.Step;
            spin.Next = now + TimeSpan.FromSeconds(step);
        }

        for (var i = _overlays.Count - 1; i >= 0; i--)
        {
            var (end, uid, key) = _overlays[i];
            if (now < end)
                continue;

            _overlays.RemoveAt(i);
            if (TryComp<SpriteComponent>(uid, out var sprite))
                _sprite.RemoveLayer((uid, sprite), key, false);
        }
    }
}
