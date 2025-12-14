using System.Linq;
using Content.Shared.DetailExaminable;
using Content.Server.MedievalPasport.Components;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Content.Shared.Salvage.Expeditions;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Factions;

public sealed partial class MedievalFactionsSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public readonly Dictionary<int, WantedData> WantedList = new();

    private void InitializeWanted()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestartCleanup);

        SubscribeLocalEvent<WantedDeskComponent, MapInitEvent>(OnDeskInit);
    }

    private void OnRestartCleanup(RoundRestartCleanupEvent args)
    {
        WantedList.Clear();
    }

    private void OnDeskInit(EntityUid uid, WantedDeskComponent comp, MapInitEvent args)
    {
        UpdateUi(uid);
    }

    public void AddWanted(EntityUid uid, string job, string performer, string details, ProtoId<MedievalFactionPrototype> proto)
    {
        if (!TryComp<MedievalFactionMemberComponent>(uid, out var friends))
            return;
        if (WantedList.ContainsKey(friends.MemberID))
            return;
        if (Proto.TryIndex(proto, out var factProto) && factProto.WantedText != null)
        {
            friends.Wanted = new(proto, factProto.WantedText);
            Dirty(uid, friends);
        }

        var profile = BuildProfile(uid);
        if (profile == null)
            return;

        string flavorText = "";
        if (TryComp<DetailExaminableComponent>(uid, out var detailExaminable))
            flavorText = detailExaminable.Content;

        var wanted = new WantedData(profile, job, proto, performer, flavorText, details);
        WantedList.Add(friends.MemberID, wanted);
        UpdateUi();

        _action.RemoveAction(uid, friends.FactionMenuActionEntity);
        _ui.CloseUis(uid);
    }

    private HumanoidCharacterProfile? BuildProfile(EntityUid uid)
    {
        if (!TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            return null;

        HumanoidCharacterAppearance hca = new();

        if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.FacialHair, out var facialHair))
            if (facialHair.TryGetValue(0, out var marking))
            {
                hca = hca.WithFacialHairStyleName(marking.MarkingId);
                hca = hca.WithFacialHairColor(marking.MarkingColors.First());
            }
        if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Hair, out var hair))
            if (hair.TryGetValue(0, out var marking))
            {
                hca = hca.WithHairStyleName(marking.MarkingId);
                hca = hca.WithHairColor(marking.MarkingColors.First());
            }
        if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Head, out var head))
            hca = hca.WithMarkings(head);
        if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.HeadSide, out var headSide))
            hca = hca.WithMarkings(headSide);
        if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.HeadTop, out var headTop))
            hca = hca.WithMarkings(headTop);
        if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Snout, out var snout))
            hca = hca.WithMarkings(snout);
        if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Chest, out var chest))
            hca = hca.WithMarkings(chest);
        if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Arms, out var arms))
            hca = hca.WithMarkings(arms);
        if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Legs, out var legs))
            hca = hca.WithMarkings(legs);
        if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Tail, out var tail))
            hca = hca.WithMarkings(tail);
        if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Overlay, out var overlay))
            hca = hca.WithMarkings(overlay);

        hca = hca.WithSkinColor(humanoid.SkinColor);
        hca = hca.WithEyeColor(humanoid.EyeColor);

        return new HumanoidCharacterProfile().
                WithCharacterAppearance(hca).
                WithSpecies(humanoid.Species).
                WithSex(humanoid.Sex).
                WithAge(humanoid.Age).
                WithName(Name(uid));
    }

    public void RemoveWanted(EntityUid uid)
    {
        if (!TryComp<MedievalFactionMemberComponent>(uid, out var friends))
            return;
        if (!WantedList.ContainsKey(friends.MemberID))
            return;

        WantedList.Remove(friends.MemberID);
        UpdateUi();
    }

    public void UpdateUi()
    {
        var state = new WantedDeskBoundUserInterfaceState(WantedList);
        var query = EntityQueryEnumerator<WantedDeskComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _ui.SetUiState(uid, WantedDeskUiKey.Key, state);
            _appearance.SetData(uid, WantedDeskVisuals.Appearance, WantedList.Count switch { <= 0 => WantedDeskVisualState.None, < 3 => WantedDeskVisualState.Min, < 6 => WantedDeskVisualState.Medium, > 6 => WantedDeskVisualState.Full, _ => WantedDeskVisualState.None });
        }
    }

    public void UpdateUi(EntityUid uid)
    {
        var state = new WantedDeskBoundUserInterfaceState(WantedList);
        _ui.SetUiState(uid, WantedDeskUiKey.Key, state);
        _appearance.SetData(uid, WantedDeskVisuals.Appearance, WantedList.Count switch { <= 0 => WantedDeskVisualState.None, < 3 => WantedDeskVisualState.Min, < 6 => WantedDeskVisualState.Medium, > 6 => WantedDeskVisualState.Full, _ => WantedDeskVisualState.None });
    }
}
