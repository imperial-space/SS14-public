using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.UniversalSecurity;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Random;
using Content.Server.CustomDoorKey.Components;

public sealed partial class UniversalLockpickServerSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly UniversalLockableServerSystem _lockableSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalLockpickComponent, AfterInteractEvent>(OnLockpickInteract);
        SubscribeLocalEvent<UniversalLockpickComponent, UniversalLockpickSetCodeMessage>(OnSetCodeReceived);
        SubscribeLocalEvent<UniversalLockpickComponent, UniversalLockpickHackDoAfterEvent>(OnHackDoAfter);
    }

    private void OnLockpickInteract(Entity<UniversalLockpickComponent> lockpickEntity, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } lockableUid)
            return;

        if (!TryComp<UniversalLockableComponent>(lockableUid, out var lockableComponent))
            return;

        if (!_itemSlots.TryGetSlot(lockableUid, "lockSlot", out var slot) || slot.Item is not { } lockUid)
            return;

        if (!TryComp<UniversalLockComponent>(lockUid, out var lockComponent) || !lockComponent.IsSetuped)
            return;

        lockpickEntity.Comp.LockUid = lockUid;
        lockpickEntity.Comp.LockableUid = lockableUid;
        lockpickEntity.Comp.User = args.User;

        var state = new UniversalLockpickBuiState(lockComponent.MaxValue, lockComponent.Length, new int[lockComponent.Length]);
        _uiSystem.SetUiState(lockpickEntity.Owner, UniversalSecurityUiKey.Lockpick, state);

        if (_uiSystem.TryOpenUi(lockpickEntity.Owner, UniversalSecurityUiKey.Lockpick, args.User))
            args.Handled = true;
    }

    private void OnSetCodeReceived(Entity<UniversalLockpickComponent> lockpickEntity, ref UniversalLockpickSetCodeMessage args)
    {
        if (lockpickEntity.Comp.LockUid is not { } lockUid || !Exists(lockUid) ||
            lockpickEntity.Comp.LockableUid is not { } lockableUid || !Exists(lockableUid) ||
            lockpickEntity.Comp.User is not { } user || !Exists(user) ||
            !TryComp<UniversalLockComponent>(lockUid, out var lockComponent) ||
            !TryComp<SkillsComponent>(user, out var skillComponent) ||
            !lockComponent.IsSetuped ||
            !_itemSlots.TryGetSlot(lockableUid, "lockSlot", out var slot))
        {
            _uiSystem.CloseUi(lockpickEntity.Owner, UniversalSecurityUiKey.Lockpick);
            return;
        }

        if (!_interactionSystem.InRangeUnobstructed(args.Actor, lockableUid))
        {
            _uiSystem.CloseUi(lockpickEntity.Owner, UniversalSecurityUiKey.Lockpick);
            return;
        }

        if (args.NewCode == null || args.NewCode.Length != lockComponent.Length)
        {
            _uiSystem.CloseUi(lockpickEntity.Owner, UniversalSecurityUiKey.Lockpick);
            return;
        }

        float agility = skillComponent.Levels["Agility"];
        if (agility <= 0) agility = 1f;

        var ev = new UniversalLockpickHackDoAfterEvent
        {
            NewCode = args.NewCode
        };

        var doAfterTime = Math.Max(0.1f, lockpickEntity.Comp.HackTime / (agility / 5f));

        var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(doAfterTime), ev, lockpickEntity, lockpickEntity, lockableUid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            DistanceThreshold = 2.0f
        };

        if (_doAfterSystem.TryStartDoAfter(doAfterArgs))
            _audioSystem.PlayPvs(new SoundPathSpecifier(lockpickEntity.Comp.EffectSoundOnNext), lockableUid);
    }

    private void OnHackDoAfter(Entity<UniversalLockpickComponent> lockpickEntity, ref UniversalLockpickHackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
        {
            _uiSystem.CloseUi(lockpickEntity.Owner, UniversalSecurityUiKey.Lockpick);
            args.Handled = true;
            return;
        }

        if (lockpickEntity.Comp.LockUid is not { } lockUid || !Exists(lockUid) ||
            lockpickEntity.Comp.LockableUid is not { } lockableUid || !Exists(lockableUid) ||
            lockpickEntity.Comp.User is not { } user || !Exists(user) ||
            !TryComp<UniversalLockComponent>(lockUid, out var lockComponent) ||
            !TryComp<SkillsComponent>(user, out var skillComponent) ||
            !lockComponent.IsSetuped ||
            !_itemSlots.TryGetSlot(lockableUid, "lockSlot", out var slot) ||
            args.NewCode == null || args.NewCode.Length != lockComponent.Length)
        {
            _uiSystem.CloseUi(lockpickEntity.Owner, UniversalSecurityUiKey.Lockpick);
            args.Handled = true;
            return;
        }


        if (args.NewCode.SequenceEqual(lockComponent.Code))
        {
            _lockableSystem.OnUsedKeySuccess((lockUid, lockComponent), (lockableUid, Comp<UniversalLockableComponent>(lockableUid)), slot, user);
            _audioSystem.PlayPvs(new SoundPathSpecifier(lockpickEntity.Comp.EffectSoundOnSucces), lockUid);

            _uiSystem.CloseUi(lockpickEntity.Owner, UniversalSecurityUiKey.Lockpick);

            lockpickEntity.Comp.LockUid = null;
            lockpickEntity.Comp.LockableUid = null;
            lockpickEntity.Comp.User = null;

            args.Handled = true;
            return;
        }

        bool isHacker = HasComp<DoorHackerComponent>(user);
        int[] stateCode = new int[lockComponent.Length];

        for (var i = 0; i < args.NewCode.Length; i++)
        {
            if (lockComponent.Code[i] == args.NewCode[i])
            {
                stateCode[i] = 255;
                continue;
            }

            if (isHacker)
            {
                int diff = Math.Abs(lockComponent.Code[i] - args.NewCode[i]);
                int direction = Math.Sign(lockComponent.Code[i] - args.NewCode[i]);

                stateCode[i] = diff <= 3 ? direction : direction * 2;
            }
            else
                stateCode[i] = 0;
        }

        var state = new UniversalLockpickBuiState(lockComponent.MaxValue, lockComponent.Length, stateCode);
        _uiSystem.SetUiState(lockpickEntity.Owner, UniversalSecurityUiKey.Lockpick, state);
        _audioSystem.PlayPvs(new SoundPathSpecifier(lockpickEntity.Comp.EffectSoundOnNext), lockpickEntity);

        float agility = skillComponent.Levels["Agility"];
        if (agility <= 0)
            agility = 1f;

        var breakChance = Math.Clamp(lockpickEntity.Comp.BreakChance / MathF.Max(0.1f, agility / 10f), 0.01f, 0.75f);
        if (_random.Prob(breakChance))
            OnLockpickBreak(lockpickEntity);

        args.Handled = true;
    }

    private void OnLockpickBreak(Entity<UniversalLockpickComponent> lockpickEntity)
    {
        var audioParams = new AudioParams { Volume = -5 };

        _audioSystem.PlayPvs(new SoundPathSpecifier(lockpickEntity.Comp.EffectSoundOnBreak), Transform(lockpickEntity).Coordinates, audioParams);
        _uiSystem.CloseUi(lockpickEntity.Owner, UniversalSecurityUiKey.Lockpick);

        QueueDel(lockpickEntity);
    }
}