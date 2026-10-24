using System.Linq;
using Content.Server.Administration;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Popups;
using Content.Shared.Turrets;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.StationAi;
using Content.Shared.Inventory;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StationAi;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.StationAi.Malf;

/// <summary>Улучшения (upgrade_cameras, upgrade_turrets, eavesdrop, voice_changer) и роботизированная фабрика.</summary>
public sealed partial class ImperialMalfAiSystem
{
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly QuickDialogSystem _dialog = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private static readonly EntProtoId FactoryBorgProto = "PlayerBorgBattery";
    private static readonly EntProtoId HeavyLaserProto = "BulletLaserMagnum";
    private static readonly TimeSpan FactoryCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan UpgradeInterval = TimeSpan.FromSeconds(5);

    private static readonly System.Reflection.FieldInfo OccludedField =
        typeof(StationAiVisionComponent).GetField(nameof(StationAiVisionComponent.Occluded))!;

    private TimeSpan _nextUpgradeCheck;

    private void InitializeUpgrades()
    {
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfVoiceChangerActionEvent>(OnVoiceChanger);
        SubscribeLocalEvent<ImperialMalfAiComponent, TransformSpeakerNameEvent>(OnSpeakerName);
        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnEavesdrop);
    }

    /// <summary>upgrade(): эффект улучшения при покупке.</summary>
    private void ApplyUpgrade(Entity<ImperialMalfAiComponent> ent, ImperialMalfModulePrototype module)
    {
        switch (module.ID)
        {
            case "ImperialMalfUpgradeCameras":
                ent.Comp.XrayCameras = true;
                UpgradeCameras(ent);
                break;
            case "ImperialMalfUpgradeTurrets":
                ent.Comp.HeavyTurrets = true;
                UpgradeTurrets(ent);
                break;
            case "ImperialMalfEavesdrop":
                ent.Comp.Eavesdrop = true;
                break;
            case "ImperialMalfDoomsday":
                DoomsdayBought(ent);
                break;
        }
    }

    /// <summary>upgrade_cameras: все камеры станции видят сквозь стены (рентген) и включаются.</summary>
    private void UpgradeCameras(Entity<ImperialMalfAiComponent> ent)
    {
        var query = EntityQueryEnumerator<StationAiVisionComponent>();
        while (query.MoveNext(out var uid, out var vision))
        {
            if (!vision.Occluded || HasComp<StationAiCoreComponent>(uid) || !OnStation(ent, uid))
                continue;

            OccludedField.SetValue(vision, false);
            Dirty(uid, vision);
        }
    }

    /// <summary>upgrade_turrets: турели станции починены, летальный режим — тяжёлый лазер.</summary>
    private void UpgradeTurrets(Entity<ImperialMalfAiComponent> ent)
    {
        var query = EntityQueryEnumerator<StationAiTurretComponent, BatteryWeaponFireModesComponent>();
        while (query.MoveNext(out var uid, out _, out var modes))
        {
            if (!OnStation(ent, uid))
                continue;

            if (TryComp<DamageableComponent>(uid, out var damageable))
                _damage.SetAllDamage((uid, damageable), 0);

            var changed = false;
            foreach (var mode in modes.FireModes)
            {
                if (mode.Prototype == HeavyLaserProto || !mode.Prototype.Id.Contains("Laser", StringComparison.Ordinal))
                    continue;

                mode.Prototype = HeavyLaserProto;
                changed = true;
            }

            if (!changed)
                continue;

            Dirty(uid, modes);
            if (TryComp<BatteryAmmoProviderComponent>(uid, out var provider) && provider.Prototype.Id.Contains("Laser", StringComparison.Ordinal))
            {
                provider.Prototype = HeavyLaserProto;
                Dirty(uid, provider);
            }
        }
    }

    /// <summary>eavesdrop: ИИ слышит речь рядом с любой камерой.</summary>
    private void OnEavesdrop(ExpandICChatRecipientsEvent ev)
    {
        var listeners = new List<(ICommonSession Session, EntityUid Ai)>();
        var query = EntityQueryEnumerator<ImperialMalfAiComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out var malf, out var actor))
        {
            if (malf.Eavesdrop && !ev.Recipients.ContainsKey(actor.PlayerSession))
                listeners.Add((actor.PlayerSession, uid));
        }

        if (listeners.Count == 0)
            return;

        var source = _transform.GetMapCoordinates(ev.Source);
        var nearest = float.MaxValue;
        foreach (var camera in _lookup.GetEntitiesInRange<StationAiVisionComponent>(source, ev.VoiceRange))
        {
            if (!camera.Comp.Enabled || HasComp<StationAiCoreComponent>(camera))
                continue;

            var distance = (_transform.GetWorldPosition(camera) - source.Position).Length();
            nearest = Math.Min(nearest, distance);
        }

        if (nearest > ev.VoiceRange)
            return;

        foreach (var (session, ai) in listeners)
        {
            if (OnStation(ai, ev.Source))
                ev.Recipients.TryAdd(session, new ChatSystem.ICChatRecipientData(nearest, false));
        }
    }

    /// <summary>voice_changer: ИИ говорит чужим именем.</summary>
    private void OnVoiceChanger(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfVoiceChangerActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;
        _dialog.OpenDialog<string>(actor.PlayerSession, Loc.GetString("imperial-malf-voice-title"),
            Loc.GetString("imperial-malf-voice-prompt"), name =>
            {
                if (!TryComp<ImperialMalfAiComponent>(ent, out var malf))
                    return;

                name = name.Trim();
                malf.VoiceName = name.Length == 0 ? null : name[..Math.Min(name.Length, 32)];
                Notify(ent, malf.VoiceName == null
                    ? Loc.GetString("imperial-malf-voice-reset")
                    : Loc.GetString("imperial-malf-voice-set", ("name", malf.VoiceName)));
            });
    }

    private void OnSpeakerName(Entity<ImperialMalfAiComponent> ent, ref TransformSpeakerNameEvent args)
    {
        if (ent.Comp.VoiceName is { } name)
            args.VoiceName = name;
    }

    private void UpdateUpgrades()
    {
        if (_timing.CurTime < _nextUpgradeCheck)
            return;

        _nextUpgradeCheck = _timing.CurTime + UpgradeInterval;

        // Новые камеры и турели (построенные после покупки) тоже улучшаются.
        var query = EntityQueryEnumerator<ImperialMalfAiComponent>();
        while (query.MoveNext(out var uid, out var malf))
        {
            if (malf.XrayCameras)
                UpgradeCameras((uid, malf));
            if (malf.HeavyTurrets)
                UpgradeTurrets((uid, malf));
        }
    }

    /// <summary>
    /// transformer: человек, лежащий на ленте фабрики, становится киборгом под управлением ИИ.
    /// Снаряжение падает рядом, тело исчезает, разум переходит в шасси.
    /// </summary>
    private void UpdateFactories()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialMalfFactoryComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var factory, out var xform))
        {
            if (now < factory.NextUse)
                continue;

            foreach (var victim in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(xform.Coordinates, 1.4f))
            {
                if (!HasComp<MobStateComponent>(victim) || _mobs.IsDead(victim))
                    continue;

                var victimPos = _transform.GetWorldPosition(victim);
                var center = _transform.GetWorldPosition(uid);
                if (Math.Abs(victimPos.Y - center.Y) > 0.6f)
                    continue;

                Robotize(uid, victim);
                factory.NextUse = now + FactoryCooldown;
                break;
            }
        }
    }

    private void Robotize(EntityUid factory, EntityUid victim)
    {
        var coords = Transform(victim).Coordinates;

        if (_inventory.TryGetSlots(victim, out var slots))
        {
            foreach (var slot in slots)
            {
                _inventory.TryUnequip(victim, slot.Name, true, true);
            }
        }

        foreach (var held in _hands.EnumerateHeld(victim).ToList())
        {
            _hands.TryDrop(victim, held, checkActionBlocker: false);
        }

        var borg = Spawn(FactoryBorgProto, coords);
        if (_mind.TryGetMind(victim, out var mindId, out var mind))
            _mind.TransferTo(mindId, borg, mind: mind);

        _meta.SetEntityName(borg, Loc.GetString("imperial-malf-factory-borg-name", ("name", Name(victim))));
        _audio.PlayPvs(BangSound, factory);
        _popup.PopupEntity(Loc.GetString("imperial-malf-factory-converted", ("name", Name(victim))), factory, PopupType.LargeCaution);
        QueueDel(victim);
    }
}
