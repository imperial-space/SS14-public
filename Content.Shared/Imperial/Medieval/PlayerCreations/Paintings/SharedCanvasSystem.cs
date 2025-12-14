
using Content.Shared.Interaction.Events;


namespace Content.Shared.Imperial.Medieval.PlayerCreations.Paintings;
public sealed class SharedCanvasSystem : EntitySystem
{

    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CanvasComponent, UseInHandEvent>(OnUse);
    }

    private void OnUse(EntityUid uid, CanvasComponent comp, UseInHandEvent args)
    {
        _ui.OpenUi(uid, PaintUiKey.Key, args.User);

        UpdateUiState(uid, comp.Texture);
    }


    private void UpdateUiState(EntityUid uid, Color[] texture)
    {
        _ui.SetUiState(uid, PaintUiKey.Key, new PaintingBoundUserInterfaceState(texture));
    }
}
