using Content.Server.Popups;
using Content.Shared.Imperial.Medieval.Grab.Components;
using Content.Shared.Imperial.Medieval.Grab.Systems;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Throwing;
using Robust.Server.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Audio;

namespace Content.Server.Imperial.Medieval.Grab;

public sealed class GrabThrowSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly GrabSystem _grab = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    /// <summary>
    /// Попытка бросить существо, удерживаемый игроком в грабе.
    /// </summary>
    public bool TryThrowGrabbed(EntityUid player, EntityCoordinates target)
    {
        // Проверяем, есть ли у игрока активный граб
        if (!TryComp<GrabberComponent>(player, out var grabber) ||
            grabber.GrabbedEntity is not { Valid: true } grabbed)
            return false;

        if (!HasComp<PhysicsComponent>(grabbed))
            return false;

        if (TryComp<GrabbableComponent>(grabbed, out var grabbable))
            _grab.TryStopGrab(grabbed, grabbable, player);

        // Определяем силу по навыку Strength
        int levelStrength = 10;
        if (TryComp<SkillsComponent>(player, out var skills))
            skills.Levels.TryGetValue(SharedSkillsSystem.StrengthId, out levelStrength);

        float throwDistance = 1.5f;
        if (levelStrength > 10)
            throwDistance += (levelStrength - 10) * 0.13f; // За каждый уровень силы больше 10 прибавляем 0.13 к дальности броска
        else if (levelStrength < 10)
            throwDistance -= (10 - levelStrength) * 0.18f; // За каждый уровень силы меньше 10 отбавляем 0.25 к дальности броска

        throwDistance = MathF.Max(throwDistance, 0.5f);

        var grabbedPos = _transform.GetMapCoordinates(grabbed).Position;
        var targetPos = _transform.ToMapCoordinates(target).Position;
        var dirVector = targetPos - grabbedPos;

        if (dirVector.Length() > throwDistance)
            dirVector = dirVector.Normalized() * throwDistance;

        float baseThrowSpeed = (dirVector.Length() / 3f) * 30f;

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg"), grabbed);
        _throwing.TryThrow(
            grabbed,
            dirVector,
            baseThrowSpeed: baseThrowSpeed,
            user: player,
            pushbackRatio: 2f,
            recoil: false,
            playSound: true,
            doSpin: false
        );

        _popup.PopupEntity(
            Loc.GetString("grab-throw-success", ("target", MetaData(grabbed).EntityName)),
            player,
            player
        );

        return true;
    }
}
