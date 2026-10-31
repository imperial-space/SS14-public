using Content.Server.Popups;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Варп-врата флота Хаоса: с флагмана на десантные корабли и бомбардировщики и обратно. Пройти может только слуга Тёмных богов.
/// </summary>
public sealed class CadiaWarpGateSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private static readonly SoundSpecifier StepSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/exit_blood.ogg");
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(3);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaWarpGateComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
    }

    private void OnVerbs(Entity<CadiaWarpGateComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        var user = args.User;
        var query = EntityQueryEnumerator<CadiaWarpGateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var gate, out var xform))
        {
            if (uid == ent.Owner || xform.MapUid != Transform(ent).MapUid)
                continue;
            var target = uid;
            var label = gate.Label is { } key ? Loc.GetString(key) : Name(xform.GridUid ?? uid);
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cadia-warp-gate-verb", ("target", label)),
                Category = VerbCategory.SelectType,
                Act = () => Step(ent, target, user),
            });
        }
    }

    private void Step(Entity<CadiaWarpGateComponent> from, EntityUid to, EntityUid user)
    {
        if (!TryComp<CadiaFactionMemberComponent>(user, out var member) || member.Faction != CadiaFaction.Chaos)
        {
            _popup.PopupEntity(Loc.GetString("cadia-warp-gate-denied"), user, user, PopupType.MediumCaution);
            return;
        }
        var now = _timing.CurTime;
        if (now < from.Comp.NextUse || TerminatingOrDeleted(to))
            return;
        from.Comp.NextUse = now + Cooldown;

        Spawn("CadiaPsyRingRed", _xform.GetMapCoordinates(user));
        _audio.PlayPvs(StepSound, from);
        _xform.SetCoordinates(user, Transform(to).Coordinates);
        _xform.AttachToGridOrMap(user);
        Spawn("CadiaPsyRingRed", _xform.GetMapCoordinates(to));
        _audio.PlayPvs(StepSound, to);
    }
}
