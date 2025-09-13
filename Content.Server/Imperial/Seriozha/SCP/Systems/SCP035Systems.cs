using Content.Server.Stunnable;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Seriozha.SCP.Components;
using Content.Shared.Imperial.Seriozha.SCP.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Content.Shared.NPC.Systems;
using Content.Server.NPC.HTN;
using Content.Shared.NPC.Components;

namespace Content.Server.Imperial.Seriozha.SCP.Systems;

/*
private static readonly Dictionary<string, string> TemporarySlotMap = new()
    {
        {"head", "HELMET"},
        {"eyes", "EYES"},
        {"ears", "EARS"},
        {"mask", "MASK"},
        {"outerClothing", "OUTERCLOTHING"},
        {Jumpsuit, "INNERCLOTHING"},
        {"neck", "NECK"},
        {"back", "BACKPACK"},
        {"belt", "BELT"},
        {"gloves", "HAND"},
        {"shoes", "FEET"},
        {"id", "IDCARD"},
        {"pocket1", "POCKET1"},
        {"pocket2", "POCKET2"},
        {"suitstorage", "SUITSTORAGE"},
    };
*/

public sealed partial class SCP035System : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly NpcFactionSystem _npcFactionSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SCP035Component, SCP035IntoEvent>(SCP035Into);
        SubscribeLocalEvent<SCP035Component, BeingUnequippedAttemptEvent>(TryUnequip);
        SubscribeLocalEvent<SCP035Component, SCP035OutoEvent>(SCP035Outo);
        SubscribeLocalEvent<SCP035Component, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SCP035Component, SCP035ControlEvent>(DoControl);
        SubscribeLocalEvent<SCP035Component, SCP035HealEvent>(OnHealAndControl);
    }
    private void Controll(SCP035Component comp, bool corpse)
    {
        if (!TryComp<MindContainerComponent>(comp.In, out var mindContainerComponent) || !mindContainerComponent.HasMind || comp.In == null) return;
        var oobject = Spawn(comp.Prototype);

        var mind = mindContainerComponent.Mind.Value;
        var internalContainer = _container.EnsureContainer<ContainerSlot>(comp.In.Value, comp.ContainerId);

        if (_container.Insert(oobject, internalContainer))
            _mind.TransferTo(mind, oobject);

        if (!corpse)
        {
            comp.Mind = mind;
            comp.Object = oobject;
            comp.Container = internalContainer;
        }

        _mind.TransferTo(mind, comp.In);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SCP035Component>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ActualTime + comp.Time > _timing.CurTime || comp.In == null || comp.Container == null || comp.Object == null || !comp.UnderControl) return;

            _mind.TransferTo(comp.In.Value, uid);

            if (!comp.Corpses[comp.In.Value])
            {
                _mind.TransferTo(comp.Object.Value, comp.In);
                Del(comp.Object); // or "_container.RemoveEntity(comp.In.Value, comp.Object.Value);", it really doesn't matter
                _container.Remove(comp.In.Value, comp.Container);
            }
            // clear yay
            comp.Mind = null;
            comp.Container = null;
            comp.Object = null;
            comp.ActualTime = TimeSpan.Zero;
            comp.UnderControl = false;
        }
    }
    private void OnMapInit(EntityUid uid, SCP035Component comp, MapInitEvent ev)
    {
        _action.AddAction(uid, "ActionSCP035Into");
        _action.AddAction(uid, "ActionSCP035Outo");
        _action.AddAction(uid, "ActionSCP035Control");
        _action.AddAction(uid, "ActionSCP035Heal");
    }
    private void TryUnequip(EntityUid uid, SCP035Component comp, BeingUnequippedAttemptEvent ev)
    {
        if (comp.In != null || comp.TryinUneq) ev.Cancel();
    }
    private void SCP035Into(EntityUid uid, SCP035Component comp, SCP035IntoEvent ev)
    {
        if (comp.In != null || ev.Handled) return;
        ev.Handled = true;
        _inventory.TryEquip(ev.Target, uid, "HELMET", true, true);
        comp.In = ev.Target;
        _stun.TryStun(ev.Target, TimeSpan.FromSeconds(1), false);
    }
    private void SCP035Outo(EntityUid uid, SCP035Component comp, SCP035OutoEvent ev)
    {
        if (comp.In == null || comp.Object == null || comp.Container == null || comp.UnderControl) return;
        comp.TryinUneq = true;
        _inventory.TryUnequip(comp.In.Value, "HELMET");
        _stun.TryStun(comp.In.Value, TimeSpan.FromSeconds(5), false);

        if (comp.Corpses[comp.In.Value])
        {
            var component = EnsureComp<HTNComponent>(comp.In.Value);
            component.RootTask = new HTNCompoundTask()
            {
                Task = "SimpleHumanoidHostileCompound"
            };
            EnsureComp<NpcFactionMemberComponent>(comp.In.Value);

            _npcFactionSystem.ClearFactions(comp.In.Value);
            _npcFactionSystem.AddFaction(comp.In.Value, comp.FactionToChange);
        }
        comp.In = null;
        comp.TryinUneq = false;
    }
    private void DoControl(EntityUid uid, SCP035Component comp, SCP035ControlEvent _)
    {
        if (!TryComp<MindContainerComponent>(comp.In, out var mindContainerComponent) || !mindContainerComponent.HasMind || comp.In == null || _mobState.IsDead(comp.In.Value) || ev.Handled) return;
        ev.Handled = true;
        Controll(comp, false);
        if (!comp.Corpses.ContainsKey(comp.In.Value)) comp.Corpses.TryAdd(comp.In.Value, false);
        comp.UnderControl = true;
        comp.ActualTime = _timing.CurTime;
    }
    private void OnHealAndControl(EntityUid uid, SCP035Component comp, SCP035HealEvent ev)
    {
        if (comp.In == null || comp.Object == null || comp.Container == null || comp.UnderControl) return;
        if (!_mobState.IsDead(comp.In.Value)) return;
        if (!TryComp<DamageableComponent>(comp.In.Value, out var damageableComponent)) return;
        if (!comp.Corpses.ContainsKey(comp.In.Value)) comp.Corpses.TryAdd(comp.In.Value, true);
        foreach (var pair in damageableComponent.Damage.DamageDict)
        {
            var specifier = new DamageSpecifier()
            {
                DamageDict = new Dictionary<string, FixedPoint2>()
                {
                    { pair.Key, -pair.Value },
                }
            };
            _damage.TryChangeDamage(comp.In.Value, specifier);
        }
        _mobState.ChangeMobState(comp.In.Value, MobState.Alive);
        Controll(comp, true);
    }
}
