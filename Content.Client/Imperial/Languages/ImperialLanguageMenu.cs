using System.Linq;
using System.Numerics;
using Content.Client.Message;
using Content.Shared.Imperial.Languages;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Languages;

/// <summary>Открывает меню языков по просьбе сервера и шлёт выбор.</summary>
public sealed class ImperialLanguageMenuSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;

    private ImperialLanguageMenu? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ImperialLanguageMenuOpenMessage>(OnOpen);
        SubscribeLocalEvent<ImperialLanguageSpeakerComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnOpen(ImperialLanguageMenuOpenMessage msg)
    {
        if (_window != null)
        {
            _window.Close();
            return;
        }

        _window = new ImperialLanguageMenu(id => RaiseNetworkEvent(new ImperialLanguageSelectMessage(id)));
        _window.OnClose += () => _window = null;
        Refresh();
        _window.OpenCentered();
    }

    private void OnState(Entity<ImperialLanguageSpeakerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Owner == _player.LocalEntity)
            Refresh();
    }

    private void Refresh()
    {
        if (_window == null || _player.LocalEntity is not { } local || !TryComp<ImperialLanguageSpeakerComponent>(local, out var speaker))
            return;

        _window.Update((local, speaker));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }
}

/// <summary>Language Menu из TG: известные языки, ключи для чата и выбор языка по умолчанию.</summary>
public sealed class ImperialLanguageMenu : DefaultWindow
{
    private readonly IPrototypeManager _proto;
    private readonly SharedImperialLanguageSystem _lang;
    private readonly Action<ProtoId<ImperialLanguagePrototype>> _onSelect;
    private readonly BoxContainer _list = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };

    public ImperialLanguageMenu(Action<ProtoId<ImperialLanguagePrototype>> onSelect)
    {
        _proto = IoCManager.Resolve<IPrototypeManager>();
        _lang = IoCManager.Resolve<IEntityManager>().System<SharedImperialLanguageSystem>();
        _onSelect = onSelect;
        Title = Loc.GetString("imperial-language-menu-title");
        MinSize = new Vector2(420, 300);
        SetSize = new Vector2(480, 520);

        var hint = new RichTextLabel { Margin = new Thickness(4, 0, 4, 6) };
        hint.SetMarkup(Loc.GetString("imperial-language-menu-hint"));
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(_list);
        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        root.AddChild(hint);
        root.AddChild(scroll);
        Contents.AddChild(root);
    }

    public void Update(Entity<ImperialLanguageSpeakerComponent> speaker)
    {
        _list.RemoveAllChildren();
        var current = _lang.GetCurrent((speaker.Owner, speaker.Comp));
        var known = speaker.Comp.Spoken.Concat(speaker.Comp.Understood).Concat(speaker.Comp.Partial.Keys).Distinct()
            .Select(id => _proto.TryIndex(id, out var proto) ? proto : null)
            .OfType<ImperialLanguagePrototype>()
            .OrderByDescending(l => l.Priority)
            .ThenBy(l => Loc.GetString(l.Name));

        foreach (var language in known)
        {
            var spoken = _lang.CanSpeak((speaker.Owner, speaker.Comp), language.ID);
            var understanding = _lang.Understanding((speaker.Owner, speaker.Comp), language.ID);
            var status = speaker.Comp.Blocked.Contains(language.ID)
                ? Loc.GetString("imperial-language-menu-blocked")
                : spoken
                    ? Loc.GetString("imperial-language-menu-spoken")
                    : understanding >= 100
                        ? Loc.GetString("imperial-language-menu-understood")
                        : Loc.GetString("imperial-language-menu-partial", ("percent", understanding));

            var title = new RichTextLabel { HorizontalExpand = true };
            title.SetMarkup(Loc.GetString("imperial-language-menu-entry",
                ("name", Loc.GetString(language.Name)), ("key", language.Key), ("status", status),
                ("color", language.Color.ToHex())));

            var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            header.AddChild(title);
            if (spoken)
            {
                var selected = language.ID == current.Id;
                var button = new Button
                {
                    Text = Loc.GetString(selected ? "imperial-language-menu-selected" : "imperial-language-menu-select"),
                    Disabled = selected,
                    MinWidth = 110,
                };
                var id = language.ID;
                button.OnPressed += _ => _onSelect(id);
                header.AddChild(button);
            }

            var desc = new RichTextLabel { Margin = new Thickness(8, 0, 0, 0) };
            desc.SetMessage(Loc.GetString(language.Description));

            var entry = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
            entry.AddChild(header);
            entry.AddChild(desc);
            var panel = new PanelContainer { Children = { entry } };
            panel.PanelOverride = new Robust.Client.Graphics.StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#1b1b22"),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 4,
            };
            _list.AddChild(panel);
        }
    }
}
