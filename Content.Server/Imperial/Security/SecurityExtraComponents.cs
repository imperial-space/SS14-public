using Content.Shared.Imperial.Security;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

// ─── Пейнтбол ──────────────────────────────────────────────────────────────

/// <summary>gun/projectile/automatic/paintball_gun.</summary>
[RegisterComponent]
public sealed partial class PaintballGunComponent : Component
{
    /// <summary>paintball_color: синий, красный, зелёный, жёлтый.</summary>
    [DataField]
    public int ColorIndex;

    /// <summary>emagged: перегруженный компрессор — шарики бьют на 8.</summary>
    [ViewVariables]
    public bool Emagged;
}

/// <summary>projectile/paintball: оставляет пятно краски.</summary>
[RegisterComponent]
public sealed partial class PaintballProjectileComponent : Component
{
    [DataField]
    public EntProtoId Splat = "PaintballSplat";
}

/// <summary>Магазин перцовых шариков — маркер не перекрашивает их.</summary>
[RegisterComponent]
public sealed partial class PepperballMagazineComponent : Component;

// ─── Плакаты розыска ───────────────────────────────────────────────────────

/// <summary>Данные плаката: и у свёрнутого, и у висящего на стене.</summary>
[RegisterComponent]
public sealed partial class WantedPosterComponent : Component
{
    [DataField]
    public string WantedName = string.Empty;

    [DataField]
    public string Description = string.Empty;

    [DataField]
    public string Header = string.Empty;

    [DataField]
    public bool Missing;

    /// <summary>Свёрнутый плакат (obj/item/poster/wanted) или висящий (structure/sign/poster/wanted).</summary>
    [DataField]
    public bool Rolled = true;
}

/// <summary>Принтер плакатов на консоли уголовных записей.</summary>
[RegisterComponent]
public sealed partial class WantedPrinterComponent : Component
{
    [ViewVariables]
    public TimeSpan? PrintAt;

    [ViewVariables]
    public WantedPrintMessage? Pending;
}

// ─── Задания СБ ────────────────────────────────────────────────────────────

public enum SecurityBountyKind : byte
{
    Patrol,
    Contraband,
}

/// <summary>datum/bounty/patrol и datum/bounty/item/contraband.</summary>
public sealed class SecurityBounty
{
    public SecurityBountyKind Kind;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public int Reward;

    // Патруль.
    public EntityUid Grid;
    public EntityCoordinates Center;
    public string Area = string.Empty;
    public int NeededCoverage;
    public readonly Dictionary<Vector2i, int> WalkedTiles = new();
    public Vector2i? LastTile;
    public int Alerted;

    // Контрабанда.
    public int Required;
    public int Shipped;

    public int Progress
    {
        get
        {
            var total = 0;
            foreach (var count in WalkedTiles.Values)
            {
                total += count;
            }

            return total;
        }
    }

    public bool CanClaim => Kind == SecurityBountyKind.Patrol ? Progress >= NeededCoverage : Shipped >= Required;
}

/// <summary>registered_account.civilian_bounty: задание хранится на ID-карте.</summary>
[RegisterComponent]
public sealed partial class SecurityBountyCardComponent : Component
{
    [ViewVariables]
    public SecurityBounty? Current;

    [ViewVariables]
    public List<SecurityBounty>? Options;

    /// <summary>bounty_timer: 5 минут между запросами.</summary>
    [ViewVariables]
    public TimeSpan NextRequest;
}

/// <summary>computer/piratepad_control/civilian.</summary>
[RegisterComponent]
public sealed partial class SecurityBountyConsoleComponent : Component
{
    public const string IdSlot = "bounty_id";

    [ViewVariables]
    public EntityUid? Pad;

    [ViewVariables]
    public string StatusReport = string.Empty;

    /// <summary>warmup_time: 3 секунды до отправки.</summary>
    [ViewVariables]
    public TimeSpan? SendAt;
}

/// <summary>piratepad/civilian.</summary>
[RegisterComponent]
public sealed partial class SecurityBountyPadComponent : Component;

/// <summary>bounty_cube: карго продаёт его за полную награду.</summary>
[RegisterComponent]
public sealed partial class SecurityBountyCubeComponent : Component;
