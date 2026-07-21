using System.Linq;

using Content.Server.Chat.Systems;
using Content.Server.Imperial.Medieval.Factions;
using Content.Server.Imperial.Medieval.TempInvincibility;
using Content.Server.Imperial.Medieval.Achievements;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.CommsCharger;
using Content.Shared.Imperial.Medieval.ObeliskDestroyable;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Achievements;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.ObeliskDestroyable;

public sealed class ObeliskDestroyableSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MedievalFactionLateJoinLockSystem _lateJoinLock = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly TempInvincibilitySystem _tempInvincibility = default!;
    [Dependency] private readonly AchievementSystem _achievement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ObeliskDestroyableComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<ObeliskDestroyableComponent, TempInvincibilityEndedEvent>(OnTempInvincibilityEnded);
    }

    private void OnDamageChanged(EntityUid uid, ObeliskDestroyableComponent component, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        var damageable = args.Damageable;

        if (!TryGetCurrentPhase(component, out var phase, out var phaseIndex))
        {
            return;
        }

        var requiredDamage = GetCumulativeThreshold(component, phaseIndex + 1);
        if (damageable.TotalDamage < requiredDamage)
            return;

        SendAnnouncement(component, phase);

        if (phase.DestroyOnReached)
        {
            var trueOrigin = _achievement.GetPlayerFromOrigin(args.Origin); // xd
            
            if (trueOrigin != null && TryComp<MedievalFactionMemberComponent>(trueOrigin, out var trueOriginFaction))
            {
                var query = EntityQueryEnumerator<MedievalFactionMemberComponent, ActorComponent>();
                while (query.MoveNext(out var playerUid, out var member, out var _))
                {
                    if (member.Faction != trueOriginFaction.Faction)
                        continue;

                    _achievement.TryUpdateProgressAndGrant(playerUid,
                        new DestroyFactionObeliskContext(component.Faction),
                        ach => ach.Conditions.Any(c => c is DestroyFactionObeliskCondition));
                }
            }

            DestroyObelisk(uid, component);
            return;
        }

        component.CurrentPhase++;
        SetTotalDamage(uid, damageable, requiredDamage);
        _tempInvincibility.StartTempInvincibility(uid, component.InvincibilityDuration);
        component.InvincibilityActive = true;
        Dirty(uid, component);
    }

    public void ResetObelisk(EntityUid uid, ObeliskDestroyableComponent component, DamageableComponent? damageable = null)
    {
        if (!TryComp(uid, out damageable))
            return;

        component.CurrentPhase = 0;
        component.InvincibilityActive = false;
        _tempInvincibility.EndTempInvincibilityEarly(uid);

        _damageable.SetAllDamage(uid, damageable, FixedPoint2.Zero);
        Dirty(uid, component);
    }

    private void OnTempInvincibilityEnded(EntityUid uid, ObeliskDestroyableComponent component, TempInvincibilityEndedEvent args)
    {
        component.InvincibilityActive = false;
        Dirty(uid, component);
    }

    private void DestroyObelisk(EntityUid uid, ObeliskDestroyableComponent component)
    {
        if (component.DestroyedEffect is { } effect)
            Spawn(effect, Transform(uid).Coordinates);

        _metaData.SetEntityDescription(uid, Loc.GetString(component.DestroyedDescription));
        _lateJoinLock.LockDepartment(component.LockedDepartment);
        RemComp<CommsChargerComponent>(uid);
        RemComp<ObeliskDestroyableComponent>(uid);
    }

    private void SetTotalDamage(EntityUid uid, DamageableComponent damageable, FixedPoint2 targetDamage)
    {
        var currentDamage = new DamageSpecifier(damageable.Damage);
        var overflow = currentDamage.GetTotal() - targetDamage;
        if (overflow <= FixedPoint2.Zero)
            return;

        var damageTypes = new List<string>(currentDamage.DamageDict.Keys);
        foreach (var damageType in damageTypes)
        {
            if (!currentDamage.DamageDict.TryGetValue(damageType, out var amount) ||
                amount <= FixedPoint2.Zero)
            {
                continue;
            }

            var reduction = amount < overflow ? amount : overflow;
            currentDamage.DamageDict[damageType] -= reduction;
            overflow -= reduction;

            if (overflow <= FixedPoint2.Zero)
            {
                break;
            }
        }

        _damageable.SetDamage(uid, damageable, currentDamage);
    }

    private bool TryGetCurrentPhase(ObeliskDestroyableComponent component, out ObeliskDestroyablePhaseData phase, out int phaseIndex)
    {
        if (component.CurrentPhase < 0 || component.CurrentPhase >= component.Phases.Count)
        {
            phase = default!;
            phaseIndex = -1;
            return false;
        }

        phaseIndex = component.CurrentPhase;
        phase = component.Phases[phaseIndex];
        return true;
    }

    private FixedPoint2 GetCumulativeThreshold(ObeliskDestroyableComponent component, int phaseCount)
    {
        var total = FixedPoint2.Zero;
        for (var i = 0; i < phaseCount && i < component.Phases.Count; i++)
        {
            total += component.Phases[i].Threshold;
        }

        return total;
    }

    private void SendAnnouncement(ObeliskDestroyableComponent component, ObeliskDestroyablePhaseData phase)
    {
        var sender = Loc.TryGetString(component.AnnouncementSender, out var localizedSender)
            ? localizedSender
            : component.AnnouncementSender.ToString();

        var announcementId = string.IsNullOrWhiteSpace(component.AnnouncementLocPrefix)
            ? phase.Announcement.ToString()
            : $"{component.AnnouncementLocPrefix}-{phase.Announcement}";

        var message = Loc.GetString(announcementId);

        _chat.DispatchGlobalAnnouncement(
            message,
            sender,
            playSound: true,
            announcementSound: component.AnnouncementSound,
            colorOverride: component.AnnouncementColor);
    }
}
