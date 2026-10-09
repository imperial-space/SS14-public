using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Blob;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Blob;

/// <summary>
/// Радиальные меню оверманда: выбор нового штамма (open_reroll_menu) и прыжок к узлу (jump_to_node).
/// </summary>
[UsedImplicitly]
public sealed class BlobOvermindBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly SpriteSpecifier CoreIcon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/blob/blob.rsi"), "blob_core");
    private static readonly SpriteSpecifier NodeIcon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/blob/blob.rsi"), "blob_node_overlay");

    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.OpenOverMouseScreenPosition();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_menu == null)
            return;

        var buttons = new List<RadialMenuOptionBase>();
        switch (state)
        {
            case BlobRerollState reroll:
                foreach (var id in reroll.Choices)
                {
                    if (!_proto.TryIndex<BlobStrainPrototype>(id, out var strain))
                        continue;

                    var tooltip = Loc.GetString(strain.Name) + "\n" + Loc.GetString(strain.AnalyzerDamage);
                    if (strain.AnalyzerEffect is { } effect)
                        tooltip += "\n" + Loc.GetString(effect);

                    buttons.Add(new RadialMenuActionOption<string>(SelectStrain, id)
                    {
                        IconSpecifier = RadialMenuIconSpecifier.With(CoreIcon),
                        ToolTip = tooltip,
                        BackgroundColor = strain.Color.WithAlpha(0.6f),
                        HoverBackgroundColor = strain.Color.WithAlpha(0.9f),
                    });
                }
                break;

            case BlobNodesState nodes:
                for (var i = 0; i < nodes.Nodes.Count; i++)
                {
                    buttons.Add(new RadialMenuActionOption<NetEntity>(JumpToNode, nodes.Nodes[i])
                    {
                        IconSpecifier = RadialMenuIconSpecifier.With(NodeIcon),
                        ToolTip = nodes.Names[i],
                    });
                }
                break;
        }

        _menu.SetButtons(buttons);
    }

    private void SelectStrain(string strain) => SendMessage(new BlobSelectStrainMessage(strain));

    private void JumpToNode(NetEntity node) => SendMessage(new BlobJumpToNodeMessage(node));
}
