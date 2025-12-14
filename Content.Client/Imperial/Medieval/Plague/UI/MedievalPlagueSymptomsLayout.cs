using System.Linq;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.Plague.UI;

public sealed partial class MedievalPlagueSymptomsLayout : LayoutContainer
{
    public MedievalPlagueSymptomsLayout()
    {

    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        foreach (var child in Children)
        {
            if (child is not MedievalPlagueSymptomButton button)
                continue;

            DrawLines(handle, button);
        }
    }

    private void DrawLines(DrawingHandleScreen handle, MedievalPlagueSymptomButton button)
    {
        var proto = button.Proto;
        var pos = button.Position + new Vector2(button.Width / 2, button.Height / 2);
        foreach (var child in Children)
        {
            if (child is not MedievalPlagueSymptomButton plag)
                continue;

            if (!proto.Required.Contains(plag.Proto.ID))
                continue;

            var targetPos = plag.Position + new Vector2(plag.Width / 2, plag.Height / 2);
            handle.DrawLine(pos, targetPos, Color.WhiteSmoke);
        }
    }
}
