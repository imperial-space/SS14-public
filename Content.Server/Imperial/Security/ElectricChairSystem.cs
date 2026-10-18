using System.Linq;
using Content.Server.Electrocution;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Imperial.Security;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Security;

/// <summary>
/// chair/e_chair и component/electrified_buckle: набор шокера на стуле превращает его в электростул.
/// Сигнал (кнопка или пульт) включает и выключает цикл: раз в 5 секунд сидящего бьёт током
/// из кабеля под стулом, и чем выше напряжение сети, тем сильнее.
/// </summary>
public sealed class ElectricChairSystem : EntitySystem
{
    public const string TogglePort = "Toggle";

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShockKitComponent, AfterInteractEvent>(OnKitAfterInteract);
        SubscribeLocalEvent<ElectricChairComponent, InteractUsingEvent>(OnChairInteractUsing);
        SubscribeLocalEvent<ElectricChairComponent, SignalReceivedEvent>(OnSignal);
        SubscribeLocalEvent<ElectricChairComponent, StrappedEvent>(OnStrapped);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ElectricChairComponent, StrapComponent>();
        while (query.MoveNext(out var uid, out var chair, out var strap))
        {
            if (chair.ShockVisualUntil is { } until && now >= until)
            {
                chair.ShockVisualUntil = null;
                _appearance.SetData(uid, ElectricChairVisuals.Shocking, false);
            }

            if (!chair.Active || strap.BuckledEntities.Count == 0 || now < chair.NextShock)
                continue;

            chair.NextShock = now + chair.LoopTime;
            Shock((uid, chair), strap);
        }
    }

    /// <summary>do_electrocution(): разряд по всем пристёгнутым.</summary>
    private void Shock(Entity<ElectricChairComponent> ent, StrapComponent strap)
    {
        var shocked = false;
        foreach (var mob in strap.BuckledEntities.ToList())
        {
            shocked |= _electrocution.TryDoElectrifiedAct(ent, mob);
        }

        if (!shocked)
            return;

        _popup.PopupEntity(Loc.GetString("electric-chair-shock", ("chair", ent.Owner)), ent, PopupType.LargeCaution);
        _appearance.SetData(ent, ElectricChairVisuals.Shocking, true);
        ent.Comp.ShockVisualUntil = _timing.CurTime + TimeSpan.FromSeconds(1);
    }

    /// <summary>on_buckle(): отсчёт до первого разряда начинается при посадке.</summary>
    private void OnStrapped(Entity<ElectricChairComponent> ent, ref StrappedEvent args)
    {
        ent.Comp.NextShock = _timing.CurTime + ent.Comp.LoopTime;
    }

    /// <summary>toggle_shock_loop().</summary>
    private void OnSignal(Entity<ElectricChairComponent> ent, ref SignalReceivedEvent args)
    {
        if (args.Port != TogglePort)
            return;

        ent.Comp.Active = !ent.Comp.Active;
        ent.Comp.NextShock = _timing.CurTime + ent.Comp.LoopTime;
        _popup.PopupEntity(Loc.GetString(ent.Comp.Active ? "electric-chair-armed" : "electric-chair-safe", ("chair", ent.Owner)),
            ent, PopupType.Medium);
    }

    /// <summary>chair/attackby(shock_kit) → electrify_self().</summary>
    private void OnKitAfterInteract(Entity<ShockKitComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } chair)
            return;

        if (!TryComp<StrapComponent>(chair, out var strap) || strap.Position != StrapPosition.Stand || HasComp<ElectricChairComponent>(chair))
            return;

        args.Handled = true;
        var xform = Transform(chair);
        var coords = xform.Coordinates;
        var rotation = xform.LocalRotation;
        var original = MetaData(chair).EntityPrototype?.ID;

        foreach (var buckled in strap.BuckledEntities.ToList())
        {
            _buckle.Unbuckle(buckled, args.User);
        }

        var echair = Spawn(ent.Comp.ChairProto, coords);
        _xform.SetLocalRotation(echair, rotation);
        var comp = EnsureComp<ElectricChairComponent>(echair);
        comp.OriginalChair = original;

        var kitContainer = _container.EnsureContainer<ContainerSlot>(echair, ElectricChairComponent.KitContainer);
        _container.Insert(ent.Owner, kitContainer);

        _popup.PopupEntity(Loc.GetString("electric-chair-assembled", ("chair", chair)), echair, args.User);
        QueueDel(chair);
    }

    /// <summary>Гаечный ключ или отвёртка снимают набор и возвращают обычный стул.</summary>
    private void OnChairInteractUsing(Entity<ElectricChairComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_tool.HasQuality(args.Used, new Robust.Shared.Prototypes.ProtoId<Content.Shared.Tools.ToolQualityPrototype>("Anchoring")) && !_tool.HasQuality(args.Used, new Robust.Shared.Prototypes.ProtoId<Content.Shared.Tools.ToolQualityPrototype>("Screwing")))
            return;

        args.Handled = true;
        var xform = Transform(ent);
        var coords = xform.Coordinates;

        if (_container.TryGetContainer(ent, ElectricChairComponent.KitContainer, out var kitContainer))
        {
            foreach (var kit in kitContainer.ContainedEntities.ToList())
            {
                _container.Remove(kit, kitContainer, destination: coords);
            }
        }

        if (ent.Comp.OriginalChair is { } original)
        {
            var chair = Spawn(original, coords);
            _xform.SetLocalRotation(chair, xform.LocalRotation);
        }

        _popup.PopupEntity(Loc.GetString("electric-chair-disassembled"), ent, args.User);
        QueueDel(ent);
    }
}
