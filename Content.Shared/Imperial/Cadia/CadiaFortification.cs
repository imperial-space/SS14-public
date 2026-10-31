using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Укрытие: мешки с песком. Стрелок, стоящий вплотную, стреляет поверх, а издали пули то застревают, то проходят.
/// </summary>
[RegisterComponent]
public sealed partial class CadiaCoverComponent : Component
{
    /// <summary>Какая доля пуль издали застревает в укрытии (0..1).</summary>
    [DataField]
    public float Block = 0.65f;

    /// <summary>С какого расстояния стрелок уже стреляет поверх.</summary>
    [DataField]
    public float OverRange = 1.7f;

    /// <summary>ДОТ, которому принадлежат стены: изнутри пули проходят всегда, снаружи почти всегда застревают.</summary>
    [ViewVariables]
    public EntityUid? Inside;
}

/// <summary>ДОТ: коробка со стенами-фикстурами, ставит на свой купол орудие. Своим внутри крыша полупрозрачна.</summary>
[RegisterComponent]
public sealed partial class CadiaPillboxComponent : Component
{
    [DataField]
    public EntProtoId GunProto = "CadiaPillboxGun";

    /// <summary>Где на спрайте купол, относительно центра коробки.</summary>
    [DataField]
    public System.Numerics.Vector2 GunOffset = new(0f, 0.69f);

    /// <summary>Половина внутреннего помещения: кто внутри, стреляет наружу и видит сквозь крышу.</summary>
    [DataField]
    public float InteriorHalf = 1.25f;

    [ViewVariables]
    public EntityUid? Gun;
}

/// <summary>Часть стены ДОТа: гранату можно закинуть внутрь через неё.</summary>
[RegisterComponent]
public sealed partial class CadiaPillboxPartComponent : Component
{
    [ViewVariables]
    public EntityUid? Center;
}

/// <summary>Танк давит это гусеницами: колючая проволока, колья.</summary>
[RegisterComponent]
public sealed partial class CadiaTankCrushableComponent : Component
{
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/metal_break1.ogg");
}

/// <summary>Противотанковая мина: пехота проходит, под гусеницами взрывается.</summary>
[RegisterComponent]
public sealed partial class CadiaTankMineComponent : Component
{
    [DataField]
    public float Intensity = 260f;
}

/// <summary>Склад: выдаёт по одному предмету рукой. Ящик боеприпасов выдаёт обойму к оружию в руках.</summary>
[RegisterComponent]
public sealed partial class CadiaSupplyComponent : Component
{
    [DataField]
    public int Charges = 20;

    /// <summary>Что выдавать; пусто — обойму к оружию в руках.</summary>
    [DataField]
    public List<EntProtoId> Items = new();

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/rustle1.ogg");

    [DataField]
    public LocId EmptyPopup = "cadia-supply-empty";

    [DataField]
    public bool DeleteWhenEmpty;
}

/// <summary>Участок окопа.</summary>
[RegisterComponent]
public sealed partial class CadiaTrenchComponent : Component;

/// <summary>Лопатка: копает окоп на голой земле.</summary>
[RegisterComponent]
public sealed partial class CadiaEntrenchingToolComponent : Component
{
    [DataField]
    public TimeSpan DigTime = TimeSpan.FromSeconds(5);
}

/// <summary>Боец в окопе: большая часть пуль издали проходит над головой.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaInTrenchComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Sections;
}

[Serializable, NetSerializable]
public sealed partial class CadiaTrenchDigDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetCoordinates Spot;

    public CadiaTrenchDigDoAfterEvent(NetCoordinates spot)
    {
        Spot = spot;
    }

    public override DoAfterEvent Clone() => this;
}

/// <summary>Варп-ловушка Хаоса: срабатывает под врагом.</summary>
[RegisterComponent]
public sealed partial class CadiaWarpTrapComponent : Component;

/// <summary>Алтарь: понемногу питает силу варпа, пока стоит.</summary>
[RegisterComponent]
public sealed partial class CadiaWarpSourceComponent : Component
{
    [DataField]
    public float PerMinute = 2f;

    [ViewVariables]
    public float Accumulated;
}

/// <summary>Медик видит раненых своей стороны и слышит их вызов.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CadiaMedicSightComponent : Component;

[Serializable, NetSerializable]
public enum CadiaScreenFx : byte
{
    PylonFall,
    Prince,
    Exterminatus,
}

/// <summary>Большой момент войны: вспышка и тряска у всех.</summary>
[Serializable, NetSerializable]
public sealed class CadiaScreenFxEvent(CadiaScreenFx kind) : EntityEventArgs
{
    public readonly CadiaScreenFx Kind = kind;
}

/// <summary>Ящик спускается с неба на парашюте. Сервер считает время, клиент рисует спуск.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CadiaSupplyDropComponent : Component
{
    /// <summary>Что появится при посадке: по нему же клиент рисует груз под куполом.</summary>
    [DataField, AutoNetworkedField]
    public string Item = string.Empty;

    [DataField, AutoNetworkedField]
    public TimeSpan Start;

    [DataField, AutoNetworkedField]
    public TimeSpan LandAt;
}

/// <summary>Полевая станция лечения: несколько секунд на месте — раны стянуты.</summary>
[RegisterComponent]
public sealed partial class CadiaHealStationComponent : Component
{
    [DataField]
    public float Heal = 40f;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4);

    [DataField]
    public int Charges = 15;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Items/Medical/ointment_end.ogg");
}

[Serializable, NetSerializable]
public sealed partial class CadiaHealStationDoAfterEvent : SimpleDoAfterEvent;

public sealed class CadiaCoverSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const float TrenchPointBlank = 2.5f;
    private const uint TrenchMissPercent = 55;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaCoverComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<CadiaInTrenchComponent, PreventCollideEvent>(OnTrenchCollide);
    }

    /// <summary>В окопе: кто стреляет вплотную, попадает, а издали больше половины пуль уходит над головой.</summary>
    private void OnTrenchCollide(Entity<CadiaInTrenchComponent> ent, ref PreventCollideEvent args)
    {
        if (!TryComp<ProjectileComponent>(args.OtherEntity, out var projectile))
            return;
        if (projectile.Shooter is { } shooter && !TerminatingOrDeleted(shooter))
        {
            var here = _xform.GetMapCoordinates(ent);
            var from = _xform.GetMapCoordinates(shooter);
            if (here.MapId == from.MapId && (here.Position - from.Position).LengthSquared() <= TrenchPointBlank * TrenchPointBlank)
                return;
        }
        var roll = unchecked((uint) (args.OtherEntity.Id * 2654435761u + ent.Owner.Id * 40503u)) % 100u;
        if (roll < TrenchMissPercent)
            args.Cancelled = true;
    }

    private void OnPreventCollide(Entity<CadiaCoverComponent> ent, ref PreventCollideEvent args)
    {
        if (!TryComp<ProjectileComponent>(args.OtherEntity, out var projectile))
            return;

        // Свой мешок не мешает: кто стоит вплотную, стреляет поверх.
        if (projectile.Shooter is { } shooter && !TerminatingOrDeleted(shooter))
        {
            var here = _xform.GetMapCoordinates(ent);
            var from = _xform.GetMapCoordinates(shooter);
            if (ent.Comp.Inside is { } center && !TerminatingOrDeleted(center))
            {
                var inside = _xform.GetMapCoordinates(center);
                var offset = from.Position - inside.Position;
                if (from.MapId == inside.MapId && Math.Max(Math.Abs(offset.X), Math.Abs(offset.Y)) <= CompOrNull<CadiaPillboxComponent>(center)?.InteriorHalf)
                {
                    args.Cancelled = true;
                    return;
                }
            }
            if (here.MapId == from.MapId && (here.Position - from.Position).LengthSquared() <= ent.Comp.OverRange * ent.Comp.OverRange)
            {
                args.Cancelled = true;
                return;
            }
        }

        // Столкновение проверяется много раз за полёт: решение должно быть одним для этой пары пуля — укрытие.
        var roll = unchecked((uint) (args.OtherEntity.Id * 2654435761u + ent.Owner.Id * 40503u)) % 100u;
        if (roll >= ent.Comp.Block * 100f)
            args.Cancelled = true;
    }
}
