using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.EmotePanel;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.EmotePanel;

/// <summary>Своя эмоция на панели: эмоция, эмоция со своим текстом или просто текст.</summary>
public sealed class ImperialPanelEmote
{
    public string Name = string.Empty;
    public ProtoId<ImperialEmotePrototype>? Emote;
    public string? Text;
}
