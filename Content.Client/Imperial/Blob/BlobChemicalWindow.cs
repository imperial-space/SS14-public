using System;
using System.Numerics;
using Content.Shared.Imperial.Blob;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Blob;

public sealed class BlobChemicalWindow : DefaultWindow
{
    private static readonly ResPath BlobRsi = new("Imperial/blob/blob.rsi");

    [Dependency] private readonly IEntitySystemManager _entitySystem = default!;

    public event Action<BlobChemicalType>? OnChemicalSelected;

    private readonly Label _summary;
    private readonly BoxContainer _options;
    private readonly SpriteSystem _spriteSystem;

    public BlobChemicalWindow()
    {
        IoCManager.InjectDependencies(this);
        _spriteSystem = _entitySystem.GetEntitySystem<SpriteSystem>();

        Title = Loc.GetString("blob-chemical-menu-title");
        MinSize = new Vector2(320, 360);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(8),
        };

        _summary = new Label
        {
            HorizontalExpand = true,
        };

        root.AddChild(_summary);

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        _options = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };

        scroll.AddChild(_options);
        root.AddChild(scroll);
        Contents.AddChild(root);
    }

    public void SetState(BlobChemicalMenuState state)
    {
        _summary.Text = Loc.GetString(
            "blob-chemical-menu-summary",
            ("current", Loc.GetString(BlobChemicalVisuals.GetNameLocId(state.CurrentChemical))),
            ("biomass", state.Biomass),
            ("cost", state.ChangeCost));
        _summary.ModulateSelfOverride = state.CurrentColor;

        _options.RemoveAllChildren();

        foreach (var chemical in state.Chemicals)
        {
            var selected = chemical == state.CurrentChemical;
            var affordable = selected || state.Biomass >= state.ChangeCost;
            var color = BlobChemicalVisuals.GetColor(chemical);
            var capturedChemical = chemical;

            var button = new Button
            {
                HorizontalExpand = true,
                Disabled = !affordable,
                MinSize = new Vector2(0, 42),
            };

            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 10,
                Margin = new Thickness(8, 6),
            };

            row.AddChild(new TextureRect
            {
                Texture = _spriteSystem.Frame0(new SpriteSpecifier.Rsi(BlobRsi, "blob")),
                MinSize = new Vector2(28, 28),
                Stretch = TextureRect.StretchMode.KeepCentered,
                ModulateSelfOverride = color,
            });

            row.AddChild(new Label
            {
                Text = selected
                    ? Loc.GetString("blob-chemical-menu-entry-selected", ("chemical", Loc.GetString(BlobChemicalVisuals.GetNameLocId(chemical))))
                    : Loc.GetString("blob-chemical-menu-entry", ("chemical", Loc.GetString(BlobChemicalVisuals.GetNameLocId(chemical)))),
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Center,
            });

            if (!selected)
            {
                row.AddChild(new Label
                {
                    Text = affordable
                        ? Loc.GetString("blob-chemical-menu-cost", ("cost", state.ChangeCost))
                        : Loc.GetString("blob-chemical-menu-cost-blocked", ("cost", state.ChangeCost)),
                    VerticalAlignment = VAlignment.Center,
                    ModulateSelfOverride = affordable ? Color.White : Color.FromHex("#d05b5b"),
                });
            }

            button.OnPressed += _ =>
            {
                OnChemicalSelected?.Invoke(capturedChemical);
                Close();
            };

            button.AddChild(row);
            _options.AddChild(button);
        }
    }
}