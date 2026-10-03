using System.Linq;
using System.Text;
using Content.Server.Actions;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Decals;
using Content.Server.Doors.Systems;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Antag;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Decals;
using Content.Shared.Doors.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Follower.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Objectives.Systems;
using Content.Shared.Overlays;
using Content.Shared.PDA;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Speech.Muting;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Temperature.Components;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using CancellationTokenSource = System.Threading.CancellationTokenSource;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Именные цели еретика.
/// </summary>
public sealed partial class HereticSystem
{
    public bool IsNamedTarget(EntityUid bodyUid, EntityUid targetBodyUid)
    {
        if (!TryComp<HereticComponent>(bodyUid, out var comp))
            return false;
        return comp.NamedTargets.Contains(targetBodyUid);
    }

    public HereticPath GetCurrentPath(EntityUid bodyUid)
    {
        if (!TryComp<HereticComponent>(bodyUid, out var comp))
            return HereticPath.General;
        return comp.CurrentPath;
    }

    public void AssignNamedTargets(EntityUid bodyUid, EntityUid hereticMindId)
    {
        if (!TryComp<HereticComponent>(bodyUid, out var comp))
            return;

        // Determine the heretic's own primary department so we can pick one colleague as a target.
        string? hereticDeptId = null;
        if (_jobs.MindTryGetJob(hereticMindId, out var hereticJob) &&
            _jobs.TryGetPrimaryDepartment(hereticJob.ID, out var hereticDept))
        {
            hereticDeptId = hereticDept.ID;
        }

        // Bucket every other mind into job-based categories.
        var commandPool = new List<EntityUid>();
        var securityPool = new List<EntityUid>();
        var sameDeptPool = new List<EntityUid>();
        var otherPool = new List<EntityUid>();

        var mindQuery = EntityQueryEnumerator<MindComponent>();
        while (mindQuery.MoveNext(out var mindEnt, out var mindComp))
        {
            if (mindEnt == hereticMindId)
                continue;
            if (mindComp.CurrentEntity == null)
                continue;
            var bodyEnt = mindComp.CurrentEntity.Value;

            string? deptId = null;
            if (_jobs.MindTryGetJob(mindEnt, out var candidateJob) &&
                _jobs.TryGetPrimaryDepartment(candidateJob.ID, out var candidateDept))
            {
                deptId = candidateDept.ID;
            }

            if (deptId == "Command")
                commandPool.Add(bodyEnt);
            else if (deptId == "Security")
                securityPool.Add(bodyEnt);
            else if (hereticDeptId != null && deptId == hereticDeptId)
                sameDeptPool.Add(bodyEnt);
            else
                otherPool.Add(bodyEnt);
        }

        _random.Shuffle(commandPool);
        _random.Shuffle(securityPool);
        _random.Shuffle(sameDeptPool);
        _random.Shuffle(otherPool);

        var selected = new List<EntityUid>();

        // 1 command staff
        if (commandPool.Count > 0)
            selected.Add(commandPool[0]);

        // 1 security officer
        if (securityPool.Count > 0)
            selected.Add(securityPool[0]);

        // 1 crewmate from the heretic's own department
        if (sameDeptPool.Count > 0)
            selected.Add(sameDeptPool[0]);

        // Fill remaining slots (up to 5 total) from everyone else.
        var fallback = commandPool.Skip(selected.Contains(commandPool.Count > 0 ? commandPool[0] : EntityUid.Invalid) ? 1 : 0)
            .Concat(securityPool.Skip(selected.Contains(securityPool.Count > 0 ? securityPool[0] : EntityUid.Invalid) ? 1 : 0))
            .Concat(sameDeptPool.Skip(selected.Contains(sameDeptPool.Count > 0 ? sameDeptPool[0] : EntityUid.Invalid) ? 1 : 0))
            .Concat(otherPool)
            .Where(e => !selected.Contains(e))
            .ToList();
        _random.Shuffle(fallback);

        while (selected.Count < 5 && fallback.Count > 0)
        {
            selected.Add(fallback[0]);
            fallback.RemoveAt(0);
        }

        // Remove HUD marker from old targets before reassigning.
        foreach (var oldTarget in comp.NamedTargets)
        {
            if (!TryComp<HereticNamedTargetComponent>(oldTarget, out var oldMarker))
                continue;
            oldMarker.OwningHeretics.Remove(bodyUid);
            if (oldMarker.OwningHeretics.Count == 0)
                RemCompDeferred<HereticNamedTargetComponent>(oldTarget);
            else
                Dirty(oldTarget, oldMarker);
        }

        comp.NamedTargets = selected;

        // Add HUD marker to new targets.
        foreach (var target in comp.NamedTargets)
        {
            var marker = EnsureComp<HereticNamedTargetComponent>(target);
            if (!marker.OwningHeretics.Contains(bodyUid))
            {
                marker.OwningHeretics.Add(bodyUid);
                Dirty(target, marker);
            }
        }

        if (comp.NamedTargets.Count > 0)
        {
            var names = string.Join(", ", comp.NamedTargets.Select(t => MetaData(t).EntityName));
            _popup.PopupEntity(Loc.GetString("heretic-named-targets-assigned", ("names", names)), bodyUid, bodyUid, PopupType.LargeCaution);
            SendHereticMessage(bodyUid, Loc.GetString("heretic-named-targets-chat", ("names", names)));
        }

        Dirty(bodyUid, comp);
    }

    /// <summary>
    public bool IsNamedTarget(HereticComponent comp, EntityUid targetUid)
    {
        return comp.NamedTargets.Contains(targetUid);
    }

    /// <summary>
    /// Removes a single named target from the heretic's list, cleaning up HUD markers.
    /// </summary>
    public void RemoveNamedTarget(EntityUid bodyUid, HereticComponent comp, EntityUid targetUid)
    {
        if (!comp.NamedTargets.Remove(targetUid))
            return;

        if (TryComp<HereticNamedTargetComponent>(targetUid, out var marker))
        {
            marker.OwningHeretics.Remove(bodyUid);
            if (marker.OwningHeretics.Count == 0)
                RemCompDeferred<HereticNamedTargetComponent>(targetUid);
            else
                Dirty(targetUid, marker);
        }

        Dirty(bodyUid, comp);
    }

    /// <summary>
    /// Replaces the heretic's named targets with the given list, updating HUD markers.
    /// </summary>
    public void SetNamedTargets(EntityUid bodyUid, HereticComponent comp, List<EntityUid> targets)
    {
        foreach (var oldTarget in comp.NamedTargets)
        {
            if (!TryComp<HereticNamedTargetComponent>(oldTarget, out var oldMarker))
                continue;
            oldMarker.OwningHeretics.Remove(bodyUid);
            if (oldMarker.OwningHeretics.Count == 0)
                RemCompDeferred<HereticNamedTargetComponent>(oldTarget);
            else
                Dirty(oldTarget, oldMarker);
        }

        comp.NamedTargets = targets;

        foreach (var target in comp.NamedTargets)
        {
            var marker = EnsureComp<HereticNamedTargetComponent>(target);
            if (!marker.OwningHeretics.Contains(bodyUid))
            {
                marker.OwningHeretics.Add(bodyUid);
                Dirty(target, marker);
            }
        }

        Dirty(bodyUid, comp);
    }

    /// <summary>
    /// Opens the HeartbeatMansus target-tracking BUI for the heretic.
    /// </summary>
    public void OpenTargetBui(EntityUid uid, HereticComponent comp)
    {
        if (comp.BuiHolder == EntityUid.Invalid || !Exists(comp.BuiHolder))
            return;
        SendTargetBuiState(uid, comp);
        _ui.TryOpenUi(comp.BuiHolder, HereticTargetBuiKey.Key, uid);
    }

    private void OnTargetSelectedMessage(EntityUid holderUid, HereticKnowledgeHolderComponent holderComp, HereticTargetSelectedMessage args)
    {
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_singlebeat.ogg"), Filter.Broadcast(), false);
    }

    private void SendTargetBuiState(EntityUid uid, HereticComponent comp)
    {
        var targets = new List<HereticTargetData>();
        foreach (var targetUid in comp.NamedTargets)
        {
            if (!Exists(targetUid))
                continue;
            var isDead = TryComp<MobStateComponent>(targetUid, out var mobState) &&
                         mobState.CurrentState == MobState.Dead;
            targets.Add(new HereticTargetData
            {
                Entity = GetNetEntity(targetUid),
                Name = MetaData(targetUid).EntityName,
                IsDead = isDead,
            });
        }
        _ui.SetUiState(comp.BuiHolder, HereticTargetBuiKey.Key, new HereticTargetBuiState { Targets = targets });
    }

}
