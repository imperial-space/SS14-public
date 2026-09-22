using System.Text;
using Content.Client.CharacterInfo;
using Content.Client.Message;
using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticCharacterInfoSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CharacterInfoSystem.GetCharacterInfoControlsEvent>(OnGetControls);
    }

    private void OnGetControls(ref CharacterInfoSystem.GetCharacterInfoControlsEvent ev)
    {
        if (!HasComp<HereticComponent>(ev.Entity))
            return;

        ev.Controls.Add(new HereticMansusControl());
    }

    private sealed class HereticMansusControl : BoxContainer
    {
        private readonly RichTextLabel _label;
        private const string Text = "Ваши задачи ожидают вас в Книге Мансуса";
        private float _t;

        public HereticMansusControl()
        {
            Orientation = LayoutOrientation.Vertical;
            _label = new RichTextLabel
            {
                HorizontalExpand = true,
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(0, 8, 0, 8),
            };
            AddChild(_label);
            UpdateLabel(0f);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            _t += args.DeltaSeconds;
            UpdateLabel(_t);
        }

        private void UpdateLabel(float t)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < Text.Length; i++)
            {
                var hue = (t * 0.4f + i / (float) Text.Length) % 1f;
                var c = HueToColor(hue);
                var r = (int) (c.R * 255);
                var g = (int) (c.G * 255);
                var b = (int) (c.B * 255);
                sb.Append($"[color=#{r:x2}{g:x2}{b:x2}]{Text[i]}[/color]");
            }
            _label.SetMarkup(sb.ToString());
        }

        private static Color HueToColor(float h)
        {
            h = (h % 1f + 1f) % 1f;
            var hi = (int) (h * 6);
            var f = h * 6 - hi;
            return (hi % 6) switch
            {
                0 => new Color(1f, f,       0f),
                1 => new Color(1f - f, 1f,  0f),
                2 => new Color(0f, 1f,      f),
                3 => new Color(0f, 1f - f,  1f),
                4 => new Color(f,  0f,      1f),
                _ => new Color(1f - f, 0f,  1f),
            };
        }
    }
}
