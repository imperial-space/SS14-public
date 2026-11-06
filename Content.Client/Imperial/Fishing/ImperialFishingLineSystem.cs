using System.Numerics;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Physics;
using Robust.Client.Graphics;

namespace Content.Client.Imperial.Fishing;

/// <summary>
/// /datum/beam/fishing_line: леска тянется от кончика удочки, а не от центра рыбака.
/// Смещение кончика зависит от того, куда смотрит рыбак и в какой руке удочка (update_offsets).
/// </summary>
public sealed class ImperialFishingLineSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>Пиксели TG (righthand/lefthand × S/E/W/N), от центра клетки.</summary>
    private static readonly Dictionary<Direction, Vector2> RightHand = new()
    {
        [Direction.South] = new(13, 16),
        [Direction.East] = new(18, 16),
        [Direction.West] = new(-20, 18),
        [Direction.North] = new(-14, 16),
    };

    private static readonly Dictionary<Direction, Vector2> LeftHand = new()
    {
        [Direction.South] = new(-13, 15),
        [Direction.East] = new(24, 18),
        [Direction.West] = new(-17, 16),
        [Direction.North] = new(13, 15),
    };

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var eyeRot = _eye.CurrentEye.Rotation;
        var query = EntityQueryEnumerator<ImperialFishingFloatComponent, JointVisualsComponent>();
        while (query.MoveNext(out _, out _, out var visuals))
        {
            if (visuals.Target is not { } user || !TryComp(user, out TransformComponent? xform))
                continue;

            var left = false;
            var holding = false;
            if (TryComp<HandsComponent>(user, out var hands))
            {
                foreach (var held in _hands.EnumerateHeld((user, hands)))
                {
                    if (!HasComp<ImperialFishingRodComponent>(held) || !_hands.IsHolding((user, hands), held, out var handId)
                        || !_hands.TryGetHand((user, hands), handId, out var hand))
                        continue;

                    holding = true;
                    left = hand.Value.Location == HandLocation.Left;
                    break;
                }
            }

            // Без удочки в руках (перчатки, модуль МОДа) леска идёт от рыбака.
            if (!holding)
            {
                visuals.OffsetB = Vector2.Zero;
                continue;
            }

            var worldRot = _transform.GetWorldRotation(xform);
            var dir = (worldRot + eyeRot).GetCardinalDir();
            var pixels = (left ? LeftHand : RightHand).GetValueOrDefault(dir);

            // Экранное смещение → мировое → локальное (оверлей поворачивает OffsetB на LocalRotation в системе родителя).
            var world = (-eyeRot).RotateVec(pixels / EyeManager.PixelsPerMeter);
            var parentRot = worldRot - xform.LocalRotation;
            var local = (-xform.LocalRotation).RotateVec((-parentRot).RotateVec(world));
            visuals.OffsetB = local;
        }
    }
}
