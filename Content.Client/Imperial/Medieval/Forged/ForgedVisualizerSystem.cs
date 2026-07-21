using Content.Shared.Forged;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Forged;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Medieval.Forged;

public sealed class ForgedVisualizerSystem : VisualizerSystem<ForgedComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, ForgedComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null) return;

        UpdateAppearance(uid, component, args.Sprite, args.Component);
    }

    private void UpdateAppearance(EntityUid uid, ForgedComponent component, SpriteComponent sprite, AppearanceComponent appearance)
    {
        foreach (ForgedVisuals visualKey in Enum.GetValues(typeof(ForgedVisuals)))
        {
            if (AppearanceSystem.TryGetData<ForgedVisualsPacket>(uid, visualKey, out var packet, appearance)) SetLayer(uid, visualKey, packet, sprite);
        }
    }

    private void SetLayer(EntityUid uid, ForgedVisuals partKey, ForgedVisualsPacket packet, SpriteComponent sprite)
    {
        if (partKey != ForgedVisuals.core && partKey != ForgedVisuals.torso_upgrade)
        {
            HumanoidVisualLayers? targetLayer = partKey switch
            {
                ForgedVisuals.crown      => HumanoidVisualLayers.HeadTop,
                ForgedVisuals.head       => HumanoidVisualLayers.Head,
                ForgedVisuals.eyes       => HumanoidVisualLayers.Eyes,
                ForgedVisuals.torso      => HumanoidVisualLayers.Chest,
                ForgedVisuals.right_arm  => HumanoidVisualLayers.RArm,
                ForgedVisuals.left_arm   => HumanoidVisualLayers.LArm,
                ForgedVisuals.right_hand => HumanoidVisualLayers.RHand,
                ForgedVisuals.left_hand  => HumanoidVisualLayers.LHand,
                ForgedVisuals.right_leg  => HumanoidVisualLayers.RLeg,
                ForgedVisuals.left_leg   => HumanoidVisualLayers.LLeg,
                ForgedVisuals.right_foot  => HumanoidVisualLayers.RFoot,
                ForgedVisuals.left_foot   => HumanoidVisualLayers.LFoot,

                _ => null
            };

            if (targetLayer == null)
                return;

            if (packet.State == "blank" || string.IsNullOrEmpty(packet.State))
                return;
    
            if (_sprite.LayerMapTryGet((uid, sprite), targetLayer.Value, out var index, false))
            {
                _sprite.LayerSetRsi((uid, sprite), index, packet.RsiPath);
                _sprite.LayerSetRsiState((uid, sprite), index, packet.State);
                _sprite.LayerSetVisible((uid, sprite), index, true);
            }
        }
        else
        {
            if (_sprite.LayerMapTryGet((uid, sprite), partKey.ToString(), out var index, false))
            {
                _sprite.LayerSetRsi((uid, sprite), index, packet.RsiPath);
                _sprite.LayerSetRsiState((uid, sprite), index, packet.State);
                _sprite.LayerSetVisible((uid, sprite), index, true);
            }
        }
    }
}
