using Content.Client.Items.Systems;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.Lavaland.Weapons;
using Content.Shared.Item;
using Content.Shared.Toggleable;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Lavaland.Weapons;

/// <summary>
/// Переключает in-hand спрайт боевого секача между состояниями
/// cleaving_saw (сложен) и cleaving_saw_open (разложен) при toggle.
/// </summary>
public sealed class CleavingSawVisualsSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;

    private const string LeftRsi = "Imperial/lava/mining/64x64_lefthand.rsi";
    private const string RightRsi = "Imperial/lava/mining/64x64_righthand.rsi";
    private const string StateClosed = "cleaving_saw";
    private const string StateOpen = "cleaving_saw_open";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CleavingSawComponent, GetInhandVisualsEvent>(OnGetInhandVisuals,
            after: [typeof(ItemSystem)]);
        SubscribeLocalEvent<CleavingSawComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<CleavingSawComponent> ent, ref AppearanceChangeEvent args)
    {
        _item.VisualsChanged(ent);
    }

    private void OnGetInhandVisuals(Entity<CleavingSawComponent> ent, ref GetInhandVisualsEvent args)
    {
        var activated = TryComp(ent, out AppearanceComponent? appearance)
            && _appearance.TryGetData<bool>(ent, ToggleableVisuals.Enabled, out var enabled, appearance)
            && enabled;

        var state = activated ? StateOpen : StateClosed;
        var rsi = args.Location == HandLocation.Left ? LeftRsi : RightRsi;
        var key = $"inhand-{args.Location.ToString().ToLowerInvariant()}";

        // Убираем слой, добавленный ItemSystem (если он есть), чтобы не было дубликата
        args.Layers.RemoveAll(l => l.Item1 == key);

        args.Layers.Add((key, new PrototypeLayerData
        {
            RsiPath = rsi,
            State = state,
        }));
    }
}
