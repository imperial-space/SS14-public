using System.Linq;
using Content.Server.Imperial.Heretic.Objectives;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Изучение знаний и их пассивные эффекты.
/// </summary>
public sealed partial class HereticSystem
{
    private static readonly ProtoId<DamageTypePrototype> CellularDamageType = "Cellular";

    private static readonly EntProtoId HereticSpaceHands = "HereticSpaceHands";

    public bool TryResearchKnowledge(EntityUid uid, HereticComponent comp, string knowledgeId)
    {
        if (!_proto.TryIndex<HereticKnowledgePrototype>(knowledgeId, out var proto))
            return false;

        if (comp.ResearchedKnowledge.Contains(knowledgeId))
        {
            _popup.PopupEntity(Loc.GetString("heretic-knowledge-already-known"), uid, uid);
            return false;
        }

        // Conflicts check: block if any mutually-exclusive node is already researched
        foreach (var conflict in proto.ConflictsWith)
        {
            if (comp.ResearchedKnowledge.Contains(conflict.Id))
            {
                _popup.PopupEntity(Loc.GetString("heretic-knowledge-already-known"), uid, uid);
                return false;
            }
        }

        var isGift = comp.PendingGiftGroups.Any(g => g.Candidates.Contains(knowledgeId));
        var effectiveCost = isGift ? 0 : proto.Cost;

        if (!isGift && comp.CurrentPath == HereticPath.Lock && comp.PassiveLevel >= 1 && proto.Path == HereticPath.General)
            effectiveCost = Math.Max(0, effectiveCost - 1);

        if (comp.KnowledgePoints < effectiveCost)
        {
            _popup.PopupEntity(Loc.GetString("heretic-knowledge-no-points"), uid, uid);
            return false;
        }

        // Prerequisites check
        if (!ArePrerequisitesMet(comp, proto))
        {
            _popup.PopupEntity(Loc.GetString("heretic-knowledge-prereq-missing"), uid, uid);
            return false;
        }

        // Shop level check: General shop nodes require sufficient ShopLevel (gifts bypass this)
        if (!isGift && proto.ShopLevel > 0 && comp.ShopLevel < proto.ShopLevel)
        {
            _popup.PopupEntity(Loc.GetString("heretic-knowledge-prereq-missing"), uid, uid);
            return false;
        }

        // Path lock check: if heretic already has a path, only allow General or same path
        if (proto.SetsPath && comp.CurrentPath != HereticPath.General && proto.Path != comp.CurrentPath)
        {
            _popup.PopupEntity(Loc.GetString("heretic-knowledge-wrong-path"), uid, uid);
            return false;
        }

        // Ascension gate: all non-ascension objectives must be complete first
        if (knowledgeId.StartsWith("KnowledgeAscension") && !comp.AscensionBypass)
        {
            if (_mind.TryGetMind(uid, out var mindId, out var mind))
            {
                foreach (var obj in mind.Objectives)
                {
                    if (HasComp<HereticAscensionConditionComponent>(obj))
                        continue;
                    if (!_objectives.IsCompleted(obj, (mindId, mind)))
                    {
                        _popup.PopupEntity(Loc.GetString("heretic-ascension-goals-incomplete"), uid, uid, PopupType.MediumCaution);
                        return false;
                    }
                }
            }
        }

        comp.KnowledgePoints -= effectiveCost;
        comp.ResearchedKnowledge.Add(knowledgeId);
        comp.TotalShopCostLearned += proto.Cost;
        if (comp.TotalShopCostLearned >= 8)
            ActivateHereticAura(uid, comp);

        if (isGift)
            comp.PendingGiftGroups.RemoveAll(g => g.Candidates.Contains(knowledgeId));

        if (proto.SetsPath && comp.CurrentPath == HereticPath.General)
            comp.CurrentPath = proto.Path;

        GrantKnowledgeActions(uid, comp, proto);
        ApplyPassiveKnowledgeEffect(uid, comp, knowledgeId);
        // Blades are obtained exclusively via ritual crafting (RitualBlade*Craft/Upgrade) — no auto-spawn on knowledge research.

        // Advance shop level when a path-specific node is researched (blade upgrades are excluded)
        if (proto.Path != HereticPath.General && !proto.SetsPath && proto.AdvancesShopLevel && comp.ShopLevel < 5)
        {
            comp.ShopLevel = Math.Min(comp.ShopLevel + 1, 5);
            var candidates = new List<string>();
            foreach (var shopProto in _proto.EnumeratePrototypes<HereticKnowledgePrototype>())
            {
                if (shopProto.ShopLevel == comp.ShopLevel && !comp.ResearchedKnowledge.Contains(shopProto.ID))
                    candidates.Add(shopProto.ID);
            }
            _random.Shuffle(candidates);
            var giftCandidates = new List<string>();
            for (var i = 0; i < Math.Min(3, candidates.Count); i++)
                giftCandidates.Add(candidates[i]);
            if (giftCandidates.Count > 0)
                comp.PendingGiftGroups.Add(new HereticGiftGroup { SourceNodeId = knowledgeId, Candidates = giftCandidates });
        }

        Dirty(uid, comp);
        SendInfoBuiState(uid, comp);

        _popup.PopupEntity(Loc.GetString("heretic-knowledge-learned", ("name", Loc.GetString(proto.Name))), uid, uid, PopupType.Medium);
        SendHereticMessage(uid, Loc.GetString("heretic-knowledge-gained-chat", ("name", Loc.GetString(proto.Name))));
        return true;
    }

    /// <summary>
    /// All base <see cref="HereticKnowledgePrototype.Prerequisites"/> must be researched, AND
    /// (if any <see cref="HereticKnowledgePrototype.PrerequisitesAny"/> sets are defined) at least
    /// one of those alternative sets must be fully researched.
    /// </summary>
    private static bool ArePrerequisitesMet(HereticComponent comp, HereticKnowledgePrototype proto)
    {
        if (!proto.Prerequisites.All(p => comp.ResearchedKnowledge.Contains(p)))
            return false;

        if (proto.PrerequisitesAny.Count == 0)
            return true;

        return proto.PrerequisitesAny.Any(set => set.All(p => comp.ResearchedKnowledge.Contains(p)));
    }

    private void GrantKnowledge(EntityUid uid, HereticComponent comp, string knowledgeId)
    {
        if (!_proto.TryIndex<HereticKnowledgePrototype>(knowledgeId, out var proto))
            return;

        if (comp.ResearchedKnowledge.Contains(knowledgeId))
            return;

        comp.ResearchedKnowledge.Add(knowledgeId);
        GrantKnowledgeActions(uid, comp, proto);
    }

    public void GrantMarkKnowledge(EntityUid uid, HereticComponent comp, string knowledgeId)
        => GrantKnowledge(uid, comp, knowledgeId);

    private void GrantKnowledgeActions(EntityUid uid, HereticComponent comp, HereticKnowledgePrototype proto)
    {
        foreach (var actionProtoId in proto.GrantActions)
        {
            EntityUid? actionEnt = null;
            _actions.AddAction(uid, ref actionEnt, actionProtoId);
            if (actionEnt.HasValue)
                comp.GrantedActions.Add(actionEnt.Value);
        }
    }

    private void ApplyPassiveKnowledgeEffect(EntityUid uid, HereticComponent comp, string knowledgeId)
    {
        DamageSpecifier? heal = null;
        string? popupKey = null;

        switch (knowledgeId)
        {
            case "KnowledgeBreakOfDawn":
                break; // Items are spawned directly in MakeHeretic

            case "KnowledgeLockwielder":
                {
                    heal = new DamageSpecifier();
                    heal.DamageDict[BluntDamageType] = FixedPoint2.New(-20);
                    heal.DamageDict[SlashDamageType] = FixedPoint2.New(-10);
                    break;
                }

            case "KnowledgeFleshArtisan":
                heal = new DamageSpecifier();
                heal.DamageDict[SlashDamageType] = FixedPoint2.New(-25);
                heal.DamageDict[PiercingDamageType] = FixedPoint2.New(-15);
                popupKey = "heretic-passive-flesh-artisan";
                break;

            case "KnowledgeVoidTraveler":
                heal = new DamageSpecifier();
                heal.DamageDict[ColdDamageType] = FixedPoint2.New(-15);
                heal.DamageDict[CellularDamageType] = FixedPoint2.New(-10);
                popupKey = "heretic-passive-void-traveler";
                break;

            case "KnowledgeBladeAdept":
                heal = new DamageSpecifier();
                heal.DamageDict[SlashDamageType] = FixedPoint2.New(-20);
                heal.DamageDict[BluntDamageType] = FixedPoint2.New(-10);
                popupKey = "heretic-passive-blade-adept";
                break;

            case "KnowledgeRustReaper":
                {
                    heal = new DamageSpecifier();
                    heal.DamageDict[CausticDamageType] = FixedPoint2.New(-15);
                    heal.DamageDict[SlashDamageType] = FixedPoint2.New(-10);
                    break;
                }

            case "KnowledgeCosmicAcolyte":
                {
                    heal = new DamageSpecifier();
                    heal.DamageDict[RadiationDamageType] = FixedPoint2.New(-15);
                    heal.DamageDict[HeatDamageType] = FixedPoint2.New(-10);
                    var coords = Transform(uid).Coordinates;
                    var spaceHands = Spawn(HereticSpaceHands, coords);
                    _hands.TryPickupAnyHand(uid, spaceHands);
                    popupKey = "heretic-space-hands-obtained";
                    break;
                }

            case "KnowledgeGraspOfLunacy":
                {
                    popupKey = "heretic-passive-grasp-of-lunacy";
                    break;
                }

            case "KnowledgeVolcanoBlast":
                {
                    if (TryComp<FlammableComponent>(uid, out var flammable))
                        flammable.MaximumFireStacks = 0f;
                    popupKey = "heretic-passive-volcano-blast";
                    break;
                }

            // ── Blade upgrades ───────────────────────────────────────────────
            case "KnowledgeFieryBlade":
            case "KnowledgeCravingBlade":
            case "KnowledgeBleedingSteel":
            case "KnowledgeToxicBlade":
            case "KnowledgeCosmicBlade":
            case "KnowledgeMoonBlade":
                ApplyBladeUpgrade(uid, comp);
                comp.BladeUpgraded = true;
                Dirty(uid, comp);
                popupKey = "heretic-blade-upgrade-ritual";
                break;

            case "KnowledgeEmpoweredBlades":
                ApplyBladeUpgrade(uid, comp);
                comp.BladeUpgraded = true;
                Dirty(uid, comp);
                popupKey = "heretic-passive-empowered-blades-learned";
                break;

            case "KnowledgeMawedCrucible":
            case "KnowledgeShopMawedCrucible":
                popupKey = "heretic-mawed-crucible-unlocked";
                break;

            case "KnowledgeFuriousSteel":
                popupKey = "heretic-passive-furious-steel";
                break;

            case "KnowledgeCallOfMoon":
                EnsureComp<HereticMoonBrainDamageComponent>(uid);
                popupKey = "heretic-passive-call-of-moon";
                break;

        }

        if (heal != null)
            _damageSystem.TryChangeDamage(uid, heal, ignoreResistances: true);

        if (popupKey != null)
            _popup.PopupEntity(Loc.GetString(popupKey), uid, uid, PopupType.Medium);
    }

}
