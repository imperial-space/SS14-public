using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Chaplain;
using Content.Shared.Imperial.Chaplain.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Chaplain;

/// <summary>
/// Радиальное меню маяка вооружения: иконка главного предмета каждого набора, при наведении — название.
/// </summary>
[UsedImplicitly]
public sealed class HolyArmamentsBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private SimpleRadialMenu? _menu;

    public HolyArmamentsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        _menu.SetButtons(BuildOptions());
        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> BuildOptions()
    {
        var options = new List<(string Name, RadialMenuOptionBase Option)>();
        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || !proto.TryGetComponent<HolyArmamentsKitComponent>(out var kit, EntMan.ComponentFactory))
                continue;

            var option = new RadialMenuActionOption<EntProtoId>(OnPicked, proto.ID)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(kit.Preview),
                ToolTip = proto.Name,
            };
            options.Add((proto.Name, option));
        }

        return options
            .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(o => o.Option);
    }

    private void OnPicked(EntProtoId kit)
    {
        SendMessage(new HolyArmamentsPickMessage(kit));
        Close();
    }
}
