tiles-zlevel-open-space = открытое пространство

ent-ZOpenSpaceMarker = открытое пространство
    .desc = Смотри под ноги!
ent-ZLadder = лестница
    .desc = Прочная металлическая лестница на соседний этаж. Клик — подняться, Alt-клик — спуститься.
ent-ZStairs = ступеньки
    .desc = Ступени на этаж выше: просто пройдите по ним вперёд, а сверху — шагните с площадки назад.
ent-ZStairsWood = деревянные ступеньки
    .desc = { ent-ZStairs.desc }
ent-ZStairsStone = каменные ступеньки
    .desc = { ent-ZStairs.desc }
ent-ActionZLookUp = Посмотреть вверх
    .desc = Взглянуть вверх сквозь открытое пространство. Нажмите ещё раз, чтобы снова смотреть прямо.
ent-ActionZLookDown = Посмотреть вниз
    .desc = Взглянуть вниз сквозь открытое пространство или стеклянный пол. Нажмите ещё раз, чтобы снова смотреть прямо.

zlevel-look-straight = Вы снова смотрите прямо.
zlevel-nothing-above = Выше нет ничего интересного. Лучше смотреть перед собой.
zlevel-nothing-below = Ниже нет ничего интересного. Лучше смотреть перед собой.
zlevel-tilt-up = Вы поднимаете голову.
zlevel-tilt-down = Вы опускаете голову.
zlevel-nothing-interesting = Там нет ничего интересного.
zlevel-cant-see-above = Сквозь потолок над вами ничего не видно.
zlevel-cant-see-below = Сквозь пол под вами ничего не видно.

zlevel-falls-through = { $name } проваливается вниз!
zlevel-crash-into-others = { $name } с грохотом падает сверху!
zlevel-crash-into-self = Вы с грохотом падаете на пол!
zlevel-fall-down-stairs = Вы кубарем катитесь вниз по ступенькам!

zlevel-ladder-climb-up = Подняться
zlevel-ladder-climb-down = Спуститься
zlevel-ladder-examine = [color=cyan]Клик[/color] — подняться или спуститься (если можно и туда, и туда — меню выбора); [color=cyan]Alt-клик[/color] — спуститься.
zlevel-ladder-nowhere = Никуда не ведёт!
zlevel-ladder-no-further-up = Выше подняться нельзя
zlevel-ladder-no-further-down = Ниже спуститься нельзя
zlevel-ladder-buckled = Вы пристёгнуты к чему-то закреплённому!
zlevel-ladder-climbing-up = Взбирается наверх...
zlevel-ladder-climbing-down = Спускается вниз...
zlevel-ladder-nothing-that-way = Туда ничего не ведёт!
zlevel-ladder-climbs-up = { $user } взбирается по лестнице наверх.
zlevel-ladder-climbs-down = { $user } спускается по лестнице вниз.
zlevel-ladder-climbed-up = Поднялся
zlevel-ladder-climbed-down = Спустился

cmd-zlevel-desc = Маппинг этажей (z-уровней) как в SS13.
cmd-zlevel-help = zlevel info | set <стопка> <этаж> | newabove | newbelow | up | down | link <MapId ниже> <MapId выше> | save <путь.yml> | load <путь.yml>
cmd-zlevel-no-map = Вы не на карте.
cmd-zlevel-info-none = Эта карта не входит ни в одну стопку этажей.
cmd-zlevel-set-done = Карта стала этажом { $level } стопки «{ $stack }».
cmd-zlevel-exists = С этой стороны этаж уже есть.
cmd-zlevel-created = Создан этаж { $level } (MapId { $map }).
cmd-zlevel-no-such-map = Такой карты нет.
cmd-zlevel-linked = Карта { $above } теперь над картой { $below }.
cmd-zlevel-saved = Сохранено: { $path }
cmd-zlevel-save-failed = Не удалось сохранить { $path }.
cmd-zlevel-load-failed = Не удалось загрузить { $path }.
cmd-zlevel-loaded = Загружено этажей: { $count }, нижний — MapId { $map }.

ent-ZVent = межэтажная вентиляция
    .desc = Широкая вентиляционная шахта на соседний этаж. Пролезть можно, но не быстро.
zlevel-vent-climb-up = Пролезть наверх
zlevel-vent-climb-down = Пролезть вниз
zlevel-vent-examine = [color=cyan]Клик[/color] — пролезть наверх; [color=cyan]Alt-клик[/color] — пролезть вниз.
zlevel-vent-nowhere = Шахта никуда не ведёт!
zlevel-vent-no-further-up = Выше шахта не ведёт
zlevel-vent-no-further-down = Ниже шахта не ведёт
zlevel-vent-buckled = Вы пристёгнуты к чему-то закреплённому!
zlevel-vent-climbing-up = Лезет в шахту наверх...
zlevel-vent-climbing-down = Лезет в шахту вниз...
zlevel-vent-nothing-that-way = Туда шахта не ведёт!
zlevel-vent-climbs-up = { $user } забирается в шахту и ползёт наверх.
zlevel-vent-climbs-down = { $user } забирается в шахту и ползёт вниз.
zlevel-vent-climbed-up = Вылез наверх
zlevel-vent-climbed-down = Вылез внизу

ent-ZElevator = грузовой лифт
    .desc = Платформа 3×3: возит людей и грузы между этажами.
ent-ZElevatorButton = кнопка вызова лифта
    .desc = Вызывает грузовой лифт на этот этаж.
zlevel-elevator-up = Поднять лифт
zlevel-elevator-down = Опустить лифт
zlevel-elevator-examine = [color=cyan]Клик[/color] — вверх; [color=cyan]Alt-клик[/color] — вниз. Везёт всё, что стоит на платформе.
zlevel-elevator-busy = Лифт уже едет.
zlevel-elevator-top = Выше лифт не поднимется.
zlevel-elevator-bottom = Ниже лифт не опустится.
zlevel-elevator-blocked = Шахта перекрыта!
zlevel-elevator-starts = Лифт с лязгом трогается.
zlevel-elevator-arrived = Лифт прибыл.
zlevel-elevator-none = Рядом нет лифта.
zlevel-elevator-here = Лифт уже здесь.
zlevel-elevator-called = Лифт вызван.
zlevel-collapse-warning = Пол угрожающе трещит!
ent-ZElevator5 = большой грузовой лифт
    .desc = Платформа 5×5 для тяжёлых грузов.
ent-ZElevator7 = товарный лифт
    .desc = Огромная платформа 7×7: возит технику и целые отряды.
zlevel-stairs-examine = Пройдите по ступенькам [color=cyan]вперёд[/color] (по стрелке рисунка) и сойдите с последней — окажетесь этажом выше. Сверху спуск — шаг назад с площадки над последней ступенькой.
zlevel-stairs-ok = [color=green]Наверху есть куда выйти.[/color]
zlevel-stairs-blocked = [color=red]Подняться нельзя: { $reason }[/color]
zlevel-stairs-no-level = выше нет этажа.
zlevel-stairs-ceiling = над ступенькой стена.
zlevel-stairs-wall = наверху впереди стена.
zlevel-stairs-no-floor = наверху впереди нет пола — постройте площадку перед ступеньками.
ent-ActionZMoveUp = Подняться на этаж
    .desc = Переместиться этажом выше. Призраку и ИИ перекрытия не мешают, на джетпаке — только сквозь проём.
ent-ActionZMoveDown = Опуститься на этаж
    .desc = Переместиться этажом ниже. Призраку и ИИ перекрытия не мешают, на джетпаке — только сквозь проём.
ent-ZCableHubHV = межэтажный щиток ВВ
    .desc = Соединяет высоковольтные кабели этажа с этажами выше и ниже. Ставится на кабель, щитки — друг над другом.
ent-ZCableHubMV = межэтажный щиток СВ
    .desc = Соединяет средневольтные кабели этажа с этажами выше и ниже. Ставится на кабель, щитки — друг над другом.
ent-ZCableHubLV = межэтажный щиток НВ
    .desc = Соединяет низковольтные кабели (ЛКП) этажа с этажами выше и ниже. Ставится на кабель, щитки — друг над другом.
ent-ZPipeAdapter = межэтажный переходник труб
    .desc = Соединяет трубы этажа с этажами выше и ниже. Переходники ставятся друг над другом.
ent-ZDisposalLift = стояк мусоропровода
    .desc = Вертикальная труба мусоропровода на соседний этаж. Стояки ставятся друг над другом.
ent-ZClimbingHook = крюк-кошка
    .desc = Крепкий крюк на верёвке. Забросьте его в проём над собой и вылезьте этажом выше.
zlevel-fly-up = Вы поднимаетесь этажом выше.
zlevel-fly-down = Вы опускаетесь этажом ниже.
zlevel-fly-blocked-up = Над вами перекрытие — нужен проём.
zlevel-fly-blocked-down = Под вами пол — нужен проём.
zlevel-hook-no-hole = Над вами нет проёма, за который можно зацепиться.
zlevel-hook-throw = { $user } забрасывает крюк-кошку наверх.
zlevel-hook-climbed = Вы вылезаете наверх.
zlevel-speech-from-above = [italic]{ $name } (сверху): «{ $message }»[/italic]
zlevel-speech-from-below = [italic]{ $name } (снизу): «{ $message }»[/italic]
