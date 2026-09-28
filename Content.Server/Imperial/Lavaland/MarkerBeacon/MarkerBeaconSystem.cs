using Content.Shared.DoAfter;
using Content.Shared.Imperial.Lavaland.MarkerBeacon;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Server.Stack;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Maths;

namespace Content.Server.Imperial.Lavaland.MarkerBeacon;

public sealed class MarkerBeaconSystem : EntitySystem
{
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly PointLightSystem _pointLight = default!;

    private static readonly MarkerBeaconColor[] AllColors = Enum.GetValues<MarkerBeaconColor>();

    private static readonly Dictionary<MarkerBeaconColor, Color> LightColors = new()
    {
        { MarkerBeaconColor.Burgundy,  Color.FromHex("#8B1A1A") },
        { MarkerBeaconColor.Bronze,    Color.FromHex("#CD7F32") },
        { MarkerBeaconColor.Yellow,    Color.FromHex("#FFD700") },
        { MarkerBeaconColor.Lime,      Color.FromHex("#39FF14") },
        { MarkerBeaconColor.Olive,     Color.FromHex("#9ACD32") },
        { MarkerBeaconColor.Jade,      Color.FromHex("#00C878") },
        { MarkerBeaconColor.Teal,      Color.FromHex("#00CED1") },
        { MarkerBeaconColor.Cerulean,  Color.FromHex("#00BFFF") },
        { MarkerBeaconColor.Indigo,    Color.FromHex("#6A5ACD") },
        { MarkerBeaconColor.Purple,    Color.FromHex("#BF00FF") },
        { MarkerBeaconColor.Violet,    Color.FromHex("#EE82EE") },
        { MarkerBeaconColor.Fuchsia,   Color.FromHex("#FF00FF") },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MarkerBeaconComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<MarkerBeaconComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<MarkerBeaconStructureComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<MarkerBeaconStructureComponent, MarkerBeaconPickupDoAfterEvent>(OnPickupDoAfter);
    }

    private void OnUseInHand(EntityUid uid, MarkerBeaconComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<StackComponent>(uid, out var stack) || stack.Count <= 0)
            return;

        _stack.SetCount(uid, stack.Count - 1, stack);

        var structure = Spawn("MarkerBeaconStructure", Transform(args.User).Coordinates);
        if (TryComp<MarkerBeaconStructureComponent>(structure, out var structComp))
        {
            structComp.Color = comp.Color;
            _appearance.SetData(structure, MarkerBeaconVisuals.Color, comp.Color);
            _pointLight.SetColor(structure, LightColors[comp.Color]);
        }

        args.Handled = true;
    }

    private void OnGetVerbs(EntityUid uid, MarkerBeaconComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var next = AllColors[((int)comp.Color + 1) % AllColors.Length];

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("marker-beacon-verb-cycle",
                ("color", Loc.GetString($"marker-beacon-color-{next.ToString().ToLowerInvariant()}"))),
            Act = () =>
            {
                comp.Color = next;
                _popup.PopupEntity(
                    Loc.GetString("marker-beacon-color-set",
                        ("color", Loc.GetString($"marker-beacon-color-{comp.Color.ToString().ToLowerInvariant()}"))),
                    uid, args.User, PopupType.Small);
            }
        });
    }

    private void OnInteractHand(EntityUid uid, MarkerBeaconStructureComponent comp, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User,
            TimeSpan.FromSeconds(1.5), new MarkerBeaconPickupDoAfterEvent(), uid, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        });

        args.Handled = true;
    }

    private void OnPickupDoAfter(EntityUid uid, MarkerBeaconStructureComponent comp, MarkerBeaconPickupDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var beacon = Spawn("MarkerBeacon1", Transform(uid).Coordinates);
        if (TryComp<MarkerBeaconComponent>(beacon, out var beaconComp))
            beaconComp.Color = comp.Color;

        QueueDel(uid);
    }
}
