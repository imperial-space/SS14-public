using JetBrains.Annotations;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.RichText;

[UsedImplicitly]
public sealed class PurplePulseTag : IMarkupTagHandler
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public string Name => "purplepulse";

    public void PushDrawContext(MarkupNode node, MarkupDrawingContext context)
    {
        var t = (float)(_timing.CurTime.TotalSeconds % 2.0);
        var wave = (MathF.Sin(t * MathF.PI) + 1f) * 0.5f;
        var r = (byte)(0x55 + wave * (0xCC - 0x55));
        var g = (byte)(0x00 + wave * 0x44);
        var b = (byte)(0x99 + wave * (0xFF - 0x99));
        context.Color.Push(new Color(r, g, b));
    }

    public void PopDrawContext(MarkupNode node, MarkupDrawingContext context)
    {
        context.Color.Pop();
    }
}
