using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Content.Client.Imperial.Medieval.Factions;
using Content.Client.Stylesheets;
using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Microsoft.Extensions.Logging;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Serilog;
using Content.Shared.Imperial.Medieval.Factions.Components;

namespace Content.Client.Imperial.Medieval.Factions.UI.Elements;

public sealed partial class FactionRelationsPanel : GridContainer
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    public Action<ProtoId<MedievalFactionPrototype>>? WarPressed;
    private (Button, TimeSpan)? _time;

    public FactionRelationsPanel(FactionMenuData data)
    {
        IoCManager.InjectDependencies(this);

        var factions = _proto.EnumeratePrototypes<MedievalFactionPrototype>()
            .OrderBy(f => f.ID == data.Faction ? 0 : 1).ThenBy(f => f.Name);

        Rows = factions.Count() + 1;
        Columns = factions.Count() + 1;

        for (var i = -1; i < factions.Count(); i++)
        {
            if (i < 0)
            {
                AddLabels(data.Faction);
                continue;
            }

            Add(data.Faction, factions.ToList()[i], data.Relations[factions.ElementAt(i)], data.Access == FactionMenuAccess.Full);
        }
    }

    public void Add(ProtoId<MedievalFactionPrototype> userFaction, ProtoId<MedievalFactionPrototype> faction, Dictionary<ProtoId<MedievalFactionPrototype>, ProtoId<FactionRelationsPrototype>> relations, bool allowWar)
    {
        var factions = _proto.EnumeratePrototypes<MedievalFactionPrototype>()
            .OrderBy(f => f.ID == userFaction ? 0 : 1).ThenBy(f => f.Name);

        var label = new RichTextLabel()
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            SizeFlagsStretchRatio = 2,
            Margin = new(2)
        };

        BoxContainer box = new()
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SetHeight = 64f,
            Margin = new Thickness(4),
            Children = { label }
        };

        if (userFaction != faction && allowWar)
        {
            var button = new Button()
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                SizeFlagsStretchRatio = 1,
                Text = "Война",
                Disabled = relations.GetValueOrDefault(userFaction, "Neutral") == "War" || _proto.Index(userFaction).BlockedRelations.Contains(faction),
                Margin = new(2)
            };

            button.OnPressed += args =>
            {
                if (!_time.HasValue)
                {
                    button.StyleClasses.Add(StyleNano.StyleClassButtonColorRed);
                    button.Text = "Подтвердить";
                    _time = (button, _timing.CurTime + TimeSpan.FromSeconds(3));
                }
                else if (_time.Value.Item1 == button)
                {
                    button.StyleClasses.Remove(StyleNano.StyleClassButtonColorRed);
                    button.Text = "Война";
                    button.Disabled = true;
                    WarPressed?.Invoke(faction);
                }
                else
                {
                    _time.Value.Item1.StyleClasses.Remove(StyleNano.StyleClassButtonColorRed);
                    _time.Value.Item1.Text = "Война";

                    button.StyleClasses.Add(StyleNano.StyleClassButtonColorRed);
                    button.Text = "Подтвердить";
                    _time = (button, _timing.CurTime + TimeSpan.FromSeconds(3));
                }
            };

            box.AddChild(button);
        }

        label.SetMessage(_proto.Index(faction).Name);
        AddChild(box);

        foreach (var item in factions)
        {
            if (item.ID == faction)
            {
                var control = new Control()
                {
                    HorizontalExpand = true,
                    VerticalExpand = true,
                    Margin = new Thickness(4)
                };

                AddChild(new BoxContainer()
                {
                    HorizontalExpand = true,
                    SetHeight = 64f,
                    Margin = new Thickness(4),
                    Children = { control }
                });

                continue;
            }

            AddElement(relations.GetValueOrDefault(item.ID, "Neutral"), _proto.Index(faction).BlockedRelations.Contains(item));
        }
    }

    public void AddLabels(ProtoId<MedievalFactionPrototype> userFaction)
    {
        var factions = _proto.EnumeratePrototypes<MedievalFactionPrototype>()
            .OrderBy(f => f.ID == userFaction ? 0 : 1).ThenBy(f => f.Name);

        AddChild(new Control()
        {
            HorizontalExpand = true,
            SetHeight = 32f,
            Margin = new Thickness(4)
        });

        foreach (var faction in factions)
        {
            var label = new RichTextLabel()
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                Margin = new Thickness(4)
            };
            BoxContainer box = new()
            {
                HorizontalExpand = true,
                SetHeight = 32f,
                Margin = new Thickness(4),
                Children = { label }
            };

            label.SetMessage(faction.Name);
            AddChild(box);
        }
    }

    private void AddElement(ProtoId<FactionRelationsPrototype> relation, bool blocked)
    {
        var relationProto = _proto.Index(relation);

        var icon = new TextureRect
        {
            Texture = relationProto.Icon.Frame0(),
            ToolTip = relationProto.Name,
            HorizontalExpand = true,
            VerticalExpand = true,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0)
        };

        if (blocked)
        {
            icon.AddChild(new TextureRect
            {
                Texture = new SpriteSpecifier.Rsi(new("Imperial/Medieval/Interface/faction_relations.rsi"), "blocked").Frame0(),
                HorizontalExpand = true,
                VerticalExpand = true,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
            });
        }

        BoxContainer box = new()
        {
            HorizontalExpand = true,
            SetHeight = 64f,
            Margin = new Thickness(4),
            Children = { icon }
        };

        AddChild(box);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        for (var i = 0; i <= Columns; i++)
        {
            var first = Children.First();

            var leftTop = first.Position + new Vector2(-first.Margin.Left, -first.Margin.Top);
            var rightTop = leftTop;
            var leftBottom = leftTop;

            var heightDiff = Vector2.Zero;
            var widthDiff = Vector2.Zero;

            for (var j = 0; j < i; j++)
            {
                var widthElement = Children.ElementAt(j);
                var heightElement = Children.ElementAt(j * Columns);
                heightDiff += new Vector2(0, heightElement.Margin.Top * 2 + heightElement.Height + heightElement.Margin.Bottom);
                widthDiff += new Vector2(widthElement.Margin.Left * 2 + widthElement.Width + widthElement.Margin.Right, 0);
            }
            for (var j = 0; j < Columns; j++)
            {
                var elementWidth = Children.ElementAt(j);
                rightTop += new Vector2(elementWidth.Margin.Left * 2 + elementWidth.Width + elementWidth.Margin.Right, 0);

                var elementHeight = Children.ElementAt(Columns * j);
                leftBottom += new Vector2(0, elementHeight.Margin.Top * 2 + elementHeight.Height + elementHeight.Margin.Bottom);
            }

            handle.DrawLine(leftTop + heightDiff, rightTop + heightDiff, Color.Gray);
            handle.DrawLine(leftTop + widthDiff, leftBottom + widthDiff, Color.Gray);
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!_time.HasValue)
            return;

        if (_time.Value.Item2 <= _timing.CurTime)
        {
            _time.Value.Item1.StyleClasses.Remove(StyleNano.StyleClassButtonColorRed);
            _time.Value.Item1.Text = "Война";
            _time = null;
        }
    }
}
