using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.NullRod;
using Content.Shared.Imperial.NullRod.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.NullRod;

/// <summary>
/// Радиальное меню выбора святого оружия вокруг нулевого стержня, как show_radial_menu в SS13:
/// иконка каждой формы, при наведении — название и описание, формы отсортированы по названию.
/// </summary>
[UsedImplicitly]
public sealed class NullRodBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    /// <summary>Радиус кольца: форм больше двадцати, стандартного радиуса не хватает.</summary>
    private const int MenuRadius = 190;

    private SimpleRadialMenu? _menu;

    public NullRodBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        _menu.SetButtons(BuildOptions(), new SimpleRadialMenuSettings { DefaultContainerRadius = MenuRadius });
        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> BuildOptions()
    {
        var options = new List<(string Name, RadialMenuOptionBase Option)>();
        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract
                || !proto.TryGetComponent<NullRodVariantComponent>(out var variant, EntMan.ComponentFactory)
                || !variant.ChaplainSpawnable)
            {
                continue;
            }

            var option = new RadialMenuActionOption<EntProtoId>(OnPicked, proto.ID)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new EntProtoId(proto.ID)),
                ToolTip = $"{proto.Name}\n{Loc.GetString(variant.MenuDescription)}",
            };
            options.Add((proto.Name, option));
        }

        options.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        return options.Select(o => o.Option);
    }

    private void OnPicked(EntProtoId variant)
    {
        SendMessage(new NullRodPickVariantMessage(variant));
        Close();
    }
}
