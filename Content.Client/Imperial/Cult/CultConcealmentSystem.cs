using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Doors.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Управляет видимостью рун и структур культа на стороне клиента.
/// Скрытые объекты (Concealed = true) не видны игрокам без культовой принадлежности.
/// </summary>
public sealed class CultConcealmentSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly Color CultAirlockBaseColor = Color.FromHex("#8B0000");
    private static readonly Color CultAirlockUnlitColor = Color.FromHex("#FF0000");
    private EntityUid? _lastAttached;

    public override void Initialize()
    {
        base.Initialize();

        // Обновляем при изменении сетевого состояния (toggled сервером)
        SubscribeLocalEvent<CultRuneComponent, AfterAutoHandleStateEvent>(OnRuneHandleState);
        SubscribeLocalEvent<CultStructureComponent, AfterAutoHandleStateEvent>(OnStructureHandleState);

        // Обновляем при первом появлении объекта в PVS
        SubscribeLocalEvent<CultRuneComponent, ComponentStartup>(OnRuneStartup);
        SubscribeLocalEvent<CultStructureComponent, ComponentStartup>(OnStructureStartup);

        // Когда игрок становится/перестаёт быть культистом — обновляем всё
        SubscribeLocalEvent<CultistComponent, ComponentStartup>(OnCultistStartup);
        SubscribeLocalEvent<CultistComponent, ComponentShutdown>(OnCultistShutdown);

        // Когда игрок надевает/снимает повязку фанатика — обновляем всё
        SubscribeLocalEvent<CanSeeConcealedComponent, ComponentStartup>(OnCanSeeStartup);
        SubscribeLocalEvent<CanSeeConcealedComponent, ComponentShutdown>(OnCanSeeShutdown);
    }

    private void OnRuneHandleState(EntityUid uid, CultRuneComponent comp, ref AfterAutoHandleStateEvent args)
        => UpdateVisibility(uid, comp.Concealed);

    private void OnStructureHandleState(EntityUid uid, CultStructureComponent comp, ref AfterAutoHandleStateEvent args)
        => UpdateStructure(uid, comp);

    private void OnRuneStartup(EntityUid uid, CultRuneComponent comp, ComponentStartup args)
        => UpdateVisibility(uid, comp.Concealed);

    private void OnStructureStartup(EntityUid uid, CultStructureComponent comp, ComponentStartup args)
        => UpdateStructure(uid, comp);

    private void OnCultistStartup(EntityUid uid, CultistComponent comp, ComponentStartup args)
    {
        if (uid == _player.LocalSession?.AttachedEntity)
            RefreshAll();
    }

    private void OnCultistShutdown(EntityUid uid, CultistComponent comp, ComponentShutdown args)
    {
        if (uid == _player.LocalSession?.AttachedEntity)
            RefreshAll();
    }

    private void OnCanSeeStartup(EntityUid uid, CanSeeConcealedComponent comp, ComponentStartup args)
    {
        if (uid == _player.LocalSession?.AttachedEntity)
            RefreshAll();
    }

    private void OnCanSeeShutdown(EntityUid uid, CanSeeConcealedComponent comp, ComponentShutdown args)
    {
        if (uid == _player.LocalSession?.AttachedEntity)
            RefreshAll();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var attached = _player.LocalSession?.AttachedEntity;
        if (attached == _lastAttached)
            return;

        _lastAttached = attached;
        RefreshAll();
    }

    /// <summary>
    /// Обновляет видимость всех скрытых рун и структур (например, когда статус игрока изменился).
    /// </summary>
    private void RefreshAll()
    {
        var runeQuery = AllEntityQuery<CultRuneComponent>();
        while (runeQuery.MoveNext(out var uid, out var rune))
            UpdateVisibility(uid, rune.Concealed);

        var structQuery = AllEntityQuery<CultStructureComponent>();
        while (structQuery.MoveNext(out var uid, out var structure))
            UpdateStructure(uid, structure);
    }

    private void UpdateStructure(EntityUid uid, CultStructureComponent structure)
    {
        if (structure.StructureType == CultStructureType.RunedAirlock)
        {
            UpdateRunedAirlockAppearance(uid, structure.Concealed);
            return;
        }

        UpdateVisibility(uid, structure.Concealed);
    }

    private void UpdateVisibility(EntityUid uid, bool concealed)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!concealed)
        {
            sprite.Visible = true;
            return;
        }

        var localPlayer = _player.LocalSession?.AttachedEntity;
        if (localPlayer == null)
        {
            sprite.Visible = false;
            return;
        }

        var canSee = HasComp<CultistComponent>(localPlayer.Value)
                  || HasComp<CanSeeConcealedComponent>(localPlayer.Value);

        sprite.Visible = canSee;
    }

    private void UpdateRunedAirlockAppearance(EntityUid uid, bool concealed)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        sprite.Visible = true;

        var disguised = concealed && !CanSeeConcealedLocally();
        _sprite.LayerSetColor((uid, sprite), DoorVisualLayers.Base, disguised ? Color.White : CultAirlockBaseColor);
        _sprite.LayerSetColor((uid, sprite), DoorVisualLayers.BaseUnlit, disguised ? Color.White : CultAirlockUnlitColor);
    }

    private bool CanSeeConcealedLocally()
    {
        var localPlayer = _player.LocalSession?.AttachedEntity;
        return localPlayer != null
            && (HasComp<CultistComponent>(localPlayer.Value)
                || HasComp<CanSeeConcealedComponent>(localPlayer.Value));
    }
}
