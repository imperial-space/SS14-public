using Content.Server.Chat.Systems;
using Content.Server.Emp;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticStalkerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem    _audio    = default!;
    [Dependency] private readonly ChatSystem           _chat     = default!;
    [Dependency] private readonly EmpSystem            _emp      = default!;
    [Dependency] private readonly SharedPhysicsSystem  _physics  = default!;
    [Dependency] private readonly PolymorphSystem      _polymorph = default!;
    [Dependency] private readonly PopupSystem          _popup    = default!;
    [Dependency] private readonly IRobustRandom        _random   = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticStalkerComponent, HereticStalkerJauntActionEvent>(OnJaunt);
        SubscribeLocalEvent<HereticStalkerComponent, HereticAshenPassageActionEvent>(OnAshJaunt);
        SubscribeLocalEvent<HereticStalkerComponent, HereticStalkerEmpActionEvent>(OnEmp);
        SubscribeLocalEvent<HereticStalkerComponent, HereticStalkerPolymorphActionEvent>(OnPolymorph);
    }

    private void OnJaunt(EntityUid uid, HereticStalkerComponent comp, HereticStalkerJauntActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;
        ExecuteJaunt(uid);
    }

    private void OnAshJaunt(EntityUid uid, HereticStalkerComponent comp, HereticAshenPassageActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;
        ExecuteJaunt(uid);
    }

    private void ExecuteJaunt(EntityUid uid)
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics) || !TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        _chat.TrySendInGameICMessage(uid, Loc.GetString("heretic-incantation-stalker-jaunt"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_magic_ethereal_enter.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-stalker-jaunt-popup"), uid, uid, PopupType.Medium);

        var fixtureStates = new List<(string Id, bool Hard, int Layer, int Mask)>();
        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            fixtureStates.Add((id, fixture.Hard, fixture.CollisionLayer, fixture.CollisionMask));
            _physics.SetHard(uid, fixture, false, fixtures);
            _physics.SetCollisionLayer(uid, id, fixture, (int) CollisionGroup.None, fixtures, physics);
            _physics.SetCollisionMask(uid, id, fixture, (int) CollisionGroup.None, fixtures, physics);
        }

        Spawn("HereticEffectAshBlink", Transform(uid).Coordinates);

        Timer.Spawn(TimeSpan.FromSeconds(2.5), () =>
        {
            if (!Exists(uid)) return;
            if (!TryComp<PhysicsComponent>(uid, out var physics2) || !TryComp<FixturesComponent>(uid, out var fixtures2))
                return;

            foreach (var (id, hard, layer, mask) in fixtureStates)
            {
                if (!fixtures2.Fixtures.TryGetValue(id, out var fixture)) continue;
                _physics.SetHard(uid, fixture, hard, fixtures2);
                _physics.SetCollisionLayer(uid, id, fixture, layer, fixtures2, physics2);
                _physics.SetCollisionMask(uid, id, fixture, mask, fixtures2, physics2);
            }

            if (!Exists(uid)) return;
            Spawn("HereticEffectAshBlink", Transform(uid).Coordinates);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_magic_ethereal_exit.ogg"), Transform(uid).Coordinates);
        });
    }

    private void OnEmp(EntityUid uid, HereticStalkerComponent comp, HereticStalkerEmpActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        var coords = _transform.GetMapCoordinates(uid);
        _emp.EmpPulse(coords, 5f, 300f, TimeSpan.FromSeconds(15));
        _popup.PopupEntity(Loc.GetString("heretic-stalker-emp-popup"), uid, uid, PopupType.Medium);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/sparks4.ogg"), uid);
    }

    private void OnPolymorph(EntityUid uid, HereticStalkerComponent comp, HereticStalkerPolymorphActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        var allPolymorphs = new List<string>(comp.AnimalPolymorphs);
        allPolymorphs.AddRange(comp.GolemPolymorphs);

        if (allPolymorphs.Count == 0)
            return;

        var chosen = _random.Pick(allPolymorphs);
        _polymorph.PolymorphEntity(uid, chosen);
        _popup.PopupEntity(Loc.GetString("heretic-stalker-polymorph-popup"), uid, uid, PopupType.Medium);
    }
}
