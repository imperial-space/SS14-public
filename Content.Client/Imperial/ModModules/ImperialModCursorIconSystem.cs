using Content.Client.Hands;
using Content.Client.UserInterface.Systems.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Imperial.ModModules;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.ModModules;

/// <summary>
/// Пока модуль МОДа выбран для применения по цели, у курсора висит его значок — как выбранное оборудование меха.
/// Оверлей предмета в руке рисует только IconOverride, поэтому подставляется текстура значка модуля.
/// </summary>
public sealed class ImperialModCursorIconSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private EntityUid? _shown;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_overlays.TryGetOverlay<ShowHandItemOverlay>(out var overlay))
            return;

        if (_ui.GetUIController<ActionUIController>().SelectingTargetFor is not { } action
            || !TryComp<ActionComponent>(action, out var comp)
            || comp.EntIcon is not { } module
            || !HasComp<ImperialModModuleComponent>(module)
            || MetaData(module).EntityPrototype is not { } proto)
        {
            _shown = null;
            return;
        }

        if (_shown == module && overlay.IconOverride != null)
            return;

        _shown = module;
        overlay.EntityOverride = null;
        overlay.IconOverride = _sprite.GetPrototypeIcon(proto.ID).Default;
    }
}
