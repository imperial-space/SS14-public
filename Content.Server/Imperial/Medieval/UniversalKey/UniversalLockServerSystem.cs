using Content.Shared.Imperial.Medieval.UniversalSecurity;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Shared.Audio.Systems;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.Medieval.UniversalLock;

public sealed class UniversalLockServerSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalLockComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<UniversalLockComponent, UniversalLockSetCodeMessage>(OnSetCodeReceived);
    }

    private void OnUseInHand(Entity<UniversalLockComponent> lockEntity, ref UseInHandEvent args)
    {
        if (args.Handled || lockEntity.Comp.IsSetuped)
            return;

        var state = new UniversalLockBuiState(lockEntity.Comp.MaxValue, lockEntity.Comp.Length);
        _uiSystem.SetUiState(lockEntity.Owner, UniversalSecurityUiKey.Lock, state);

        if (_uiSystem.TryOpenUi(lockEntity.Owner, UniversalSecurityUiKey.Lock, args.User))
            args.Handled = true;
    }

    private void OnSetCodeReceived(Entity<UniversalLockComponent> lockEntity, ref UniversalLockSetCodeMessage args)
    {
        if (lockEntity.Comp.IsSetuped)
            return;

        if (!_interactionSystem.InRangeUnobstructed(args.Actor, lockEntity.Owner))
            return;

        if (args.NewCode == null || args.NewCode.Length != lockEntity.Comp.Length)
            return;

        foreach (var tooth in args.NewCode)
        {
            if (tooth < 0 || tooth > lockEntity.Comp.MaxValue)
                return;
        }

        SetLockCode(lockEntity, args.NewCode, args.MaxValue, args.Name);
    }

    public void SetLockCode(Entity<UniversalLockComponent> lockEntity, int[] code, int maxValue, string name = "")
    {
        if (!string.IsNullOrWhiteSpace(name))
            _metaDataSystem.SetEntityName(lockEntity, $"{name} {Name(lockEntity)}");

        lockEntity.Comp.Code = code;
        lockEntity.Comp.IsSetuped = true;
        lockEntity.Comp.Name = name;
        lockEntity.Comp.Length = code.Length;
        lockEntity.Comp.MaxValue = maxValue;

        _audioSystem.PlayPvs(lockEntity.Comp.LockSetupSound, lockEntity);
    }

    public void SetLockCodeFraction(Entity<UniversalLockComponent> lockEntity, int[] code, int maxValue)
    {
        lockEntity.Comp.Code = code;
        lockEntity.Comp.IsSetuped = true;
        lockEntity.Comp.Name = string.Empty;
        lockEntity.Comp.Length = code.Length;
        lockEntity.Comp.MaxValue = maxValue;
    }
}