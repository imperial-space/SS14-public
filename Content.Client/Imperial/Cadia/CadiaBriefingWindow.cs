using System.Numerics;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cadia;

/// <summary>Памятка роли: кто ты, зачем ты здесь и что нажимать. Открывается при появлении и действием на панели.</summary>
public sealed class CadiaBriefingWindow : DefaultWindow
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public CadiaBriefingWindow(string roleId, string name)
    {
        IoCManager.InjectDependencies(this);
        MinSize = SetSize = new Vector2(620, 620);
        Title = Loc.GetString("cadia-guide-title");

        if (!_proto.TryIndex<CadiaRolePrototype>(roleId, out var role))
            return;

        var accent = role.Faction == CadiaFaction.Imperium ? "#e8c66a" : "#d23b2a";
        var markup = new FormattedMessage();
        void Section(string header, string body)
        {
            markup.AddMarkupPermissive($"[color={accent}][bold]{header}[/bold][/color]\n{body}\n\n");
        }

        markup.AddMarkupPermissive($"[font size=16][color={accent}][bold]{FormattedMessage.EscapeText(name)}[/bold][/color][/font]\n");
        markup.AddMarkupPermissive($"{Loc.GetString(role.Name)}\n\n");
        Section(Loc.GetString("cadia-guide-task"), role.Briefing is { } briefing ? Loc.GetString(briefing) : Loc.GetString(role.Description));
        Section(Loc.GetString("cadia-guide-goal"), Loc.GetString(role.Faction == CadiaFaction.Imperium ? "cadia-goal-imperium" : "cadia-goal-chaos"));
        foreach (var tip in role.Tips)
            Section(Loc.GetString($"{tip}-title"), Loc.GetString(tip));
        Section(Loc.GetString("cadia-tip-common-title"), Loc.GetString("cadia-tip-common"));

        var text = new RichTextLabel { HorizontalExpand = true, Margin = new Thickness(8) };
        text.SetMessage(markup);
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(text);

        var close = new Button { Text = Loc.GetString("cadia-guide-close"), HorizontalAlignment = HAlignment.Center, MinWidth = 180, Margin = new Thickness(0, 6) };
        close.OnPressed += _ => Close();

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        root.AddChild(scroll);
        root.AddChild(close);
        Contents.AddChild(root);
    }
}
