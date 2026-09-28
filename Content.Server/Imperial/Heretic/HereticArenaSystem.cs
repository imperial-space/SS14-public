using Content.Server.Decals;
using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Slippery;
using Content.Shared.Bed.Sleep;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Events;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticArenaSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly HereticSystem _heretic = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticArenaParticipantComponent, MobStateChangedEvent>(OnParticipantStateChanged);
        SubscribeLocalEvent<HereticArenaParticipantComponent, KnockDownAttemptEvent>(OnParticipantKnockdownAttempt);
        SubscribeLocalEvent<HereticArenaParticipantComponent, TryingToSleepEvent>(OnParticipantSleepAttempt);
        SubscribeLocalEvent<HereticArenaComponent, ComponentShutdown>(OnArenaShutdown);
        SubscribeLocalEvent<MeleeWeaponComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<HereticArenaWallComponent, PreventCollideEvent>(OnWallPreventCollide);
        // Арена обрушается, если еретик входит в крит или умирает
        SubscribeLocalEvent<HereticComponent, MobStateChangedEvent>(OnHereticStateChanged);
    }

    private void OnWallPreventCollide(EntityUid uid, HereticArenaWallComponent comp, ref PreventCollideEvent args)
    {
        if (TryComp<HereticArenaParticipantComponent>(args.OtherEntity, out var participant) && participant.IsVictor)
            args.Cancelled = true;
    }

    private void OnMeleeHit(Entity<MeleeWeaponComponent> weapon, ref MeleeHitEvent args)
    {
        if (!args.IsHit) return;
        foreach (var hit in args.HitEntities)
        {
            if (TryComp<HereticArenaParticipantComponent>(hit, out var participant))
                participant.LastAttacker = args.User;
        }
    }

    private void OnParticipantKnockdownAttempt(EntityUid uid, HereticArenaParticipantComponent comp, ref KnockDownAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnParticipantSleepAttempt(EntityUid uid, HereticArenaParticipantComponent comp, ref TryingToSleepEvent args)
    {
        args.Cancelled = true;
    }

    private void OnHereticStateChanged(EntityUid uid, HereticComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical && args.NewMobState != MobState.Dead)
            return;

        var arenaQuery = EntityQueryEnumerator<HereticArenaComponent>();
        while (arenaQuery.MoveNext(out var arenaUid, out var arena))
        {
            if (arena.Heretic != uid) continue;
            _popup.PopupEntity(Loc.GetString("heretic-wolves-heretic-fallen"), uid, uid, PopupType.LargeCaution);
            QueueDel(arenaUid);
            break;
        }
    }

    private void OnParticipantStateChanged(EntityUid uid, HereticArenaParticipantComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical && args.NewMobState != MobState.Dead)
            return;

        if (!TryComp<HereticArenaComponent>(comp.Arena, out var arena))
            return;

        var heretic = arena.Heretic;
        var victor = comp.LastAttacker;

        if (Exists(heretic) && victor == heretic)
        {
            // Еретик победил — лечение (SS13: heal_overall_damage(60,60,60) + tox/oxy -60)
            var heal = new DamageSpecifier();
            heal.DamageDict["Blunt"] = FixedPoint2.New(-60);
            heal.DamageDict["Heat"] = FixedPoint2.New(-60);
            heal.DamageDict["Cellular"] = FixedPoint2.New(-60);
            heal.DamageDict["Asphyxiation"] = FixedPoint2.New(-60);
            heal.DamageDict["Poison"] = FixedPoint2.New(-60);
            _damage.TryChangeDamage(heretic, heal, ignoreResistances: true);

            _heretic.SpawnOrbitingBlade(heretic);

            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), heretic);
            _popup.PopupEntity(Loc.GetString("heretic-wolves-victory"), heretic, heretic, PopupType.Large);
        }
        else if (Exists(victor)
              && TryComp<HereticArenaParticipantComponent>(victor, out var victorComp))
        {
            // Победитель получает право пройти сквозь стены арены
            victorComp.IsVictor = true;
            RemComp<NoSlipComponent>(victor);
            _popup.PopupEntity(Loc.GetString("heretic-wolves-victor-escape"), victor, victor, PopupType.Large);
            if (victorComp.TrainingBlade != default && Exists(victorComp.TrainingBlade))
                QueueDel(victorComp.TrainingBlade);
            arena.Participants.Remove(victor);

            // Через 60 секунд — убираем маркерный компонент
            Timer.Spawn(60_000, () =>
            {
                if (Exists(victor))
                    RemCompDeferred<HereticArenaParticipantComponent>(victor);
            });
        }

        _popup.PopupEntity(Loc.GetString("heretic-wolves-defeated"), uid, uid, PopupType.LargeCaution);

        if (comp.TrainingBlade != default && Exists(comp.TrainingBlade))
            QueueDel(comp.TrainingBlade);

        RemoveParticipant(uid, comp);
        arena.Participants.Remove(uid);

        if (arena.Participants.Count == 0 && Exists(comp.Arena))
        {
            if (Exists(heretic))
                _popup.PopupEntity(Loc.GetString("heretic-wolves-sated"), heretic, heretic, PopupType.Large);
            QueueDel(comp.Arena);
        }
    }

    public void AddParticipant(EntityUid uid, EntityUid arenaMarker, HereticArenaComponent arena)
    {
        var participant = EnsureComp<HereticArenaParticipantComponent>(uid);
        participant.Arena = arenaMarker;
        arena.Participants.Add(uid);

        EnsureComp<NoSlipComponent>(uid);

        _popup.PopupEntity(Loc.GetString("heretic-wolves-participant-trapped"), uid, uid, PopupType.LargeCaution);
    }

    private void RemoveParticipant(EntityUid uid, HereticArenaParticipantComponent comp)
    {
        RemComp<NoSlipComponent>(uid);
        RemCompDeferred<HereticArenaParticipantComponent>(uid);
    }

    private void OnArenaShutdown(EntityUid uid, HereticArenaComponent comp, ComponentShutdown args)
    {
        foreach (var wall in comp.Walls)
        {
            if (Exists(wall))
                QueueDel(wall);
        }

        foreach (var participant in comp.Participants)
        {
            if (!Exists(participant)) continue;
            if (TryComp<HereticArenaParticipantComponent>(participant, out var partComp))
            {
                if (partComp.TrainingBlade != default && Exists(partComp.TrainingBlade))
                    QueueDel(partComp.TrainingBlade);
                RemoveParticipant(participant, partComp);
            }
        }

        if (comp.FloorGrid != default && Exists(comp.FloorGrid))
        {
            foreach (var decalId in comp.FloorDecals)
                _decals.RemoveDecal(comp.FloorGrid, decalId);
        }
    }
}
