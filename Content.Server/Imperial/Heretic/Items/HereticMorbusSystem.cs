using Content.Server.Popups;
using Content.Server.Traits.Assorted;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic.Items;

public sealed class HereticMorbusSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming          _timing        = default!;
    [Dependency] private readonly DamageableSystem     _damage        = default!;
    [Dependency] private readonly EntityLookupSystem   _lookup        = default!;
    [Dependency] private readonly ParacusiaSystem      _paracusia     = default!;
    [Dependency] private readonly PopupSystem          _popup         = default!;
    [Dependency] private readonly StatusEffectsSystem  _statusEffects = default!;
    [Dependency] private readonly SharedAudioSystem    _audio         = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticCodexComponent, ExaminedEvent>(OnCodexExamined);
    }

    // ─── Proximity curse trigger ─────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toProcess = new List<(EntityUid Rune, EntityUid Tripper)>();

        var query = EntityQueryEnumerator<HereticCursedRuneComponent>();
        while (query.MoveNext(out var runeUid, out _))
        {
            foreach (var tripper in _lookup.GetEntitiesInRange(runeUid, 0.5f, LookupFlags.Dynamic))
            {
                if (HasComp<HereticComponent>(tripper))
                    continue;
                if (!HasComp<MobStateComponent>(tripper))
                    continue;

                toProcess.Add((runeUid, tripper));
                break;
            }
        }

        foreach (var (rune, tripper) in toProcess)
        {
            if (!HasComp<HereticCursedRuneComponent>(rune))
                continue;

            RemComp<HereticCursedRuneComponent>(rune);

            var dmg = new DamageSpecifier();
            dmg.DamageDict["Slash"]     = FixedPoint2.New(20);
            dmg.DamageDict["Bloodloss"] = FixedPoint2.New(15);
            _damage.TryChangeDamage(tripper, dmg, ignoreResistances: false);

            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_singlebeat.ogg"), rune);
            _popup.PopupEntity(Loc.GetString("heretic-codex-morbus-curse-triggered"), tripper, tripper, PopupType.LargeCaution);
        }
    }

    // ─── Hallucinations on examine ───────────────────────────────────────────

    private void OnCodexExamined(EntityUid uid, HereticCodexComponent comp, ExaminedEvent args)
    {
        if (!comp.CausesHallucinations || !comp.IsOpen)
            return;

        var examiner = args.Examiner;

        if (HasComp<HereticComponent>(examiner))
            return;
        if (!HasComp<MobStateComponent>(examiner))
            return;

        var now = _timing.CurTime;
        var cooldown = EnsureComp<HereticMorbusHallucinationCooldownComponent>(examiner);
        if (now < cooldown.NextHallucination)
            return;

        cooldown.NextHallucination = now + TimeSpan.FromSeconds(60);

        _statusEffects.TryAddStatusEffectDuration(examiner, "HereticWeeepingHallucinationStatusEffect", TimeSpan.FromSeconds(30));
        if (!EnsureComp<ParacusiaComponent>(examiner, out var paracusia))
        {
            _paracusia.SetSounds(examiner, new SoundCollectionSpecifier("Paracusia"), paracusia);
            _paracusia.SetTime(examiner, 5f, 30f, paracusia);
            _paracusia.SetDistance(examiner, 7f);
        }
        _popup.PopupEntity(Loc.GetString("heretic-codex-morbus-hallucination"), examiner, examiner, PopupType.LargeCaution);
    }
}
