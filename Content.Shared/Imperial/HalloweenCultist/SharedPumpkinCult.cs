using Robust.Shared.Prototypes;
using Content.Shared.Actions;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Climbing.Events;
using Content.Shared.Humanoid;
using Content.Shared.SSDIndicator;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Audio.Systems;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffectNew;
using Content.Shared.Imperial.HalloweenCultist;
using Content.Shared.Interaction.Components;
using Content.Shared.DoAfter;
using Content.Shared.Mobs;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Popups;
using Content.Shared.Flash.Components;
using Content.Shared.Slippery;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Content.Shared.Mind.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.Imperial.HalloweenCultist.Components;

namespace Content.Shared.Imperial.HalloweenCultist;

public abstract class SharedHalloweenCultistSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] protected readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PumpkinGrab1SpellEvent>(OnGrab1);
        SubscribeLocalEvent<PumpkinGrab2SpellEvent>(OnGrab2);
        
        SubscribeLocalEvent<CultistKnifeComponent, AfterInteractEvent>(OnInteract);
        

        SubscribeLocalEvent<RobustOfferingComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<RobustOfferingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RobustOfferingActionEvent>(OnOffer);
        
        SubscribeLocalEvent<CultistCurseBlindActionEvent>(OnBlind);
        SubscribeLocalEvent<CultistCursePacifyActionEvent>(OnPacify);
    }

    private void OnGrab1(PumpkinGrab1SpellEvent args)
    {
        if(args.Handled)
            return;
        
        if(TryComp<StunnedComponent>(args.Target, out var stunned))
            return;

        _stun.TryKnockdown(args.Target, TimeSpan.FromSeconds(4f), force: true);

        args.Handled = true;
    }

    private void OnGrab2(PumpkinGrab2SpellEvent args)
    {
        if(args.Handled)
            return;
        
        if(TryComp<StunnedComponent>(args.Target, out var stunned))
            return;

        _stun.TryAddParalyzeDuration(args.Target, TimeSpan.FromSeconds(5f));

        args.Handled = true;
    }

    
    private void OnInteract(EntityUid uid, CultistKnifeComponent comp, AfterInteractEvent args)
    {
        if(args.Target == null)
            return;
        
        if(args.Target != args.User)
            return;
        
        if(!TryComp<PumpkinCultistComponent>(args.User, out var cult))
            return;
        
        if (!args.CanReach)
            return;

        comp.User = args.User;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(2f), new PumpkinCultKnifeDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            BreakOnMove = true,
            NeedHand = true,
        });

        args.Handled = true;
    }

    private void OnBlind(CultistCurseBlindActionEvent args)
    {
        var blindquery = EntityQueryEnumerator<MindContainerComponent>();
        while (blindquery.MoveNext(out var ent, out var mindToBlind))
        {
            if(HasComp<PumpkinCultistComponent>(ent))
                continue;
            
            if(!mindToBlind.HasMind)
                continue;

            AddComp<CurseBlindComponent>(ent);
        }

        args.Handled = true;
    }

    private void OnPacify(CultistCursePacifyActionEvent args)
    {
        var blindquery = EntityQueryEnumerator<MindContainerComponent>();
        while (blindquery.MoveNext(out var ent, out var mindToBlind))
        {
            if(HasComp<PumpkinCultistComponent>(ent))
                continue;
            
            if(!mindToBlind.HasMind)
                continue;

            AddComp<CursePacifyComponent>(ent);
        }

        args.Handled = true;
    }

    private void OnOffer(RobustOfferingActionEvent args)
    {
        if(args.Handled)
            return;
        
        AddComp<RobustOfferingComponent>(args.Performer);
        Del(args.Action);

        args.Handled = true;
    }
    
    private void OnMapInit(Entity<RobustOfferingComponent> ent, ref MapInitEvent args)
    {
        AddComp<FlashImmunityComponent>(ent);
        AddComp<NoSlipComponent>(ent);
    }

    private void OnShotAttempted(Entity<RobustOfferingComponent> ent, ref ShotAttemptedEvent args)
    {   
        _popup.PopupClient(Loc.GetString("gun-disabled"), ent, ent);
        args.Cancel();
    }
    
}
public sealed partial class CultistShopActionEvent : InstantActionEvent
{
}
public sealed partial class RobustOfferingActionEvent : InstantActionEvent
{
}
public sealed partial class CultistCurseBlindActionEvent : InstantActionEvent
{
}
public sealed partial class CultistCursePacifyActionEvent : InstantActionEvent
{
}
public sealed partial class PumpkinGrab1SpellEvent : EntityTargetActionEvent
{
}
public sealed partial class PumpkinGrab2SpellEvent : EntityTargetActionEvent
{
}
public sealed partial class PumpkiniteDevourEvent : EntityTargetActionEvent
{
}

[Serializable, NetSerializable]
public sealed partial class PumpkinCultKnifeDoAfterEvent : SimpleDoAfterEvent
{
}