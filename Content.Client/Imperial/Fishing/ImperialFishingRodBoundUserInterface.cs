using System.Numerics;
using Content.Shared.Imperial.Fishing;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Fishing;

/// <summary>
/// Окно снасти удочки (tgui FishingRod): наживка, леска и крючок большими кнопками.
/// Клик пустой рукой вынимает, с предметом в руке — вставляет или меняет.
/// </summary>
public sealed class ImperialFishingRodBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private readonly Dictionary<string, (Button Button, TextureRect Icon, Label Name)> _slots = new();
    private RichTextLabel? _description;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.SetSize = new Vector2(400, 450);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var slots = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        foreach (var (slot, title) in new[] { ("bait", "imperial-fishing-ui-bait"), ("line", "imperial-fishing-ui-line"), ("hook", "imperial-fishing-ui-hook") })
        {
            var icon = new TextureRect
            {
                SetSize = new Vector2(64, 64),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
            };
            var name = new Label { HorizontalExpand = true, Align = Label.AlignMode.Center, ClipText = true };
            var content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            content.AddChild(icon);
            content.AddChild(name);

            var button = new Button { HorizontalExpand = true, MinHeight = 70 };
            button.AddChild(content);
            var slotId = slot;
            button.OnPressed += _ => SendMessage(new ImperialFishingRodSlotMessage(slotId));

            slots.AddChild(ImperialFishingUi.Section(Loc.GetString(title), button));
            _slots[slot] = (button, icon, name);
        }

        root.AddChild(ImperialFishingUi.Section(null, slots));
        _description = new RichTextLabel { HorizontalExpand = true };
        root.AddChild(ImperialFishingUi.Section(null, _description));
        _window.Contents.AddChild(root);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialFishingRodBoundUserInterfaceState rod)
            return;

        var sprite = EntMan.System<SpriteSystem>();
        void Set(string slot, ImperialFishingRodSlotData data)
        {
            if (!_slots.TryGetValue(slot, out var controls))
                return;

            controls.Name.Text = data.Name ?? Loc.GetString("imperial-fishing-ui-none");
            controls.Icon.Texture = data.Proto != null ? sprite.GetPrototypeIcon(data.Proto).Default : null;
            controls.Icon.Visible = data.Proto != null;
        }

        Set("bait", rod.Bait);
        Set("line", rod.Line);
        Set("hook", rod.Hook);
        if (_description != null)
            _description.SetMessage(rod.Description);
    }
}
