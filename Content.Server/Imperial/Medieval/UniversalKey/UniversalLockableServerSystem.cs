using Content.Shared.Administration.Logs;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.UniversalSecurity;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;
using System.Linq;
using Content.Shared.Imperial.Medieval.Ships.Anchor;
using Robust.Shared.Random;
using Content.Shared.Imperial.LockDoor.Components;
using Content.Server.Imperial.Medieval.UniversalLock;
using Robust.Shared.Timing;
using Robust.Shared.Audio;
using Content.Shared.Lock;
using System.Text;
using System.Security.Cryptography;
using Content.Shared.Imperial.Medieval.Skills;
using Robust.Server.Containers;
using Robust.Shared.Containers;

public sealed class UniversalLockableServerSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly UniversalLockServerSystem _universalLockSystem = default!;
    [Dependency] private readonly LockSystem _lockSystem = default!;
    public static readonly byte[] SecretServerKeyBytes = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalLockableComponent, ActivateInWorldEvent>(OnActivate, before: new[] { typeof(MedievalAnchorSystem), typeof(SharedStorageSystem), typeof(SharedDoorSystem) });
        SubscribeLocalEvent<UniversalLockableComponent, InteractUsingEvent>(OnInteractUsing, before: new[] { typeof(MedievalAnchorSystem), typeof(SharedStorageSystem), typeof(SharedDoorSystem) });
        SubscribeLocalEvent<UniversalLockableComponent, GetVerbsEvent<AlternativeVerb>>(AddAltVerbs);
        SubscribeLocalEvent<UniversalLockableComponent, UniversalLockableDoAfterEvent>(OnLockableDoAfter);
        SubscribeLocalEvent<UniversalLockableComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<UniversalLockableComponent, MapInitEvent>(OnMapInit, after: new[] { typeof(ItemSlotsSystem), typeof(ContainerSystem), typeof(SharedContainerSystem) });
    }

    private void OnMapInit(Entity<UniversalLockableComponent> lockableEntity, ref MapInitEvent args)
    {
        if (!_itemSlots.TryGetSlot(lockableEntity, "lockSlot", out var slot) || slot.Item is not { } lockUid)
            return;

        if (!TryComp<LockDoorComponent>(lockableEntity, out var lockDoorComponent))
        {
            QueueDel(lockUid);
            return;
        }

        var accessId = lockDoorComponent.AccessLists.FirstOrDefault();
        if (string.IsNullOrEmpty(accessId))
            return;

        if (!TryComp<UniversalLockComponent>(lockUid, out var lockComponent))
            return;

        int length = 16;
        int maxValue = 32;
        int[] newCode = GenerateSecureDeterministicArray(accessId, SecretServerKeyBytes, maxValue, length);

        _universalLockSystem.SetLockCodeFraction((lockUid, lockComponent), newCode, maxValue);
        _itemSlots.TryInsert(lockableEntity, slot, lockUid, null, true);

        if (TryComp<DoorBoltComponent>(lockableEntity, out var doorBoltComponent))
        {
            if (doorBoltComponent.BoltsDown)
                OnFractionLockSpawn((lockUid, lockComponent), lockableEntity, slot);

            RemComp(lockableEntity, doorBoltComponent);
        }
    }

    public static int[] GenerateSecureDeterministicArray(string factionId, byte[] secretKeyBytes, int maxValue, int length)
    {
        if (length <= 0) return Array.Empty<int>();
        if (maxValue < 0) maxValue = 0;

        int[] result = new int[length];

        int saltByteCount = Encoding.UTF8.GetByteCount(factionId);
        Span<byte> saltSpan = stackalloc byte[saltByteCount];
        Encoding.UTF8.GetBytes(factionId, saltSpan);

        using (var kdf = new Rfc2898DeriveBytes(secretKeyBytes, saltSpan.ToArray(), iterations: 1, HashAlgorithmName.SHA256))
        {
            byte[] buffer = kdf.GetBytes(length * 4);

            for (int i = 0; i < length; i++)
            {
                int rawRandom = BitConverter.ToInt32(buffer, i * 4) & int.MaxValue;
                result[i] = rawRandom % (maxValue + 1);
            }
        }
        return result;
    }

    private void OnActivate(Entity<UniversalLockableComponent> entity, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (IsLocked(entity))
        {
            var audioParams = new AudioParams { Volume = -10 };
            _audioSystem.PlayPvs(entity.Comp.ActivateInWorldDenySound, entity, audioParams);
            args.Handled = true;
        }
    }

    private void OnInteractUsing(Entity<UniversalLockableComponent> lockableEntity, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<UniversalKeyComponent>(args.Used, out var universalKeyComponent))
        {
            if (!universalKeyComponent.IsSetuped)
                return;

            OnUsedKey((args.Used, universalKeyComponent), lockableEntity, args.User);
            args.Handled = true;
            return;
        }

        if (TryComp<UniversalLockpickComponent>(args.Used, out var lockpickComponent))
            return;

        if (IsLocked(lockableEntity))
        {
            var audioParams = new AudioParams { Volume = -10 };
            _audioSystem.PlayPvs(lockableEntity.Comp.InteractUsingDenySound, lockableEntity, audioParams);
            args.Handled = true;
        }
    }

    private void AddAltVerbs(Entity<UniversalLockableComponent> lockableEntity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_itemSlots.TryGetSlot(lockableEntity, "lockSlot", out var slot) || slot.Item is not { } lockUid)
            return;

        if (!TryComp<UniversalLockComponent>(lockUid, out var lockComponent))
            return;

        if (!TryComp<SkillsComponent>(args.User, out var skillsComponent))
            return;

        if (IsLocked(lockableEntity))
            return;

        var user = args.User;
        float agility = skillsComponent.Levels["Agility"];
        if (agility <= 0) agility = 1f;

        var time = slot.Locked ? (lockComponent.TimeToEject * (10f / agility)) : 0.1f;
        time = Math.Clamp(time, 0.1f, 64f);

        AlternativeVerb verb = new()
        {
            Text = Loc.GetString("universal-security-eject-lock"),
            Act = () =>
            {
                var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(time), new UniversalLockableDoAfterEvent(), lockableEntity)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true,
                    NeedHand = true,
                    BlockDuplicate = true,
                };

                _doAfterSystem.TryStartDoAfter(doAfterArgs);
            }
        };

        args.Verbs.Add(verb);
    }

    private void OnLockableDoAfter(Entity<UniversalLockableComponent> lockableEntity, ref UniversalLockableDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!_itemSlots.TryGetSlot(lockableEntity, "lockSlot", out var slot) || slot.Item is not { } lockUid)
            return;

        _itemSlots.SetLock(lockableEntity, slot, !slot.Locked);

        if (!slot.Locked)
            _itemSlots.TryEjectToHands(lockUid, slot, args.User);
    }

    private void OnExamine(Entity<UniversalLockableComponent> lockableEntity, ref ExaminedEvent args)
    {
        if (!_itemSlots.TryGetSlot(lockableEntity, "lockSlot", out var slot) || slot.Item is not { } item)
            return;

        if (!TryComp<UniversalLockComponent>(item, out var lockComponent))
            return;

        var msg = new FormattedMessage();
        msg.PushColor(Color.Yellow);
        msg.AddText(Name(item) + " ");

        msg.AddText(lockComponent.IsLocked
            ? Loc.GetString("universal-lock-examine-is-locked")
            : Loc.GetString("universal-lock-examine-is-unlocked"));

        msg.Pop();
        msg.AddText("\n");

        args.AddMessage(msg);
    }

    private bool IsLocked(Entity<UniversalLockableComponent> entity)
    {
        if (!_itemSlots.TryGetSlot(entity, "lockSlot", out var slot) || slot.Item is not { } lockUid)
            return false;

        return TryComp<UniversalLockComponent>(lockUid, out var lockComp) && lockComp.IsLocked;
    }

    private void OnUsedKey(Entity<UniversalKeyComponent> keyUsedEntity, Entity<UniversalLockableComponent> lockableEntity, EntityUid user)
    {
        var keyComp = keyUsedEntity.Comp;

        if (!_itemSlots.TryGetSlot(lockableEntity, "lockSlot", out var slot) || slot.Item is not { } lockUid)
            return;

        if (!TryComp<UniversalLockComponent>(lockUid, out var lockComp))
            return;

        if (keyComp.IsSuperKey || keyComp.Code.SequenceEqual(lockComp.Code))
            OnUsedKeySuccess((lockUid, lockComp), lockableEntity, slot, user);
        else
            OnUsedKeyFail();
    }

    public void OnUsedKeySuccess(Entity<UniversalLockComponent> lockEntity, Entity<UniversalLockableComponent> lockableEntity, ItemSlot slot, EntityUid? user)
    {
        lockEntity.Comp.IsLocked = !lockEntity.Comp.IsLocked;
        _itemSlots.SetLock(lockableEntity, slot, true);

        if (!lockEntity.Comp.IsLocked)
        {
            _audioSystem.PlayPvs(lockableEntity.Comp.LockUnlockedSound, lockableEntity);
            _popupSystem.PopupClient(Loc.GetString("universal-lock-unlocked-popup"), user);
        }
        else
        {
            _audioSystem.PlayPvs(lockableEntity.Comp.LockLockedSound, lockableEntity);
            _popupSystem.PopupClient(Loc.GetString("universal-lock-locked-popup"), user);
        }

        if (TryComp<LockComponent>(lockableEntity, out var lockComponent))
            if (lockComponent.Locked != lockEntity.Comp.IsLocked)
                _lockSystem.ToggleLock(lockableEntity, null, lockComponent);
    }

    public void OnFractionLockSpawn(Entity<UniversalLockComponent> lockEntity, Entity<UniversalLockableComponent> lockableEntity, ItemSlot slot)
    {
        lockEntity.Comp.IsLocked = true;
        _itemSlots.SetLock(lockableEntity, slot, true);
    }

    private void OnUsedKeyFail()
    {
    }
}