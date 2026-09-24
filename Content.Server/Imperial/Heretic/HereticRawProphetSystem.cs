using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticRawProphetSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem   _physics        = default!;
    [Dependency] private readonly SharedAudioSystem     _audio          = default!;
    [Dependency] private readonly PopupSystem           _popup          = default!;
    [Dependency] private readonly ChatSystem            _chat           = default!;
    [Dependency] private readonly StatusEffectsSystem   _statusEffects  = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticRawProphetComponent, HereticRawProphetJauntActionEvent>(OnJaunt);
        SubscribeLocalEvent<HereticRawProphetComponent, HereticAshenPassageActionEvent>(OnAshJaunt);
        SubscribeLocalEvent<HereticRawProphetComponent, HereticRawProphetBlindActionEvent>(OnBlind);
    }

    private void OnJaunt(EntityUid uid, HereticRawProphetComponent comp, HereticRawProphetJauntActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;
        ExecuteJaunt(uid);
    }

    private void OnAshJaunt(EntityUid uid, HereticRawProphetComponent comp, HereticAshenPassageActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;
        ExecuteJaunt(uid);
    }

    private void ExecuteJaunt(EntityUid uid)
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics) || !TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        _chat.TrySendInGameICMessage(uid, Loc.GetString("heretic-incantation-raw-prophet-jaunt"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_magic_ethereal_enter.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-raw-prophet-jaunt-popup"), uid, uid, PopupType.Medium);

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

    private void OnBlind(EntityUid uid, HereticRawProphetComponent comp, HereticRawProphetBlindActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        var target = args.Target;

        _popup.PopupEntity(Loc.GetString("heretic-raw-prophet-blind-popup-self"), uid, uid, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("heretic-raw-prophet-blind-popup-target"), target, target, PopupType.LargeCaution);

        _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(
            target,
            TemporaryBlindnessSystem.BlindingStatusEffect,
            TimeSpan.FromSeconds(8),
            true);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/blind.ogg"), uid);
    }
}
