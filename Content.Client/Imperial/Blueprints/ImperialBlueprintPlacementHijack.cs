using Content.Client.Imperial.Blueprints.Types;
using Robust.Client.Graphics;
using Robust.Client.Placement;
using Robust.Shared.Map;

namespace Content.Client.Imperial.Blueprints;

/// <summary>Перехват размещения: клик по карте ставит весь чертёж, R поворачивает его.</summary>
public sealed class ImperialBlueprintPlacementHijack(ImperialBlueprintSystem system, ImperialBlueprint blueprint) : PlacementHijack
{
    public readonly ImperialBlueprint Blueprint = blueprint;

    public override bool CanRotate => true;

    /// <summary>Этот перехват сейчас управляет размещением (до StartHijack менеджера ещё нет).</summary>
    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
    public bool Active => Manager != null && Manager.Hijack == this;

    public override bool HijackPlacementRequest(EntityCoordinates coordinates)
    {
        system.Place(Blueprint, coordinates, Manager.Direction);
        return true;
    }

    public override bool HijackDeletion(EntityUid entity)
    {
        return true;
    }

    public override void StartHijack(PlacementManager manager)
    {
        base.StartHijack(manager);

        // Свой предпросмотр рисует оверлей — у курсора текстур нет.
        manager.CurrentTextures = new List<IDirectionalTextureProvider>();
    }
}
