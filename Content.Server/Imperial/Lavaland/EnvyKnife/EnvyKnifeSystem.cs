using Content.Server.Body;
using Content.Shared.Body;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Lavaland.EnvyKnife;

/// <summary>
/// Нож Зависти — при попадании по гуманоиду копирует его внешность
/// (цвет кожи, глаз, маркинги) на владельца ножа.
/// </summary>
public sealed class EnvyKnifeSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly VisualBodySystem _visualBody = default!;

    public override void Initialize()
    {
        base.Initialize();
        // MeleeHitEvent — sealed class; используем старый стиль (EntityUid, TComp, TEvent).
        SubscribeLocalEvent<EnvyKnifeComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(EntityUid uid, EnvyKnifeComponent comp, MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        var wielder = args.User;

        foreach (var target in args.HitEntities)
        {
            if (!HasComp<BodyComponent>(target))
                continue;

            _visualBody.CopyAppearanceFrom(target, wielder);
            _audio.PlayPvs(comp.TransformSound, wielder);
            break;
        }
    }
}
