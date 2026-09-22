using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Heretic;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Heretic;

[UsedImplicitly]
public sealed class HereticMawedCrucibleBui : BoundUserInterface
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly ResPath EldritchRsi = new("/Textures/Imperial/heretic/eldritch.rsi");

    private SimpleRadialMenu? _menu;
    private HereticCrucibleBuiState? _lastState;

    public HereticMawedCrucibleBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<SimpleRadialMenu>();
        if (_lastState != null)
            _menu.SetButtons(BuildButtons(_lastState));
        _menu.OpenOverMouseScreenPosition();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not HereticCrucibleBuiState s)
            return;
        _lastState = s;
        _menu?.SetButtons(BuildButtons(s));
    }

    private IEnumerable<RadialMenuOptionBase> BuildButtons(HereticCrucibleBuiState state)
    {
        var now = _timing.CurTime;
        var noCharges = state.Charges <= 0;

        return new RadialMenuOptionBase[]
        {
            MakeButton(HereticCruciblePotionType.Soul, "crucible_soul",
                Loc.GetString("heretic-crucible-soul-name"),
                Loc.GetString("heretic-crucible-soul-desc"),
                TimeSpan.Zero, now, noCharges),

            MakeButton(HereticCruciblePotionType.Clarity, "clarity",
                Loc.GetString("heretic-crucible-clarity-name"),
                Loc.GetString("heretic-crucible-clarity-desc"),
                TimeSpan.Zero, now, noCharges),

            MakeButton(HereticCruciblePotionType.Marshal, "marshal",
                Loc.GetString("heretic-crucible-marshal-name"),
                Loc.GetString("heretic-crucible-marshal-desc"),
                TimeSpan.Zero, now, noCharges),
        };
    }

    private RadialMenuActionOption<HereticCruciblePotionType> MakeButton(
        HereticCruciblePotionType potion,
        string rsiState,
        string name,
        string description,
        TimeSpan cooldownEnd,
        TimeSpan now,
        bool noCharges)
    {
        var onCooldown = now < cooldownEnd;
        var remaining = onCooldown ? (int)(cooldownEnd - now).TotalSeconds + 1 : 0;

        var tooltip = name;
        if (noCharges)
            tooltip += $"\n{Loc.GetString("heretic-crucible-no-charges")}";
        else if (onCooldown)
            tooltip += $"\n{Loc.GetString("heretic-crucible-cooldown-remaining", ("seconds", remaining))}";
        else
            tooltip += $"\n{description}";

        return new RadialMenuActionOption<HereticCruciblePotionType>(OnPotionSelected, potion)
        {
            IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(EldritchRsi, rsiState)),
            ToolTip = tooltip,
        };
    }

    private void OnPotionSelected(HereticCruciblePotionType potion)
    {
        SendMessage(new HereticCrucibleSelectMessage(potion));
    }
}
