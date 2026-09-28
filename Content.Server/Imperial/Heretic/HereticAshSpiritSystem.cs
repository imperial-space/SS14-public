using System.Numerics;
using System.Threading;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticAshSpiritSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics   = default!;
    [Dependency] private readonly SharedStealthSystem _stealth   = default!;
    [Dependency] private readonly SharedAudioSystem   _audio     = default!;
    [Dependency] private readonly PopupSystem         _popup     = default!;
    [Dependency] private readonly ChatSystem          _chat      = default!;
    [Dependency] private readonly EntityLookupSystem  _lookup    = default!;
    [Dependency] private readonly MobStateSystem      _mobs      = default!;
    [Dependency] private readonly DamageableSystem    _damage    = default!;
    [Dependency] private readonly FlammableSystem     _flammable = default!;

    private readonly Dictionary<EntityUid, CancellationTokenSource> _flameOathCancels = new();
    private readonly Dictionary<EntityUid, EntityUid> _flameOathAudio = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticAshSpiritComponent, HereticAshSpiritShiftActionEvent>(OnShift);
        SubscribeLocalEvent<HereticAshSpiritComponent, HereticAshSpiritFlameOathActionEvent>(OnFlameOath);
    }

    private void OnFlameOath(EntityUid uid, HereticAshSpiritComponent comp, HereticAshSpiritFlameOathActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        _chat.TrySendInGameICMessage(uid, Loc.GetString("heretic-incantation-flame-oath"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);
        _popup.PopupEntity(Loc.GetString("heretic-ash-spirit-flame-oath-popup"), uid, uid, PopupType.Medium);

        StartFlameOath(uid, 300);
    }

    public void StartFlameOath(EntityUid uid, int ticksLeft)
    {
        if (_flameOathCancels.Remove(uid, out var oldCts))
            oldCts.Cancel();
        if (_flameOathAudio.Remove(uid, out var oldAudio))
            _audio.Stop(oldAudio);

        var cts = new CancellationTokenSource();
        _flameOathCancels[uid] = cts;

        var audioEnt = _audio.PlayPvs(
            new SoundPathSpecifier("/Audio/Imperial/Seriozha/SCP/fire.ogg"),
            uid,
            AudioParams.Default.WithLoop(true))?.Entity;
        if (audioEnt.HasValue)
            _flameOathAudio[uid] = audioEnt.Value;

        FlameOathTick(uid, ticksLeft, cts.Token);
    }

    private void FlameOathTick(EntityUid uid, int ticksLeft, CancellationToken token)
    {
        if (ticksLeft <= 0)
        {
            _flameOathCancels.Remove(uid);
            if (_flameOathAudio.Remove(uid, out var audioEnt))
                _audio.Stop(audioEnt);
            return;
        }

        Timer.Spawn(200, () =>
        {
            if (!Exists(uid)) return;
            if (!TryComp<MobStateComponent>(uid, out var mobState) || !_mobs.IsAlive(uid, mobState)) return;

            var origin = Transform(uid).Coordinates;

            // Спавн огня на 9 тайлах 3x3. Сущности живут 0.5 сек — при движении духа
            // старые тайлы гаснут, новые появляются → эффект огненного следа.
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
                Spawn("HereticAshSpiritFire", origin.Offset(new Vector2(dx, dy)));

            var dmg = new DamageSpecifier();
            dmg.DamageDict["Heat"] = FixedPoint2.New(0.5f);

            foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(origin, 1.5f))
            {
                if (ent.Owner == uid) continue;
                if (!_mobs.IsAlive(ent.Owner, ent.Comp)) continue;
                _damage.TryChangeDamage(ent.Owner, dmg, ignoreResistances: false);
                _flammable.AdjustFireStacks(ent.Owner, 0.5f, ignite: true);
            }

            FlameOathTick(uid, ticksLeft - 1, token);
        }, token);
    }

    private void OnShift(EntityUid uid, HereticAshSpiritComponent comp, HereticAshSpiritShiftActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        if (!TryComp<PhysicsComponent>(uid, out var physics) || !TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        _chat.TrySendInGameICMessage(uid, Loc.GetString("heretic-incantation-ashen-passage"), InGameICChatType.Whisper, false, ignoreActionBlocker: true);

        var fixtureStates = new List<(string Id, bool Hard, int Layer, int Mask)>();
        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            fixtureStates.Add((id, fixture.Hard, fixture.CollisionLayer, fixture.CollisionMask));
            _physics.SetHard(uid, fixture, false, fixtures);
            _physics.SetCollisionLayer(uid, id, fixture, (int) CollisionGroup.None, fixtures, physics);
            _physics.SetCollisionMask(uid, id, fixture, (int) CollisionGroup.None, fixtures, physics);
        }

        Spawn("HereticEffectAshBlink", Transform(uid).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_magic_ethereal_enter.ogg"), uid);

        var stealthComp = EnsureComp<StealthComponent>(uid);
        _stealth.SetEnabled(uid, true, stealthComp);
        _stealth.SetVisibility(uid, -1f, stealthComp);

        _popup.PopupEntity(Loc.GetString("heretic-ash-passage"), uid, uid, PopupType.Medium);

        Timer.Spawn(TimeSpan.FromSeconds(1.1), () =>
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
            if (TryComp<StealthComponent>(uid, out var sc) && sc.Enabled)
                _stealth.SetEnabled(uid, false, sc);
        });
    }
}
