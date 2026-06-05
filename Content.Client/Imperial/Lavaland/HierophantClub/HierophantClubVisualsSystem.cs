using Content.Client.Items.Systems;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.Lavaland.HierophantClub;
using Content.Shared.Item;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Lavaland.HierophantClub;

/// <summary>
/// Переключает in-hand спрайт посоха Иерофанта между состояниями
/// hierophant_club (нет зарядов) и hierophant_club_ready (есть заряды).
/// </summary>
public sealed class HierophantClubVisualsSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;

    private const string LeftRsi = "Imperial/lava/mining/64x64_lefthand.rsi";
    private const string RightRsi = "Imperial/lava/mining/64x64_righthand.rsi";
    private const string StateNoCharges = "hierophant_club";
    private const string StateReady = "hierophant_club_ready";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HierophantClubComponent, GetInhandVisualsEvent>(OnGetInhandVisuals,
            after: [typeof(ItemSystem)]);
        SubscribeLocalEvent<HierophantClubComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<HierophantClubComponent> ent, ref AppearanceChangeEvent args)
    {
        _item.VisualsChanged(ent);
    }

    private void OnGetInhandVisuals(Entity<HierophantClubComponent> ent, ref GetInhandVisualsEvent args)
    {
        var hasCharges = TryComp(ent, out AppearanceComponent? appearance)
            && _appearance.TryGetData<bool>(ent, HierophantClubVisuals.HasCharges, out var val, appearance)
            && val;

        var state = hasCharges ? StateReady : StateNoCharges;
        var rsi = args.Location == HandLocation.Left ? LeftRsi : RightRsi;
        var key = $"inhand-{args.Location.ToString().ToLowerInvariant()}";

        args.Layers.RemoveAll(l => l.Item1 == key);
        args.Layers.Add((key, new PrototypeLayerData
        {
            RsiPath = rsi,
            State = state,
        }));
    }
}
