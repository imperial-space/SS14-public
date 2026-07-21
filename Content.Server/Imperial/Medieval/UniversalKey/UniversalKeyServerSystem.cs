using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Content.Server.DoAfter;
using Content.Server.Hands.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.LockDoor.Components;
using Content.Shared.Imperial.Medieval.UniversalSecurity;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Shared.Audio.Systems;
using Robust.Server.GameObjects;

public sealed class UniversalKeyServerSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalKeyComponent, InteractUsingEvent>(OnKnifeUsingOnKey);
        SubscribeLocalEvent<UniversalKeyComponent, UniversalKeySetCodeMessage>(OnSetCodeReceived);
        SubscribeLocalEvent<UniversalLockComponent, InteractUsingEvent>(OnKeyUsedOnLock);
        SubscribeLocalEvent<UniversalLockComponent, UniversalKeySetupDoAfterEvent>(OnKeySetupDoAfterEvent);
        SubscribeLocalEvent<UniversalKeyComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<UniversalKeyComponent> keyEntity, ref MapInitEvent args)
    {
        if (!TryComp<KeyComponent>(keyEntity, out var keyComponent))
            return;

        var accessId = keyComponent.Accesses.FirstOrDefault();
        if (string.IsNullOrEmpty(accessId))
            return;

        int length = 16;
        int maxValue = 32;
        int[] newCode = UniversalLockableServerSystem.GenerateSecureDeterministicArray(
            accessId,
            UniversalLockableServerSystem.SecretServerKeyBytes,
            maxValue,
            length
        );

        SetupKeyFraction(keyEntity, newCode, maxValue);
    }

    private void OnKeyUsedOnLock(Entity<UniversalLockComponent> lockEntity, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<UniversalKeyComponent>(args.Used, out var universalKeyComponent))
            return;

        if (universalKeyComponent.IsSetuped || !lockEntity.Comp.IsSetuped)
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, universalKeyComponent.DoAfterSetupTime, new UniversalKeySetupDoAfterEvent(), lockEntity, lockEntity, args.Used)
        {
            BreakOnMove = true,
            DistanceThreshold = 2.0f,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true
        };

        if (_doAfterSystem.TryStartDoAfter(doAfterArgs))
        {
            args.Handled = true;
        }
    }

    private void OnKeySetupDoAfterEvent(Entity<UniversalLockComponent> lockEntity, ref UniversalKeySetupDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } used)
            return;

        if (!TryComp<UniversalKeyComponent>(used, out var universalKeyComponent))
            return;

        if (universalKeyComponent.IsSetuped || !lockEntity.Comp.IsSetuped)
            return;

        args.Handled = true;
        universalKeyComponent.Name = lockEntity.Comp.Name;

        SetupKey((used, universalKeyComponent), lockEntity.Comp.Code, lockEntity.Comp.MaxValue);
    }

    private void OnKnifeUsingOnKey(Entity<UniversalKeyComponent> keyEntity, ref InteractUsingEvent args)
    {
        if (args.Handled || keyEntity.Comp.IsSetuped)
            return;

        if (_tags.HasTag(args.Used, "Knife"))
        {
            args.Handled = true;
            OnKnifeUsed(keyEntity, args.User, args.Used);
        }
    }

    private void OnKnifeUsed(Entity<UniversalKeyComponent> keyEntity, EntityUid userUid, EntityUid knifeUid)
    {
        var state = new UniversalKeyBuiState();
        _uiSystem.SetUiState(keyEntity.Owner, UniversalSecurityUiKey.Key, state);

        if (_uiSystem.TryOpenUi(keyEntity.Owner, UniversalSecurityUiKey.Key, userUid))
        {
            keyEntity.Comp.User = userUid;
            keyEntity.Comp.Knife = knifeUid;
        }
    }

    private void OnSetCodeReceived(Entity<UniversalKeyComponent> keyEntity, ref UniversalKeySetCodeMessage args)
    {
        if (keyEntity.Comp.IsSetuped)
        {
            _uiSystem.CloseUi(keyEntity.Owner, UniversalSecurityUiKey.Key);
            return;
        }

        if (!_interactionSystem.InRangeUnobstructed(args.Actor, keyEntity.Owner))
        {
            _uiSystem.CloseUi(keyEntity.Owner, UniversalSecurityUiKey.Key);
            return;
        }

        if (args.NewCode == null || args.NewCode.Length == 0 || args.NewCode.Length > 32)
        {
            _uiSystem.CloseUi(keyEntity.Owner, UniversalSecurityUiKey.Key);
            return;
        }

        foreach (var tooth in args.NewCode)
        {
            if (tooth < 0 || tooth > 32)
            {
                _uiSystem.CloseUi(keyEntity.Owner, UniversalSecurityUiKey.Key);
                return;
            }
        }

        if (!_handsSystem.TryGetActiveItem(args.Actor, out var heldItem) || !_tags.HasTag(heldItem.Value, "Knife"))
        {
            _uiSystem.CloseUi(keyEntity.Owner, UniversalSecurityUiKey.Key);
            return;
        }

        keyEntity.Comp.Name = args.Name;
        SetupKey(keyEntity, args.NewCode, args.NewCode.Max());

        keyEntity.Comp.User = null;
        keyEntity.Comp.Knife = null;
        _uiSystem.CloseUi(keyEntity.Owner, UniversalSecurityUiKey.Key);
    }

    public void SetupKey(Entity<UniversalKeyComponent> keyEntity, int[] code, int maxValue)
    {
        keyEntity.Comp.Code = code;
        keyEntity.Comp.IsSetuped = true;
        keyEntity.Comp.MaxToothValue = maxValue;
        keyEntity.Comp.MaxTeethCount = code.Length;

        _appearanceSystem.SetData(keyEntity, MedievalDoorKeyCheckVisual.State, "key_ready");
        _audioSystem.PlayPvs(keyEntity.Comp.KeySetupSound, keyEntity);

        var finalName = string.IsNullOrEmpty(keyEntity.Comp.Name)
            ? Name(keyEntity)
            : $"{keyEntity.Comp.Name} {Name(keyEntity)}";

        _metaDataSystem.SetEntityName(keyEntity, finalName);
    }

    public void SetupKeyFraction(Entity<UniversalKeyComponent> keyEntity, int[] code, int maxValue)
    {
        keyEntity.Comp.Code = code;
        keyEntity.Comp.IsSetuped = true;
        keyEntity.Comp.MaxToothValue = maxValue;
        keyEntity.Comp.MaxTeethCount = code.Length;
    }
}