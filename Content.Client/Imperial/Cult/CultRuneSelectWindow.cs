using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Окно выбора руны при нажатии Z с ритуальным кинжалом.
/// Для руны телепорта показывает поле ввода названия.
/// </summary>
public sealed class CultRuneSelectWindow : DefaultWindow
{
    /// <summary>Вызывается при выборе руны. Первый аргумент — ключ руны, второй — метка (только для телепорта).</summary>
    public event Action<string, string?>? OnRuneSelected;

    private static readonly (string LocKey, string RuneId)[] _runes =
    {
        ("cult-rune-teleport",  "teleport"),
        ("cult-rune-empower",   "empower"),
        ("cult-rune-offering",  "offering"),
        ("cult-rune-revive",    "revive"),
        ("cult-rune-barrier",   "barrier"),
        ("cult-rune-summoning", "summoning"),
        ("cult-rune-bloodboil", "bloodboil"),
        ("cult-rune-spirit-realm", "spiritRealm"),
        ("cult-rune-narsie",    "narsie"),
    };

    public CultRuneSelectWindow()
    {
        Title = Loc.GetString("cult-rune-select-title");
        MinSize = new Vector2(260, 360);

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
            SeparationOverride = 4,
        };

        foreach (var (locKey, runeId) in _runes)
        {
            var capturedId = runeId;

            if (runeId == "teleport")
            {
                // Кнопка телепорта раскрывает inline-панель ввода названия
                var teleportBtn = new Button
                {
                    Text = Loc.GetString(locKey),
                    HorizontalExpand = true,
                    MinSize = new Vector2(0, 32),
                };

                var namePanel = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    Margin = new Thickness(8, 2, 8, 4),
                    SeparationOverride = 4,
                    Visible = false,
                };

                var nameLabel = new Label
                {
                    Text = Loc.GetString("cult-teleport-name-prompt"),
                    HorizontalAlignment = HAlignment.Left,
                };

                var nameEdit = new LineEdit
                {
                    PlaceHolder = Loc.GetString("cult-teleport-name-placeholder"),
                    HorizontalExpand = true,
                    MinSize = new Vector2(0, 28),
                };

                var confirmBtn = new Button
                {
                    Text = Loc.GetString("cult-rune-draw-confirm"),
                    HorizontalExpand = true,
                    MinSize = new Vector2(0, 28),
                };

                void SubmitTeleport()
                {
                    var label = nameEdit.Text.Trim();
                    if (string.IsNullOrEmpty(label))
                        label = Loc.GetString("cult-teleport-name-default");
                    OnRuneSelected?.Invoke(capturedId, label);
                    Close();
                }

                confirmBtn.OnPressed += _ => SubmitTeleport();
                nameEdit.OnTextEntered += _ => SubmitTeleport();

                namePanel.AddChild(nameLabel);
                namePanel.AddChild(nameEdit);
                namePanel.AddChild(confirmBtn);

                teleportBtn.OnPressed += _ =>
                {
                    namePanel.Visible = !namePanel.Visible;
                    if (namePanel.Visible)
                        nameEdit.GrabKeyboardFocus();
                };

                vbox.AddChild(teleportBtn);
                vbox.AddChild(namePanel);
            }
            else
            {
                var btn = new Button
                {
                    Text = Loc.GetString(locKey),
                    HorizontalExpand = true,
                    MinSize = new Vector2(0, 32),
                };
                btn.OnPressed += _ =>
                {
                    OnRuneSelected?.Invoke(capturedId, null);
                    Close();
                };
                vbox.AddChild(btn);
            }
        }

        scroll.AddChild(vbox);
        Contents.AddChild(scroll);
    }
}

