using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.Imperial.LastWords;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Imperial.LastWords;

/// <summary>
/// Вкладка «Последние слова» окна итогов раунда: карточки как в манифесте игроков, но вместо роли — заголовок
/// с должностью и под ним фраза перед смертью. Сначала умершие, потом выжившие после критического состояния.
/// </summary>
public sealed class ImperialLastWordsTab : BoxContainer
{
    [Dependency] private readonly ImperialLastWordsManager _manager = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    private static readonly Color CardColor = Color.FromHex("#2A2A2D");
    private static readonly Color CardBorder = Color.FromHex("#3C3C40");
    private static readonly Color AliveColor = Color.FromHex("#8FBF8F");

    private readonly int _roundId;
    private readonly BoxContainer _list;

    public ImperialLastWordsTab(int roundId)
    {
        IoCManager.InjectDependencies(this);
        _roundId = roundId;
        Orientation = LayoutOrientation.Vertical;
        Name = Loc.GetString("imperial-last-words-tab-title");

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false, Margin = new Thickness(10) };
        _list = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 6, Margin = new Thickness(0, 0, 10, 0) };
        scroll.AddChild(_list);
        AddChild(scroll);

        Rebuild();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _manager.Updated += Rebuild;
        Rebuild();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _manager.Updated -= Rebuild;
    }

    private void Rebuild()
    {
        _list.RemoveAllChildren();

        // Слова пришли от другого раунда (или ещё не пришли) — ждём.
        if (_manager.RoundId != _roundId || _manager.Entries.Count == 0)
        {
            _list.AddChild(new Label
            {
                Text = Loc.GetString(_manager.RoundId == _roundId ? "imperial-last-words-empty" : "imperial-last-words-waiting"),
                FontColorOverride = Color.Gray,
            });
            return;
        }

        var jobFont = _cache.NotoStack("Bold", 16);
        foreach (var entry in _manager.Entries)
        {
            _list.AddChild(Card(entry, jobFont));
        }
    }

    private Control Card(ImperialLastWordsEntry entry, Font jobFont)
    {
        var card = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CardColor,
                BorderColor = CardBorder,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 6,
            },
            ToolTip = entry.Name,
        };

        var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 12 };
        card.AddChild(row);

        // Спрайт игрока — как в манифесте; если тело не дошло до клиента, место остаётся пустым.
        var portrait = new PanelContainer
        {
            MinSize = new Vector2(64, 64),
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#1E1E21") },
            VerticalAlignment = VAlignment.Center,
        };
        if (entry.Entity is { } net && _entities.TryGetEntity(net, out var uid))
        {
            portrait.AddChild(new SpriteView(uid.Value, _entities)
            {
                OverrideDirection = Direction.South,
                Scale = new Vector2(2, 2),
                SetSize = new Vector2(64, 64),
            });
        }

        row.AddChild(portrait);

        var text = new BoxContainer { Orientation = LayoutOrientation.Vertical, VerticalAlignment = VAlignment.Center, HorizontalExpand = true };
        row.AddChild(text);
        text.AddChild(new Label
        {
            Text = entry.Job,
            FontOverride = jobFont,
            FontColorOverride = Color.White,
            ClipText = true,
        });

        var phrase = new RichTextLabel { HorizontalExpand = true };
        var message = new Robust.Shared.Utility.FormattedMessage();
        message.PushTag(new Robust.Shared.Utility.MarkupNode("font", null, new Dictionary<string, Robust.Shared.Utility.MarkupParameter>
        {
            ["size"] = new(12),
        }));
        message.PushColor(Color.FromHex("#E6E6E6"));
        message.AddText(entry.Phrase);
        message.Pop();
        message.Pop();
        phrase.SetMessage(message);
        text.AddChild(phrase);

        if (!entry.Dead)
        {
            text.AddChild(new Label
            {
                Text = Loc.GetString("imperial-last-words-survived"),
                FontColorOverride = AliveColor,
            });
        }

        return card;
    }
}
