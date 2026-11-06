using Content.Server.Body.Systems;
using Content.Server.Popups;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Flesh;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Imperial.Heretic.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Heretic.Paths.Flesh;

public sealed class HereticFleshBladeSystem : EntitySystem
{
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly HereticSystem _heretic = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    /// <summary>Кровотечение, которое открывает сработавшая метка Плоти.</summary>
    private const float FleshMarkBleed = 15f;

    private static readonly ProtoId<HereticKnowledgePrototype> KnowledgeBleedingSteelHereticKnowledge = "KnowledgeBleedingSteel";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticFleshBladeComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(EntityUid uid, HereticFleshBladeComponent comp, MeleeHitEvent args)
    {
        if (!args.IsHit || !TryComp<HereticComponent>(args.User, out var heretic))
            return;

        var bleedingSteel = _heretic.HasKnowledge(heretic, KnowledgeBleedingSteelHereticKnowledge);
        foreach (var target in args.HitEntities)
        {
            if (bleedingSteel)
                _bloodstream.TryModifyBleedAmount(target, comp.MeleeHitAmount);

            // Метка Плоти: удар клинком Плоти по меченой цели вскрывает раны.
            if (!RemComp<FleshMarkComponent>(target))
                continue;

            _bloodstream.TryModifyBleedAmount(target, FleshMarkBleed);
            _popup.PopupEntity(Loc.GetString("heretic-flesh-mark-triggered"), target, target, PopupType.MediumCaution);
        }
    }
}
