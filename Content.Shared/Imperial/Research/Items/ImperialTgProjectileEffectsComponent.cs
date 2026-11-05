using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Особые эффекты пули TG при попадании.</summary>
[RegisterComponent]
public sealed partial class ImperialTgProjectileEffectsComponent : Component
{
    /// <summary>Целевая температура тела (Iceblox: 100 K).</summary>
    [DataField]
    public float? BodyTemperature;

    /// <summary>Имплант, вживляемый цели, если его ещё нет (TRAC).</summary>
    [DataField]
    public EntProtoId? Implant;

    /// <summary>Подсветка цели (Flare), секунды.</summary>
    [DataField]
    public float GlowTime;

    [DataField]
    public Color GlowColor = Color.FromHex("#ff9933");

    /// <summary>Удар током (тесла-пушка).</summary>
    [DataField]
    public int Shock;

    /// <summary>Отбрасывание на тайлы (метеоритная пуля).</summary>
    [DataField]
    public float Knockback;

    /// <summary>Что появляется на месте попадания (разрыв реальности «Горизонта событий»).</summary>
    [DataField]
    public EntProtoId? SpawnOnHit;
}
