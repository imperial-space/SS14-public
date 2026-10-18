using Content.Shared.Imperial.Changeling;
using Robust.Client.Graphics;

namespace Content.Client.Imperial.Changeling;

/// <summary>HUD химикатов и обновление окон генокрада при смене его данных.</summary>
public sealed class ImperialChangelingClientSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialChangelingComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<ImperialChangelingComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_ui.TryGetOpenUi(ent.Owner, ImperialChangelingUiKey.Emporium, out var emporium))
            emporium.Update();

        if (_ui.TryGetOpenUi(ent.Owner, ImperialChangelingUiKey.Transform, out var transform))
            transform.Update();

        if (_ui.TryGetOpenUi(ent.Owner, ImperialChangelingUiKey.Info, out var info))
            info.Update();
    }
}
