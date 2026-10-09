using Content.Server.Imperial.Cult.Components;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Follower.Components;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult;

public sealed partial class CultSystem
{
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;

    private static readonly EntProtoId SpiritCommunionAction = "ActionCultSpiritCommunion";
    private static readonly EntProtoId GhostMarkAction = "ActionCultGhostMark";

    private int _narsieSummonCount;

    private void InitializeMisc()
    {
        SubscribeLocalEvent<CultDarkSpiritComponent, CultSpiritCommunionActionEvent>(OnSpiritCommunion);
        SubscribeLocalEvent<CultDarkSpiritComponent, CultGhostMarkActionEvent>(OnGhostMark);
        SubscribeLocalEvent<CultDarkSpiritComponent, CultDarkSpiritCommuneActionEvent>(OnLegacySpiritCommune);
        SubscribeLocalEvent<Content.Shared.Mindshield.Components.MindShieldComponent, ComponentStartup>(OnMindShield);
    }

    /// <summary>datum/antagonist/cult/on_mindshield(): культ не снимается.</summary>
    private void OnMindShield(Entity<Content.Shared.Mindshield.Components.MindShieldComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<CultistComponent>(ent, out var cultist) && !cultist.Silent)
            Message(ent, Loc.GetString("cult-mindshield-resist"));
    }

    public void SetName(EntityUid uid, string name) => _meta.SetEntityName(uid, name);

    public void MessageGhosts(Filter filter, string text)
    {
        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Server, text, text, EntityUid.Invalid, false, true, CultRed);
    }

    /// <summary>priority_announce от «Отдела паранормальных явлений».</summary>
    public void Announce(string text, SoundSpecifier? sound)
    {
        _chat.DispatchGlobalAnnouncement(text, Loc.GetString("cult-paranormal-sender"), sound != null, sound, Color.FromHex("#d94b4b"));
    }

    /// <summary>health = maxHealth - урон (maxHealth — порог крита).</summary>
    public float HealthOf(EntityUid uid, DamageableComponent damageable)
    {
        var max = _thresholds.TryGetThresholdForState(uid, MobState.Critical, out var crit) ? crit.Value.Float() : 100f;
        return max - TotalDamage(uid);
    }

    /// <summary>started_narsie_summon / failed_narsie_summon: счётчик черчения руны Нар'Си.</summary>
    public void NarSieSummonStarted()
    {
        _narsieSummonCount++;
        if (GetRule() is { } rule && !rule.Comp.RitualMusicPlayed)
        {
            rule.Comp.RitualMusicPlayed = true;
            RaiseNetworkEvent(new CultRitualMusicEvent());
        }
        if (_narsieSummonCount == 1)
            RaiseNetworkEvent(new CultStarlightEvent(true));
    }

    public void NarSieSummonFailed()
    {
        _narsieSummonCount = Math.Max(0, _narsieSummonCount - 1);
        if (_narsieSummonCount <= 1)
            RaiseNetworkEvent(new CultStarlightEvent(false));
    }

    public int NarSieSummonCount => _narsieSummonCount;

    /// <summary>spiral_range_turfs(1, cultist): кольцо вокруг (для щитов можно и на занятые тайлы).</summary>
    public List<EntityCoordinates> FreeTurfsAround(EntityUid uid, bool includeBlocked)
    {
        if (!includeBlocked)
            return FreeTurfsAround(uid);

        var result = new List<EntityCoordinates>();
        var xform = Transform(uid);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return result;
        var center = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;
                var idx = center + new Vector2i(x, y);
                if (!_map.TryGetTileRef(grid, gridComp, idx, out var tile) || tile.Tile.IsEmpty)
                    continue;
                result.Add(_map.GridTileToLocal(grid, gridComp, idx));
            }
        }
        return result;
    }

    #region Тёмный дух

    public void GrantDarkSpiritActions(EntityUid ghost, CultDarkSpiritComponent spirit)
    {
        EntityUid? comm = null;
        EntityUid? mark = null;
        _actions.AddAction(ghost, ref comm, SpiritCommunionAction);
        _actions.AddAction(ghost, ref mark, GhostMarkAction);
        if (comm != null)
            spirit.Actions.Add(comm.Value);
        if (mark != null)
            spirit.Actions.Add(mark.Value);
    }

    public void RemoveDarkSpiritActions(EntityUid ghost, CultDarkSpiritComponent spirit)
    {
        foreach (var action in spirit.Actions)
            _actions.RemoveAction(ghost, action);
        spirit.Actions.Clear();
        RemComp<CultDarkSpiritComponent>(ghost);
    }

    private void OnLegacySpiritCommune(Entity<CultDarkSpiritComponent> ent, ref CultDarkSpiritCommuneActionEvent args)
    {
        args.Handled = true;
        OpenSpiritCommunion(ent);
    }

    private void OnSpiritCommunion(Entity<CultDarkSpiritComponent> ent, ref CultSpiritCommunionActionEvent args)
    {
        args.Handled = true;
        OpenSpiritCommunion(ent);
    }

    /// <summary>comm/spirit: «The [name]: message» всем культистам и мёртвым.</summary>
    private void OpenSpiritCommunion(EntityUid ghost)
    {
        OpenText(ghost, Loc.GetString("cult-communion-title"), Loc.GetString("cult-communion-prompt"), 512, message =>
        {
            if (!HasComp<CultDarkSpiritComponent>(ghost))
                return;
            var text = Loc.GetString("cult-spirit-communion-message", ("name", Name(ghost)),
                ("message", Robust.Shared.Utility.FormattedMessage.EscapeText(message)));
            MessageCult(text, ghosts: true);
        });
    }

    /// <summary>ghostmark: метит то, за чем следует дух, или его тайл; 60 с.</summary>
    private void OnGhostMark(Entity<CultDarkSpiritComponent> ent, ref CultGhostMarkActionEvent args)
    {
        args.Handled = true;
        var rule = _rule.EnsureRule();
        var now = _timing.CurTime;

        if (rule.Comp.BloodTarget != null)
        {
            if (ent.Comp.NextMark > now)
            {
                UnsetBloodTarget();
                Message(ent, Loc.GetString("cult-ghostmark-reset"));
            }
            else
            {
                Message(ent, Loc.GetString("cult-mark-already"));
            }
            return;
        }

        if (ent.Comp.NextMark > now)
        {
            Message(ent, Loc.GetString("cult-ghostmark-not-ready"));
            return;
        }

        EntityUid target;
        if (TryComp<FollowerComponent>(ent, out var follower))
        {
            target = follower.Following;
        }
        else
        {
            target = Spawn("CultBloodMarkMarker", _transform.GetMapCoordinates(ent));
            rule.Comp.BloodTargetMarker = target;
        }

        if (SetBloodTarget(target, ent, TimeSpan.FromSeconds(60)))
        {
            Message(ent, Loc.GetString("cult-ghostmark-set", ("target", target)));
            ent.Comp.NextMark = now + TimeSpan.FromSeconds(60);
        }
    }

    #endregion

    /// <summary>Удалить истёкшие видения Апокалипсиса, кровавые искры Галлюцинаций и маркер метки.</summary>
    private void UpdateVisions(TimeSpan now)
    {
        var expired = new List<EntityUid>();
        var visions = EntityQueryEnumerator<CultApocalypseVisionComponent>();
        while (visions.MoveNext(out var uid, out var vision))
        {
            if (now >= vision.End)
                expired.Add(uid);
        }
        foreach (var uid in expired)
            RemComp<CultApocalypseVisionComponent>(uid);

        expired.Clear();
        var horror = EntityQueryEnumerator<CultHorrorMarkComponent>();
        while (horror.MoveNext(out var uid, out var mark))
        {
            if (now >= mark.End)
                expired.Add(uid);
        }
        foreach (var uid in expired)
            RemComp<CultHorrorMarkComponent>(uid);

        if (_rule.TryGetRule(out var rule) && rule.Value.Comp.BloodTargetMarker is { } marker
            && rule.Value.Comp.BloodTarget != marker)
        {
            rule.Value.Comp.BloodTargetMarker = null;
            if (!TerminatingOrDeleted(marker))
                QueueDel(marker);
        }
    }
}
