using Content.Server.Imperial.Xenobiology.Components;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Server.Imperial.Xenobiology.Systems;

/// <summary>
/// Система кроссбридинга ксено-слаймов.
///
/// Взрослый слайм с компонентом XenoSlimeCrossbreedComponent периодически ищет
/// экстракты (XenoSlimeExtractComponent) рядом с собой и поглощает их.
/// При достижении ExtractThreshold экстрактов одного цвета — слайм гибнет,
/// спавня XenoChargedSlimeCore с обоими цветами.
/// </summary>
public sealed class XenoSlimeCrossbreedSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup    = default!;
    [Dependency] private readonly SharedPopupSystem  _popup     = default!;
    [Dependency] private readonly TransformSystem    _transform = default!;

    // Соответствие прототипа → цвет (синхронизировано с ExtractProtos в CrusherSystem)
    private static readonly Dictionary<string, XenoSlimeColor> ExtractColorMap = new()
    {
        { "XenoSlimeExtractGrey",        XenoSlimeColor.Grey        },
        { "XenoSlimeExtractOrange",      XenoSlimeColor.Orange      },
        { "XenoSlimeExtractPurple",      XenoSlimeColor.Purple      },
        { "XenoSlimeExtractBlue",        XenoSlimeColor.Blue        },
        { "XenoSlimeExtractMetal",       XenoSlimeColor.Metal       },
        { "XenoSlimeExtractYellow",      XenoSlimeColor.Yellow      },
        { "XenoSlimeExtractDarkPurple",  XenoSlimeColor.DarkPurple  },
        { "XenoSlimeExtractDarkBlue",    XenoSlimeColor.DarkBlue    },
        { "XenoSlimeExtractSilver",      XenoSlimeColor.Silver      },
        { "XenoSlimeExtractBluespace",   XenoSlimeColor.Bluespace   },
        { "XenoSlimeExtractSepia",       XenoSlimeColor.Sepia       },
        { "XenoSlimeExtractCerulean",    XenoSlimeColor.Cerulean    },
        { "XenoSlimeExtractPyrite",      XenoSlimeColor.Pyrite      },
        { "XenoSlimeExtractGreen",       XenoSlimeColor.Green       },
        { "XenoSlimeExtractRed",         XenoSlimeColor.Red         },
        { "XenoSlimeExtractPink",        XenoSlimeColor.Pink        },
        { "XenoSlimeExtractGold",        XenoSlimeColor.Gold        },
        { "XenoSlimeExtractOil",         XenoSlimeColor.Oil         },
        { "XenoSlimeExtractBlack",       XenoSlimeColor.Black       },
        { "XenoSlimeExtractLightPink",   XenoSlimeColor.LightPink   },
        { "XenoSlimeExtractAdamantine",  XenoSlimeColor.Adamantine  },
        { "XenoSlimeExtractRainbow",     XenoSlimeColor.Rainbow     },
    };

    // Прототипы ядер по цвету слайма
    private static readonly Dictionary<XenoSlimeColor, string> CoreProtos = new()
    {
        { XenoSlimeColor.Grey,       "XenoChargedCorGrey"       },
        { XenoSlimeColor.Orange,     "XenoChargedCorOrange"     },
        { XenoSlimeColor.Purple,     "XenoChargedCorPurple"     },
        { XenoSlimeColor.Blue,       "XenoChargedCorBlue"       },
        { XenoSlimeColor.Metal,      "XenoChargedCorMetal"      },
        { XenoSlimeColor.Yellow,     "XenoChargedCoreYellow"    }, // Существующий
        { XenoSlimeColor.DarkPurple, "XenoChargedCorDkPurple"   },
        { XenoSlimeColor.DarkBlue,   "XenoChargedCorDkBlue"     },
        { XenoSlimeColor.Silver,     "XenoChargedCorSilver"     },
        { XenoSlimeColor.Bluespace,  "XenoChargedCorBluespace"  },
        { XenoSlimeColor.Sepia,      "XenoChargedCorSepia"      },
        { XenoSlimeColor.Cerulean,   "XenoChargedCorCerulean"   },
        { XenoSlimeColor.Pyrite,     "XenoChargedCorPyrite"     },
        { XenoSlimeColor.Green,      "XenoChargedCorGreen"      },
        { XenoSlimeColor.Red,        "XenoChargedCorRed"        },
        { XenoSlimeColor.Pink,       "XenoChargedCorPink"       },
        { XenoSlimeColor.Gold,       "XenoChargedCorGold"       },
        { XenoSlimeColor.Oil,        "XenoChargedCorOil"        },
        { XenoSlimeColor.Black,      "XenoChargedCorBlack"      },
        { XenoSlimeColor.LightPink,  "XenoChargedCorLightPink"  },
        { XenoSlimeColor.Adamantine, "XenoChargedCorAdamantine" },
        { XenoSlimeColor.Rainbow,    "XenoChargedCorRainbow"    },
    };

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<XenoSlimeCrossbreedComponent, XenoSlimeComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var crossbreed, out var slime, out var mob))
        {
            // Только живые взрослые слаймы
            if (mob.CurrentState == MobState.Dead || !slime.IsAdult)
                continue;

            crossbreed.ScanTimer -= frameTime;
            if (crossbreed.ScanTimer > 0f)
                continue;

            crossbreed.ScanTimer = crossbreed.ScanInterval;
            ScanAndAbsorbExtracts(uid, crossbreed, slime);
        }
    }

    private void ScanAndAbsorbExtracts(EntityUid uid, XenoSlimeCrossbreedComponent crossbreed, XenoSlimeComponent slime)
    {
        var coords = _transform.GetMapCoordinates(uid);

        // Ищем экстракты рядом
        var found = new HashSet<Entity<XenoSlimeExtractComponent>>();
        _lookup.GetEntitiesInRange(coords, crossbreed.ExtractSearchRange, found,
            flags: LookupFlags.Uncontained);

        foreach (var (extractUid, extractComp) in found)
        {
            // Определяем цвет экстракта через MetaData → прототип
            var meta = MetaData(extractUid);
            if (meta.EntityPrototype == null)
                continue;

            if (!ExtractColorMap.TryGetValue(meta.EntityPrototype.ID, out var extractColor))
                continue;

            // Не поедаем экстракт своего цвета (нужен другой цвет для кроссбридинга)
            if (extractColor == slime.Color)
                continue;

            // Поглощаем экстракт
            crossbreed.ExtractCounts.TryGetValue(extractColor, out var count);
            count++;
            crossbreed.ExtractCounts[extractColor] = count;
            QueueDel(extractUid);

            // Показываем прогресс игрокам рядом
            _popup.PopupCoordinates(
                $"Слайм поглотил экстракт... ({count}/{crossbreed.ExtractThreshold})",
                _transform.GetMoverCoordinates(uid),
                PopupType.Small);

            // Проверяем достижение порога
            if (count >= crossbreed.ExtractThreshold)
            {
                CreateChargedCore(uid, slime, extractColor, crossbreed);
                return; // Слайм гибнет, прекращаем итерацию
            }

            break; // Один экстракт за тик
        }
    }

    private void CreateChargedCore(EntityUid uid, XenoSlimeComponent slime, XenoSlimeColor extractColor, XenoSlimeCrossbreedComponent crossbreed)
    {
        var coords = _transform.GetMapCoordinates(uid);

        // Определяем прототип ядра по цвету слайма
        if (!CoreProtos.TryGetValue(slime.Color, out var coreProto))
            coreProto = "XenoChargedCoreYellow"; // Fallback

        // Спавним ядро
        var core = Spawn(coreProto, coords);

        // Устанавливаем оба цвета
        if (TryComp<XenoChargedSlimeCoreComponent>(core, out var coreComp))
        {
            coreComp.SlimeColor    = slime.Color;
            coreComp.ExtractColor  = extractColor;
        }

        // Показываем сообщение
        _popup.PopupCoordinates(
            $"Слайм формирует заряженное ядро!",
            _transform.GetMoverCoordinates(uid),
            PopupType.LargeCaution);

        // Слайм гибнет
        QueueDel(uid);
    }
}
