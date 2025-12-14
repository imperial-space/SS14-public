using Content.Client.Alerts;
using Content.Client.Flash;
using Content.Client.UserInterface.Systems.Alerts.Controls;
using Content.Shared.Flash.Components;
using Content.Shared.Imperial.Medieval.Illitid;
using Content.Shared.Revenant;
using Content.Shared.Revenant.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.Imperial.Medieval.Illitid;

public sealed class IllitidSystem : SharedIllitidSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private IllitidFlashOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IllitidComponent, UpdateAlertSpriteEvent>(OnUpdateAlert);


        SubscribeLocalEvent<IllitidFlashedComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<IllitidFlashedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<IllitidFlashedComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<IllitidFlashedComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        _overlay = new();
    }

    private void OnPlayerAttached(EntityUid uid, IllitidFlashedComponent component, LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(EntityUid uid, IllitidFlashedComponent component, LocalPlayerDetachedEvent args)
    {
        _overlay.ScreenshotTexture = null;
        _overlay.RequestScreenTexture = false;
        _overlayMan.RemoveOverlay(_overlay);
    }

    private void OnInit(EntityUid uid, IllitidFlashedComponent component, ComponentInit args)
    {
        if (_player.LocalEntity == uid)
        {
            _overlay.RequestScreenTexture = true;
            _overlayMan.AddOverlay(_overlay);
        }
    }

    private void OnShutdown(EntityUid uid, IllitidFlashedComponent component, ComponentShutdown args)
    {
        if (_player.LocalEntity == uid)
        {
            _overlay.ScreenshotTexture = null;
            _overlay.RequestScreenTexture = false;
            _overlayMan.RemoveOverlay(_overlay);
        }
    }


    private void OnUpdateAlert(Entity<IllitidComponent> ent, ref UpdateAlertSpriteEvent args)
    {
        if (args.Alert.ID != ent.Comp.PsiAlert)
            return;

        var psi = ent.Comp.PsiLevel;

        _sprite.LayerSetRsiState(args.SpriteViewEnt.AsNullable(), AlertVisualLayers.Base, $"s{psi}");
    }
}
