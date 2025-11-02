using Content.Server.Store.Systems;
using Content.Shared.Store.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Actions;
using Content.Shared.Imperial.HalloweenCultist;
using Content.Shared.Stacks;
using Content.Shared.Chemistry.Components;
using Content.Server.Body.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Humanoid;
using Content.Server.Revolutionary.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Climbing.Events;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Robust.Shared.Audio.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.HalloweenCultist.Components;

namespace Content.Server.Imperial.HalloweenCultist;

public class HalloweenCultistSystem : SharedHalloweenCultistSystem
{
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PumpkinCultistComponent, CultistShopActionEvent>(OnShop);
        SubscribeLocalEvent<PumpkinCultistComponent, MapInitEvent>(OnMapInit);   

        SubscribeLocalEvent<GibCultistRuneComponent, ClimbedOnEvent>(OnClimbedOn);
        SubscribeLocalEvent<PumpkiniteDevourEvent>(OnDevour);

        SubscribeLocalEvent<CultistKnifeComponent, PumpkinCultKnifeDoAfterEvent>(OnDoAfter);
    }
    private void OnMapInit(Entity<PumpkinCultistComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ent.Comp.ShopActionId);
    }
    
    private void OnShop(Entity<PumpkinCultistComponent> ent, ref CultistShopActionEvent args)
    {
        if (!TryComp<StoreComponent>(ent, out var store))
            return;

        _store.ToggleUi(ent, ent, store);
    }

    private void OnDevour(PumpkiniteDevourEvent args)
    {
        if(args.Handled)
            return;

        if(!TryComp<PumpkiniteComponent>(args.Target, out var pumpkinite))
            return;

        if(!TryComp<StackComponent>(args.Target, out var stack))
            return;

        var ichorInjection = new Solution("Ichor", 1f * stack.Count);

        _bloodstreamSystem.TryAddToChemicals(args.Performer, ichorInjection);

        Del(args.Target);

        args.Handled = true;
    }

    private void OnClimbedOn(Entity<GibCultistRuneComponent> ent, ref ClimbedOnEvent args)
    {
        if(!TryComp<HumanoidAppearanceComponent>(args.Climber, out var hum))
            return;
        
        //if(!TryComp<SSDIndicatorComponent >(args.Climber, out var ssd))
        //    return;
        //if(ssd.IsSSD == true)
        //    return;

        if(!TryComp<BodyComponent>(args.Climber, out var body))
            return;

        if(TryComp<PumpkinCultistComponent>(args.Climber, out var cult))
            return;

        _transform.SetCoordinates(args.Climber, Transform(args.Climber), Transform(ent).Coordinates);
        _transform.AttachToGridOrMap(args.Climber, Transform(args.Climber));

        if (_mind.TryGetMind(args.Climber, out var perMind, out var perMindComp))
        {
            var pumpkin = Spawn(ent.Comp.EntProto, Transform(ent).Coordinates);
            _transform.AttachToGridOrMap(pumpkin, Transform(ent));
            _mind.TransferTo(perMind, pumpkin);
        }
            
        _bodySystem.GibBody(args.Climber, body: body);

        if(TryComp<CommandStaffComponent>(args.Climber, out var head))
        {
            var metalHead = Spawn(ent.Comp.MetalProtoHead, Transform(ent).Coordinates);

            if(args.Instigator != null)
                _hands.TryPickupAnyHand(args.Instigator, metalHead);
        } 
        else
        {
            var metal = Spawn(ent.Comp.MetalProto, Transform(ent).Coordinates);

            if(args.Instigator != null)
                _hands.TryPickupAnyHand(args.Instigator, metal);
        } 

        _audio.PlayPvs(ent.Comp.Sound, ent);

        //Del(ent);
    }
    protected virtual void OnDoAfter(EntityUid uid, CultistKnifeComponent comp, DoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        var rune = Spawn(comp.RuneProto, Transform(args.User).Coordinates);
        _transform.AttachToGridOrMap(rune, Transform(args.User));
        _damageable.TryChangeDamage(args.User, comp.Damage, true);

        args.Handled = true;
    }
    
}