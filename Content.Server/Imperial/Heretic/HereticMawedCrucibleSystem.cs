using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Eye;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Server.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticMawedCrucibleSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticMawedCrucibleComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<HereticMawedCrucibleComponent, AfterInteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<HereticMawedCrucibleComponent, HereticCrucibleSelectMessage>(OnPotionSelected);
        SubscribeLocalEvent<HereticCruciblePotionComponent, UseInHandEvent>(OnPotionUsed);
        SubscribeLocalEvent<HereticCruciblePotionComponent, HereticCruciblePotionDoAfterEvent>(OnPotionConsumed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HereticCrucibleEffectComponent>();
        while (query.MoveNext(out var uid, out var eff))
        {
            if (eff.ActivePotion == HereticCruciblePotionType.Marshal && now >= eff.NextHealTick)
            {
                DoMarshalHeal(uid, eff);
                eff.NextHealTick = now + TimeSpan.FromSeconds(1);
            }

            if (now >= eff.EndTime)
                RemoveCrucibleEffect(uid, eff);
        }
    }

    // ─── Interaction ──────────────────────────────────────────────────────────

    private void OnInteract(EntityUid uid, HereticMawedCrucibleComponent comp, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!HasComp<HereticComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("heretic-crucible-no-connection"), uid, args.User, PopupType.SmallCaution);
            return;
        }

        UpdateBuiState(uid, comp);
        _ui.TryOpenUi(uid, HereticMawedCrucibleUiKey.Key, args.User);
        args.Handled = true;
    }

    private void OnInteractUsing(EntityUid uid, HereticMawedCrucibleComponent comp, AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (!HasComp<OrganComponent>(args.Used))
            return;

        if (comp.Charges >= comp.MaxCharges)
        {
            _popup.PopupEntity(Loc.GetString("heretic-crucible-full"), uid, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        comp.Charges++;
        Dirty(uid, comp);
        Del(args.Used);
        _popup.PopupEntity(Loc.GetString("heretic-crucible-refilled", ("charges", comp.Charges), ("max", comp.MaxCharges)), uid, args.User, PopupType.Small);
        UpdateBuiState(uid, comp);
        args.Handled = true;
    }

    private void OnPotionSelected(EntityUid uid, HereticMawedCrucibleComponent comp, HereticCrucibleSelectMessage msg)
    {
        var player = msg.Actor;
        if (!HasComp<HereticComponent>(player))
            return;

        if (comp.Charges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-crucible-no-charges"), uid, player, PopupType.SmallCaution);
            return;
        }

        var now = _timing.CurTime;

        if (now < comp.CooldownEnd)
        {
            var remaining = (int)(comp.CooldownEnd - now).TotalSeconds + 1;
            _popup.PopupEntity(Loc.GetString("heretic-crucible-cooldown", ("seconds", remaining)), uid, player, PopupType.SmallCaution);
            return;
        }

        string potionEntity;

        switch (msg.Potion)
        {
            case HereticCruciblePotionType.Soul:
                // Cooldown is tracked on the player entity (HereticSoulCooldownComponent)
                if (TryComp<HereticSoulCooldownComponent>(player, out var cd) && now < cd.CooldownEnd)
                {
                    var remaining = (int)(cd.CooldownEnd - now).TotalSeconds + 1;
                    _popup.PopupEntity(Loc.GetString("heretic-crucible-cooldown-remaining", ("seconds", remaining)), uid, player, PopupType.SmallCaution);
                    return;
                }
                potionEntity = "HereticCrucibleSoulPotion";
                break;

            case HereticCruciblePotionType.Clarity:
                potionEntity = "HereticCrucibleClarityPotion";
                break;

            case HereticCruciblePotionType.Marshal:
                potionEntity = "HereticCrucibleMarshalPotion";
                break;

            default:
                return;
        }

        var potion = Spawn(potionEntity, Transform(uid).Coordinates);
        if (TryComp<HereticCruciblePotionComponent>(potion, out var potionComp))
        {
            potionComp.SourceCrucible = uid;
            potionComp.SoulCooldown = comp.SoulCooldown;
        }

        _hands.TryPickupAnyHand(player, potion);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/desecration-02.ogg"), uid);

        comp.Charges--;
        comp.CooldownEnd = now + TimeSpan.FromSeconds(60);
        Dirty(uid, comp);
        UpdateBuiState(uid, comp);
    }

    // ─── Potion: start drinking (UseInHand → DoAfter) ─────────────────────────

    private void OnPotionUsed(EntityUid uid, HereticCruciblePotionComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var player = args.User;
        if (!HasComp<HereticComponent>(player))
        {
            _popup.PopupEntity(Loc.GetString("heretic-crucible-no-connection"), uid, player, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            player,
            TimeSpan.FromSeconds(comp.DrinkDelay),
            new HereticCruciblePotionDoAfterEvent(),
            uid,
            used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
            args.Handled = true;
    }

    // ─── Potion: effect applied after drinking ────────────────────────────────

    private void OnPotionConsumed(EntityUid uid, HereticCruciblePotionComponent comp, HereticCruciblePotionDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        var player = args.User;
        if (!TryComp<HereticComponent>(player, out var heretic))
            return;

        // Re-check Soul cooldown in case it was still active when drinking completed
        if (comp.PotionType == HereticCruciblePotionType.Soul)
        {
            var now = _timing.CurTime;
            if (TryComp<HereticSoulCooldownComponent>(player, out var cdComp) && now < cdComp.CooldownEnd)
            {
                var remaining = (int)(cdComp.CooldownEnd - now).TotalSeconds + 1;
                _popup.PopupEntity(Loc.GetString("heretic-crucible-cooldown-remaining", ("seconds", remaining)), uid, player, PopupType.SmallCaution);
                args.Handled = true;
                return;
            }
        }

        float soulDuration = 60f;
        float clarityDuration = 60f;
        float marshalDuration = 60f;

        if (comp.SourceCrucible.HasValue &&
            TryComp<HereticMawedCrucibleComponent>(comp.SourceCrucible.Value, out var crucComp))
        {
            soulDuration = crucComp.SoulDuration;
            clarityDuration = crucComp.ClarityDuration;
            marshalDuration = crucComp.MarshalDuration;
        }

        switch (comp.PotionType)
        {
            case HereticCruciblePotionType.Soul:
                ApplySoulPotion(player, soulDuration, comp.SoulCooldown);
                break;
            case HereticCruciblePotionType.Clarity:
                ApplyClarityPotion(player, clarityDuration);
                break;
            case HereticCruciblePotionType.Marshal:
                ApplyMarshalPotion(player, marshalDuration);
                break;
            default:
                return;
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Chemistry/bubbles.ogg"), player);
        Del(uid);
        args.Handled = true;
    }

    // ─── Soul Potion ──────────────────────────────────────────────────────────

    private void ApplySoulPotion(EntityUid playerUid, float duration, float soulCooldown)
    {
        if (!TryComp<PhysicsComponent>(playerUid, out var physics) ||
            !TryComp<FixturesComponent>(playerUid, out var fixtures))
            return;

        var eff = EnsureComp<HereticCrucibleEffectComponent>(playerUid);
        eff.ActivePotion = HereticCruciblePotionType.Soul;
        eff.EndTime = _timing.CurTime + TimeSpan.FromSeconds(duration);
        eff.OriginalPosition = Transform(playerUid).Coordinates;
        eff.SoulCooldown = soulCooldown;
        eff.FixtureStates.Clear();

        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            eff.FixtureStates.Add((id, fixture.Hard, fixture.CollisionLayer, fixture.CollisionMask));
            _physics.SetHard(playerUid, fixture, false, fixtures);
            _physics.SetCollisionLayer(playerUid, id, fixture, (int) CollisionGroup.None, fixtures, physics);
            _physics.SetCollisionMask(playerUid, id, fixture, (int) CollisionGroup.None, fixtures, physics);
        }

        _popup.PopupEntity(Loc.GetString("heretic-crucible-soul-start"), playerUid, playerUid, PopupType.Medium);
    }

    // ─── Clarity Potion ───────────────────────────────────────────────────────

    private void ApplyClarityPotion(EntityUid playerUid, float duration)
    {
        if (!TryComp<EyeComponent>(playerUid, out var eye))
            return;

        var eff = EnsureComp<HereticCrucibleEffectComponent>(playerUid);
        eff.ActivePotion = HereticCruciblePotionType.Clarity;
        eff.EndTime = _timing.CurTime + TimeSpan.FromSeconds(duration);
        eff.OriginalFov = eye.DrawFov;

        _eye.SetDrawFov(playerUid, false, eye);

        _popup.PopupEntity(Loc.GetString("heretic-crucible-clarity-start"), playerUid, playerUid, PopupType.Medium);
    }

    // ─── Marshal Potion ───────────────────────────────────────────────────────

    private void ApplyMarshalPotion(EntityUid playerUid, float duration)
    {
        var eff = EnsureComp<HereticCrucibleEffectComponent>(playerUid);
        eff.ActivePotion = HereticCruciblePotionType.Marshal;
        eff.EndTime = _timing.CurTime + TimeSpan.FromSeconds(duration);
        eff.NextHealTick = _timing.CurTime + TimeSpan.FromSeconds(1);

        EnsureComp<IgnoreSlowOnDamageComponent>(playerUid);

        _popup.PopupEntity(Loc.GetString("heretic-crucible-marshal-start"), playerUid, playerUid, PopupType.Medium);
    }

    private void DoMarshalHeal(EntityUid uid, HereticCrucibleEffectComponent eff)
    {
        var healRate = GetMarshalHealRate(uid);
        if (healRate <= 0f)
            return;

        var amount = FixedPoint2.New(healRate);
        var heal = new DamageSpecifier
        {
            DamageDict =
            {
                ["Blunt"] = -amount,
                ["Slash"] = -amount,
                ["Piercing"] = -amount,
            }
        };
        _damageable.TryChangeDamage(uid, heal, ignoreResistances: true);

        if (TryComp<BloodstreamComponent>(uid, out var blood))
            _bloodstream.TryModifyBloodLevel((uid, blood), amount);
    }

    private float GetMarshalHealRate(EntityUid uid)
    {
        if (!TryComp<MobStateComponent>(uid, out var mobState))
            return 0f;

        if (mobState.CurrentState == MobState.Critical)
            return 6f;

        if (!_mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var critThreshold))
            return 1f;

        var totalDamage = _damageable.GetTotalDamage(uid);
        if (totalDamage >= critThreshold * 0.5)
            return 3f;

        return 1f;
    }

    // ─── Effect removal ───────────────────────────────────────────────────────

    private void RemoveCrucibleEffect(EntityUid uid, HereticCrucibleEffectComponent eff)
    {
        switch (eff.ActivePotion)
        {
            case HereticCruciblePotionType.Soul:
                RestoreSoulEffect(uid, eff);
                break;

            case HereticCruciblePotionType.Clarity:
                if (TryComp<EyeComponent>(uid, out var eye))
                    _eye.SetDrawFov(uid, eff.OriginalFov, eye);
                break;

            case HereticCruciblePotionType.Marshal:
                RemCompDeferred<IgnoreSlowOnDamageComponent>(uid);
                _popup.PopupEntity(Loc.GetString("heretic-crucible-marshal-end"), uid, uid, PopupType.Small);
                break;
        }

        RemCompDeferred<HereticCrucibleEffectComponent>(uid);
    }

    private void RestoreSoulEffect(EntityUid uid, HereticCrucibleEffectComponent eff)
    {
        if (TryComp<PhysicsComponent>(uid, out var physics) && TryComp<FixturesComponent>(uid, out var fixtures))
        {
            foreach (var (id, hard, layer, mask) in eff.FixtureStates)
            {
                if (!fixtures.Fixtures.TryGetValue(id, out var fixture))
                    continue;
                _physics.SetHard(uid, fixture, hard, fixtures);
                _physics.SetCollisionLayer(uid, id, fixture, layer, fixtures, physics);
                _physics.SetCollisionMask(uid, id, fixture, mask, fixtures, physics);
            }
        }

        if (eff.OriginalPosition.HasValue)
        {
            _xform.SetCoordinates(uid, eff.OriginalPosition.Value);
            _popup.PopupEntity(Loc.GetString("heretic-crucible-soul-end"), uid, uid, PopupType.Small);
        }

        // Apply cooldown AFTER effect ends — player cannot create another Soul potion for SoulCooldown seconds
        var soulCd = EnsureComp<HereticSoulCooldownComponent>(uid);
        soulCd.CooldownEnd = _timing.CurTime + TimeSpan.FromSeconds(eff.SoulCooldown);
    }

    // ─── BUI state ────────────────────────────────────────────────────────────

    private void UpdateBuiState(EntityUid uid, HereticMawedCrucibleComponent comp)
    {
        _ui.SetUiState(uid, HereticMawedCrucibleUiKey.Key,
            new HereticCrucibleBuiState(comp.Charges, comp.MaxCharges));
    }
}
