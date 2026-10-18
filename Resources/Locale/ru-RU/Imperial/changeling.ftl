## Генокрад TG (code/modules/antagonists/changeling)

roles-antag-imperial-changeling-name = Генокрад
roles-antag-imperial-changeling-objective = Поглощайте экипаж, принимайте чужие облики и выполняйте задачи улья.
imperial-changeling-round-end-agent-name = генокрад
imperial-changeling-briefing = Вы — генокрад! Мы поглощаем ДНК других, принимаем их облик и голос. Магазин эволюции открывается кнопкой с пробиркой. Говорить с ульем: «:е».
objective-issuer-imperial-changeling = [color=#9b3fc4]Улей[/color]
chat-radio-imperial-changeling-hive = Улей

## Химикаты и способности

imperial-changeling-on-fire = горим!
imperial-changeling-req-human = станьте гуманоидом!
imperial-changeling-req-chems = требуется { $amount } химикатов!
imperial-changeling-req-dna = требуется { $amount } образцов ДНК!
imperial-changeling-req-absorbs = требуется { $amount } поглощений!
imperial-changeling-incapacitated = недееспособны!
imperial-changeling-faking-death = притворяемся мёртвым!

## Магазин эволюции

ent-ActionImperialChangelingEmporium = Магазин эволюции
    .desc = Здесь мы развиваем новые способности за очки генов.
imperial-changeling-emporium-title = Клеточный магазин
imperial-changeling-emporium-points = Очки генов: { $points }
imperial-changeling-emporium-absorbs = Поглощено: { $absorbs } · ДНК: { $dna }
imperial-changeling-emporium-readapt = Переадаптироваться
imperial-changeling-emporium-readapt-count = Переадаптация ({ $count })
imperial-changeling-emporium-search = Поиск...
imperial-changeling-emporium-info = Улей
imperial-changeling-emporium-cost = Стоимость: { $cost } ДНК
imperial-changeling-emporium-req-absorbs = { $count } поглощений
imperial-changeling-emporium-req-dna = { $count } ДНК
imperial-changeling-emporium-evolve = Развить
imperial-changeling-emporium-owned = (Развито)
imperial-changeling-category-stings = Жала
imperial-changeling-category-combat = Бой
imperial-changeling-category-stealth = Скрытность
imperial-changeling-category-utility = Полезное

imperial-changeling-evolve-already = Мы уже развили эту способность!
imperial-changeling-evolve-points = Мы достигли предела своих возможностей!
imperial-changeling-evolve-dna = У нас недостаточно ДНК, чтобы развить эту способность!
imperial-changeling-evolve-absorbs = Нам недостаточно поглощенных ДНК, чтобы развить эту способность!
imperial-changeling-evolve-cannot = Мы не можем развить эту способность!
imperial-changeling-evolve-deathcoma = Сейчас нам не хватает энергии для развития новых способностей!
imperial-changeling-readapt-form = Мы не можем избавиться от нашего развития в такой форме!
imperial-changeling-readapt-busy = Мы слишком заняты реформированием себя, чтобы переадаптироваться прямо сейчас!
imperial-changeling-readapt-power = Вам не хватает силы, чтобы переадаптировать ваше развитие!
imperial-changeling-readapt-done = Мы избавились от нашего развития в этой форме и теперь готовы к повторной адаптации.

imperial-changeling-info-header = [bold]Мы — { $id }[/bold] из улья [color=#9b3fc4]{ $hive }[/color].
imperial-changeling-info-tips = [color=#a0a0a0]Поглощение даёт новые облики, очки переадаптации и химикаты. Жертву нужно держать в тяге: в захвате за шею или беспомощной (крит, оглушение, наручники). Профилей ДНК — не больше 7, исходный облик не вытесняется.[/color]
imperial-changeling-info-memories = [bold]Украденные воспоминания:[/bold]
imperial-changeling-info-no-memories = Пока мы никого не поглотили.
imperial-changeling-memory-objectives = { $name }, задачи: { $objectives }.

## Поглощение ДНК

imperial-changeling-power-absorb-name = Поглотить ДНК
imperial-changeling-power-absorb-desc = Поглотить ДНК нашей жертвы. Для поглощения нужно душить их.
ent-ActionImperialChangelingAbsorbDna = Поглотить ДНК
    .desc = Поглотить ДНК нашей жертвы. Для поглощения нужно душить их.
imperial-changeling-absorb-busy = уже поглощаем!
imperial-changeling-absorb-grab = нужно взять в захват!
imperial-changeling-absorb-grab-stronger = нужно усилить захват до удушения! (в боевом режиме снова потяните жертву, Ctrl+клик)
imperial-changeling-absorb-full = Мы достигли предела своей способности хранить генетическую информацию! Мы должны трансформироваться, прежде чем поглощать больше.
imperial-changeling-absorb-incompatible = ДНК { $target } несовместимо с нашей биологией.
imperial-changeling-absorb-have = Мы уже имеем это ДНК в нашем хранилище!
imperial-changeling-absorb-husk = Тело { $target } разрушено до непригодности!
imperial-changeling-absorb-lesser = Мы не получим никакой выгоды от поглощения низшего существа.
imperial-changeling-absorb-stage1 = Это существо совместимо. Мы должны стоять неподвижно...
imperial-changeling-absorb-stage2-others = { CAPITALIZE($user) } вытягивает жало-хоботок!
imperial-changeling-absorb-stage2-self = Мы вытягиваем жало-хоботок.
imperial-changeling-absorb-stage3-others = { CAPITALIZE($user) } пронзает { $target } жалом-хоботком!
imperial-changeling-absorb-stage3-self = Мы пронзаем { $target } жалом-хоботком.
imperial-changeling-absorb-stage3-target = Вы чувствуете острую колющую боль!
imperial-changeling-absorb-interrupted = прервано!
imperial-changeling-absorb-done-others = { CAPITALIZE($user) } высасывает жидкости из { $target }!
imperial-changeling-absorb-done-self = Мы поглотили { $target }.
imperial-changeling-absorb-weak = Мы поглощаем { $target }, но слабой ДНК недостаточно, чтобы утолить наш голод.
imperial-changeling-absorb-ling = { CAPITALIZE($target) } был одним из нас. Мы поглотили его силу.

## Превращение

imperial-changeling-power-transform-name = Трансформация
imperial-changeling-power-transform-desc = Мы принимаем облик и голос того, кого поглотили. Стоит 5 химикатов.
ent-ActionImperialChangelingTransform = Трансформация
    .desc = Мы принимаем облик и голос того, кого поглотили. Стоит 5 химикатов.
imperial-changeling-transform-drop-flesh = Сбросить маскировку из плоти

## Регенерация

imperial-changeling-power-regenerate-name = Регенерация
imperial-changeling-power-regenerate-desc = Позволяет восстанавливать объем крови и останавливать кровотечение. Стоит 10 химикатов.
imperial-changeling-power-regenerate-help = Можно использовать в бессознательном состоянии.
ent-ActionImperialChangelingRegenerate = Регенерация
    .desc = Позволяет восстанавливать объем крови и останавливать кровотечение. Стоит 10 химикатов.
imperial-changeling-regenerate = Вы чувствуете зуд, как внутри, так и снаружи, когда ваши ткани плоти вяжутся и перевязываются.

## Возрождающий стазис

imperial-changeling-power-fakedeath-name = Возрождающий стазис
imperial-changeling-power-fakedeath-desc = Мы впадаем в стазис, что позволяет нам регенерировать и обманывать наших врагов. Стоит 15 химикатов.
ent-ActionImperialChangelingFakeDeath = Возрождающий стазис
    .desc = Мы впадаем в стазис, что позволяет нам регенерировать и обманывать наших врагов. Стоит 15 химикатов. Когда возрождение готово — нажмите ещё раз.
imperial-changeling-fakedeath-already = уже возрождаемся!
imperial-changeling-fakedeath-confirm = Уверены ли мы, что хотим инсценировать собственную смерть?
imperial-changeling-fakedeath-start = Мы начинаем стазис, подготавливая энергию, чтобы восстать вновь.
imperial-changeling-fakedeath-ready = Мы готовы возродиться.
imperial-changeling-fakedeath-revived = Мы возродили себя.

## Цели

ent-ImperialChangelingAbsorbObjective = Извлеките совместимые гены.
    .desc = Поглотите ДНК членов экипажа.
imperial-changeling-objective-absorb = Извлеките { $count } совместимых генов.
ent-ImperialChangelingAbsorbMostObjective = Извлеките больше совместимых генов, чем любой другой генокрад.
    .desc = Улей уважает лишь сильнейших.
ent-ImperialChangelingAbsorbLingObjective = Поглотите другого генокрада.
    .desc = Сила другого генокрада станет нашей.
ent-ImperialChangelingKillObjective = { "" }
    .desc = Улей желает смерти этого существа.
ent-ImperialChangelingMaroonObjective = { "" }
    .desc = Цель не должна покинуть станцию живой.
ent-ImperialChangelingPairedMaroonObjective = { "" }
    .desc = Цель не должна покинуть станцию живой — её облик нужен нам.
ent-ImperialChangelingEscapeIdentityObjective = { "" }
    .desc = Мы покинем станцию под чужой личиной.
imperial-changeling-objective-identity = Сбегите на эвакуационном шаттле под личиной { $targetName }, { CAPITALIZE($job) }.
imperial-changeling-objective-identity-id = Сбегите на эвакуационном шаттле под личиной { $targetName }, { CAPITALIZE($job) }, при этом нося ID карту цели.

## Итоги раунда

imperial-changeling-roundend-header = [color=#9b3fc4][bold]Генокрады[/bold][/color]
imperial-changeling-roundend-entry = [bold]{ $name }[/bold] ({ $user }) — извлечённых генов: { $count }
imperial-changeling-roundend-win = [color=green]Генокрад был успешен![/color]
imperial-changeling-roundend-lose = [color=red]Генокрад провалился.[/color]

## Жала

imperial-changeling-sting-set = Готовим жало. Используйте Alt+Клик по цели, чтобы ужалить её.
imperial-changeling-sting-unset = Мы убираем свое жало, пока что мы не можем никого ужалить.
imperial-changeling-sting-verb = Ужалить: { $sting }
imperial-changeling-sting-feedback = Мы незаметно жалим { $target }.
imperial-changeling-sting-feel = Вы чувствуете небольшой укол.
imperial-changeling-sting-dna-incompatible = несовместимое ДНК!
imperial-changeling-sting-already-transformed = уже трансформирован!
imperial-changeling-sting-no-eyes = нет глаз!

imperial-changeling-sting-transformation-name = Трансформирующее жало
imperial-changeling-sting-transformation-desc = Мы тихо жалим организм, вводя в него ретровирус, который заставляет его трансформироваться. Длительность 8 минут, таймер стоит, пока жертва мертва. Стоимость 33 химиката.
imperial-changeling-sting-transformation-help = Жертва трансформируется так же, как генокрад. Для сложных гуманоидов трансформация происходит на время. Не предупреждает других.
ent-ActionImperialChangelingStingTransformation = Трансформирующее жало
    .desc = Мы тихо жалим организм, вводя в него ретровирус, который заставляет его трансформироваться. Стоимость 33 химиката.
imperial-changeling-sting-transform = Мы трансформируем { $target } в { $name }.
imperial-changeling-sting-transform-permanent = Наши гены вопят, когда мы трансформируем { $target } из низшей формы в { $name } навсегда!
imperial-changeling-sting-transform-revert = Вы снова чувствуете себя собой.

imperial-changeling-sting-false-armblade-name = Жало ложного армблейда
imperial-changeling-sting-false-armblade-desc = Мы бесшумно жалим человека, впрыскивая ретровирус, который мутирует его руку, временно превращая ее в армблейд. Стоит 20 химикатов.
imperial-changeling-sting-false-armblade-help = Жертва формирует армблейд, подобно тому, как это делает генокрад, только этот клинок тупой и бесполезный.
ent-ActionImperialChangelingStingFalseArmblade = Жало ложного армблейда
    .desc = Мы бесшумно жалим человека, временно превращая его руку в армблейд. Стоит 20 химикатов.
imperial-changeling-sting-false-blade-held = У цели нельзя снять { $item }, вы не можете наложить на цель ложный армблейд!
imperial-changeling-false-blade-others = Страшный клинок образуется вокруг руки { $target }!
imperial-changeling-false-blade-self = Ваша рука скручивается и мутирует, превращаясь в ужасающее чудовище!
imperial-changeling-false-blade-remove-others = С болезненным хрустом { $target } превращает клинок обратно в руку!
imperial-changeling-false-blade-remove-self = Клинок возвращается в нормальное состояние.

imperial-changeling-sting-extract-dna-name = Жало извлечения ДНК
imperial-changeling-sting-extract-dna-desc = Мы незаметно жалим цель и извлекаем ее ДНК. Стоит 25 химикатов.
imperial-changeling-sting-extract-dna-help = Даст вам ДНК вашей цели, позволяя трансформироваться в нее. Это сделает невозможным полное поглощение их тела в дальнейшем.
ent-ActionImperialChangelingStingExtractDna = Жало извлечения ДНК
    .desc = Мы незаметно жалим цель и извлекаем ее ДНК. Стоит 25 химикатов.

imperial-changeling-sting-mute-name = Жало немоты
imperial-changeling-sting-mute-desc = Мы беззвучно жалим человека, полностью заставляя его замолчать на короткое время. Стоит 20 химикатов.
imperial-changeling-sting-mute-help = Не предупреждает жертву о том, что ее ужалили, пока она не попытается заговорить и не сможет.
ent-ActionImperialChangelingStingMute = Жало немоты
    .desc = Мы беззвучно жалим человека, заставляя его замолчать на минуту. Стоит 20 химикатов.

imperial-changeling-sting-blind-name = Ослепляющее жало
imperial-changeling-sting-blind-desc = Мы временно ослепляем нашу жертву. Стоит 25 химикатов.
imperial-changeling-sting-blind-help = Это жало полностью ослепляет цель на короткое время и оставляет ее с затуманенным зрением на долгое время. Не действует, если у цели нет глаз.
ent-ActionImperialChangelingStingBlind = Ослепляющее жало
    .desc = Мы временно ослепляем нашу жертву. Стоит 25 химикатов.
imperial-changeling-sting-blind-target = Ваши глаза ужасно горят!

imperial-changeling-sting-lsd-name = Галлюциногенное жало
imperial-changeling-sting-lsd-desc = Мы вызываем массовый ужас у нашей жертвы. Стоит 10 химикатов.
imperial-changeling-sting-lsd-help = Мы развиваем способность жалить цель мощным галлюциногенным химикатом. Объект не замечает, что его ужалили, и эффект проявляется через 30-60 секунд.
ent-ActionImperialChangelingStingLsd = Галлюциногенное жало
    .desc = Мы вызываем массовый ужас у нашей жертвы. Стоит 10 химикатов.

imperial-changeling-sting-cryo-name = Криогенное жало
imperial-changeling-sting-cryo-desc = Мы беззвучно жалим жертву коктейлем из химикатов, который замораживает ее изнутри. Стоит 15 химикатов.
imperial-changeling-sting-cryo-help = Не предупреждает жертву, хотя она, скорее всего, поймет, что внезапно замерзла.
ent-ActionImperialChangelingStingCryo = Криогенное жало
    .desc = Мы беззвучно жалим жертву коктейлем, замораживающим её изнутри. Стоит 15 химикатов.

## Органическое оружие

ent-ImperialChangelingArmBlade = рука-клинок
    .desc = Гротескный клинок из кости и плоти, рассекающий людей, как горячий нож масло.
ent-ImperialChangelingArmBladeFalse = рука-клинок
    .desc = Гротескная масса плоти, которая раньше была вашей рукой. Хотя поначалу она выглядит опасной, видно, что на самом деле она тупая и бесполезная.

## Мутации

imperial-changeling-weapon-wrong-form = неправильная форма!
imperial-changeling-weapon-hand-busy = рука занята!

imperial-changeling-armblade-name = Рука-клинок
imperial-changeling-armblade-desc = Мы превращаем одну из наших рук в смертоносный клинок. Стоит 20 химикатов.
imperial-changeling-armblade-help = Мы можем убрать свой клинок так же, как и сформировали его. Клинок вскрывает шлюзы. Нельзя использовать, находясь в меньшей форме.
ent-ActionImperialChangelingArmBlade = Рука-клинок
    .desc = Мы превращаем одну из наших рук в смертоносный клинок. Стоит 20 химикатов.
imperial-changeling-weapon-blade-form-others = Страшный клинок формируется вокруг руки { $user }!
imperial-changeling-weapon-blade-form-self = Наша рука скручивается и мутирует, превращаясь в смертоносный клинок.
imperial-changeling-weapon-blade-retract-others = С отвратительным хрустом { $user } превращает клинок в руку!
imperial-changeling-weapon-blade-retract-self = Мы ассимилируем клинок обратно в наше тело.

imperial-changeling-tentacle-name = Щупальце
imperial-changeling-tentacle-desc = Мы подготавливаем щупальце, чтобы хватать им предметы или жертв. Стоит 10 химикатов.
imperial-changeling-tentacle-help = Его можно использовать один раз, чтобы достать удаленный предмет. Вне боевого режима щупальце подтаскивает жертву к нам, в боевом — бросает к нам и берёт в захват, а если в руке острое оружие, мы ещё и пронзаем её. Не может быть использовано в меньшей форме.
ent-ActionImperialChangelingTentacle = Щупальце
    .desc = Мы подготавливаем щупальце, чтобы хватать им предметы или жертв. Стоит 10 химикатов.
ent-ImperialChangelingTentacle = щупальце
    .desc = Мясистое щупальце, которое может вытягиваться и хватать предметы или людей.
ent-ImperialChangelingTentacleProjectile = щупальце
    .desc = Щупальце.
imperial-changeling-weapon-tentacle-form-self = Вы готовитесь вытянуть щупальце.
imperial-changeling-tentacle-item = Вы притягиваете { $item } к себе.
imperial-changeling-tentacle-pull-others = { CAPITALIZE($target) } притягивается к { $user } щупальцем!
imperial-changeling-tentacle-pull-target = Вас хватает щупальце и притягивает к { $user }!
imperial-changeling-tentacle-throw-others = { CAPITALIZE($target) } брошен в сторону { $user } щупальцем!
imperial-changeling-tentacle-throw-target = Вас хватает щупальце и бросает в сторону { $user }!
imperial-changeling-tentacle-stab = { CAPITALIZE($user) } протыкает { $target } с помощью { $weapon }!

imperial-changeling-shield-name = Органический щит
imperial-changeling-shield-desc = Мы превращаем одну из наших рук в твердый щит. Стоит 20 химикатов.
imperial-changeling-shield-help = Органическая ткань не может вечно сопротивляться повреждениям; щит может сломаться, после того, как по нему нанесут слишком много ударов. Чем больше генов мы поглощаем, тем сильнее он становится. Невозможно использовать, находясь в меньшей форме.
ent-ActionImperialChangelingShield = Органический щит
    .desc = Мы превращаем одну из наших рук в твердый щит. Стоит 20 химикатов.
ent-ImperialChangelingShield = щитоподобная масса
    .desc = Масса прочной костной ткани. В узоре щита всё ещё угадываются скрюченные пальцы.
imperial-changeling-weapon-shield-form-others = Конец руки { $user } быстро раздувается, образуя огромную массу, похожую на щит!
imperial-changeling-weapon-shield-form-self = Мы надуваем руку, превращая ее в прочный щит.
imperial-changeling-weapon-shield-retract-others = С жутким хрустом { $user } превращает свой щит в руку!
imperial-changeling-weapon-shield-retract-self = Мы ассимилируем наш щит в наше тело.
imperial-changeling-weapon-shield-break-others = С жутким хрустом { $user } превращает свой щит в руку!
imperial-changeling-weapon-shield-break-self = Мы ассимилируем наш щит в наше тело.
imperial-changeling-weapon-shield-block = { CAPITALIZE($user) } блокирует удар щитом!

imperial-changeling-armor-name = Хитиновая броня
imperial-changeling-armor-desc = Мы превращаем нашу кожу в прочный хитин, чтобы защитить себя от повреждений. Стоит 20 химикатов.
imperial-changeling-armor-help = На поддержание брони требуется небольшой расход химикатов. Доспехи обеспечивают достойную защиту от грубой силы и энергетического оружия. Не может быть использована в меньшей форме.
ent-ActionImperialChangelingArmor = Хитиновая броня
    .desc = Мы превращаем нашу кожу в прочный хитин, чтобы защитить себя от повреждений. Стоит 20 химикатов.
ent-ImperialChangelingArmor = хитиновая масса
    .desc = Твердое покрытие из черного хитина.
ent-ImperialChangelingHelmet = хитиновая масса
    .desc = Твердое покрытие из черного хитина с прозрачным хитином спереди.
imperial-changeling-suit-armor-form-others = Плоть { $user } чернеет, быстро превращаясь в твердую хитиновую массу!
imperial-changeling-suit-armor-form-self = Мы закаляем свою плоть, создавая броню!
imperial-changeling-suit-armor-castoff-others = { CAPITALIZE($user) } сбрасывает свою броню!
imperial-changeling-suit-armor-castoff-self = Мы сбрасываем нашу броню.
imperial-changeling-suit-body-busy = слот тела занят!
imperial-changeling-suit-head-busy = слот головы занят!

imperial-changeling-hivehead-name = Голова-улей
imperial-changeling-hivehead-desc = Мы покрываем голову восковым покрытием, похожим на пчелиный улей, которое можно использовать для производства пчел, атакующих наших врагов. Стоит 15 химикатов.
imperial-changeling-hivehead-help = Хотя голова улья не дает особой брони, она позволяет посылать пчел в атаку на цели.
ent-ActionImperialChangelingHiveHead = Голова-улей
    .desc = Мы покрываем голову восковым покрытием, похожим на пчелиный улей. Стоит 15 химикатов.
ent-ImperialChangelingHiveHead = голова-улей
    .desc = Странное восковое покрытие на голове. От него звенит в ушах.
ent-ActionImperialChangelingReleaseBees = Выпустить пчел
    .desc = Выпустите группу пчел, чтобы они атаковали всех остальных живых существ.
imperial-changeling-suit-hive-castoff-others = { CAPITALIZE($user) } сбрасывает свою голову-улей!
imperial-changeling-suit-hive-castoff-self = Мы сбрасываем нашу голову-улей.
imperial-changeling-hive-release-others = Голова { $user } начинает гудеть, когда из нее начинают вылетать пчелы!
imperial-changeling-hive-release-self = Мы выпускаем пчел.

## Прочие способности

imperial-changeling-power-adrenaline-name = Генный стимулятор
imperial-changeling-power-adrenaline-desc = Мы концентрируем наши химикаты в мощный стимулятор, делающий нашу форму потрясающе устойчивой к недееспособности. Стоит 25 химикатов.
imperial-changeling-power-adrenaline-help = Избавляет от любого оглушения и вливает в нашу форму ЧЕТЫРЕ единицы адреналина генокрада; наша форма восстанавливает огромное количество выносливости и просто не замечает боли или усталости во время его воздействия.
ent-ActionImperialChangelingAdrenaline = Генный стимулятор
    .desc = Мы концентрируем химикаты в мощный стимулятор. Стоит 25 химикатов.
imperial-changeling-adrenaline = Поразительный прилив стимулятора, точно настроенного под нашу биологию, БОДРИТ. Нас не одолеть.
reagent-name-imperial-changeling-adrenaline = адреналин генокрада
reagent-desc-imperial-changeling-adrenaline = Сокращает время потери сознания, сбивания с ног и оглушения. Восстанавливает выносливость, но при передозировке наносит урон токсинами.

imperial-changeling-power-eyesight-name = Улучшенное зрение
imperial-changeling-power-eyesight-desc = Создает больше светочувствительных палочек в наших глазах, что позволяет нашему зрению проникать сквозь большинство загораживающих предметов. Защищает наше зрение от вспышек во время бездействия.
imperial-changeling-power-eyesight-help = Дает нам рентгеновское зрение или защиту от вспышек. Пока рентгеновское зрение активно, мы станем гораздо более уязвимыми для устройств со вспышкой.
ent-ActionImperialChangelingEyesight = Улучшенное зрение
    .desc = Переключает рентгеновское зрение и защиту от вспышек.
imperial-changeling-eyesight-protect = Мы настраиваем глаза, чтобы защитить их от яркого света.
imperial-changeling-eyesight-xray = Мы настраиваем глаза, чтобы чувствовать добычу сквозь стены.

imperial-changeling-power-biodegrade-name = Биодеградация
imperial-changeling-power-biodegrade-desc = Растворяет наручники и другие предметы, мешающие свободному движению. Стоит 30 химикатов.
imperial-changeling-power-biodegrade-help = Это очевидно для находящихся рядом людей и может разрушить стандартные наручники и шкафы. Работает против захватов.
ent-ActionImperialChangelingBiodegrade = Биодеградация
    .desc = Растворяет наручники и другие предметы, мешающие свободному движению. Стоит 30 химикатов.
imperial-changeling-biodegrade-free = уже свободен!
imperial-changeling-biodegrade-locker = Пузырящаяся кислота начинает извергаться из { $locker }...
imperial-changeling-biodegrade-cuffs = { CAPITALIZE($user) } извергает потоки кислоты на оковы, расплавляя их с ужасающей легкостью.
imperial-changeling-biodegrade-acid = { CAPITALIZE($user) } извергает струю шипящей кислоты на { $target }, используя возможность вырваться.
imperial-changeling-biodegrade-acid-ling = { CAPITALIZE($user) } извергает струю шипящей кислоты на { $target }... но ничего не произошло!

imperial-changeling-power-chameleon-name = Кожа хамелеона
imperial-changeling-power-chameleon-desc = Пигментация нашей кожи быстро меняется в зависимости от текущего окружения. Стоит 10 химикатов.
imperial-changeling-power-chameleon-help = Позволяет нам становиться невидимыми после нескольких секунд неподвижности. Можно включать и выключать.
ent-ActionImperialChangelingChameleon = Кожа хамелеона
    .desc = Мы становимся невидимыми, пока стоим на месте. Стоит 10 химикатов.
imperial-changeling-chameleon-on = Наша кожа начинает подстраиваться под окружение.
imperial-changeling-chameleon-off = Наша кожа возвращает обычный цвет.

imperial-changeling-power-darkness-name = Адаптация к темноте
imperial-changeling-power-darkness-desc = Пигментация кожи и глаза быстро меняется в зависимости от темноты. Для включения требуется 15 химикатов. Замедляет регенерацию химикатов на 15%.
imperial-changeling-power-darkness-help = Позволяет затемнить и изменить полупрозрачность пигментации, а также адаптировать глаза для видения в темных условиях. Можно включать и выключать.
ent-ActionImperialChangelingDarkness = Адаптация к темноте
    .desc = Мы становимся полупрозрачными и видим в темноте. Включение стоит 15 химикатов.
imperial-changeling-darkness-on-others = Кожа { $user } внезапно становится полупрозрачной!
imperial-changeling-darkness-on-self = Теперь мы стали гораздо более скрытными и лучше видим в темноте.
imperial-changeling-darkness-off-others = { CAPITALIZE($user) } появляется из воздуха!
imperial-changeling-darkness-off-self = Мы становимся внешне нормальными и теряем способность видеть в темноте.

imperial-changeling-power-defibgrasp-name = Дефибрилляторная хватка
imperial-changeling-power-defibgrasp-desc = Мы готовимся, пока находимся в стазисе. Если кто-то из наших врагов попытается сделать нам дефибрилляцию, мы оглушим его и мгновенно завершим наш стазис.
imperial-changeling-power-defibgrasp-help = Эта способность пассивна и срабатывает, когда к нашей груди прикладывают лопатки дефибриллятора, пока мы мертвы или находимся в стазисе. Также на мгновение оглушает киборгов.
imperial-changeling-defib-others = { CAPITALIZE($user) } внезапно просыпается!
imperial-changeling-defib-self = Сила дефибриллятора проходит через нас, оживляя нас из стазиса!
imperial-changeling-defib-target = { CAPITALIZE($user) } внезапно просыпается и хватает вас за руки!
imperial-changeling-defib-borg = { CAPITALIZE($user) } внезапно пробуждается, перегружая ваши сенсоры!

imperial-changeling-power-digitalcamo-name = Цифровой камуфляж
imperial-changeling-power-digitalcamo-desc = Развивая способность искажать свою форму и пропорции, мы побеждаем обычные алгоритмы, используемые для обнаружения форм жизни на камерах.
imperial-changeling-power-digitalcamo-help = При использовании этого навыка нас нельзя отследить с помощью камеры или увидеть с помощью ИИ. Однако люди, смотрящие на нас, будут находить нас... странными.
ent-ActionImperialChangelingDigitalCamo = Цифровой камуфляж
    .desc = Мы искажаем свою форму, чтобы скрыться от ИИ.
imperial-changeling-digicamo-on = Мы искажаем свою форму, чтобы скрыться от ИИ.
imperial-changeling-digicamo-off = Мы возвращаемся в нормальное состояние.
imperial-changeling-digicamo-examine = [color=#9b3fc4]{ CAPITALIZE($user) } двигает телом неестественно и совершенно не по-человечески.[/color]

imperial-changeling-power-fleshmend-name = Заживление плоти
imperial-changeling-power-fleshmend-desc = Наша плоть быстро регенерирует, заживляя ожоги и ушибы, одышку, а также скрывая все шрамы. Стоит 20 химикатов.
imperial-changeling-power-fleshmend-help = Если мы находимся в огне, эффект лечения не действует. Не восстанавливает потерянную кровь. Действует в бессознательном состоянии.
ent-ActionImperialChangelingFleshmend = Заживление плоти
    .desc = Наша плоть быстро регенерирует 10 секунд. Стоит 20 химикатов.
imperial-changeling-fleshmend = Мы начинаем быстро выздоравливать.
imperial-changeling-fleshmend-already = уже лечимся!

imperial-changeling-power-panacea-name = Анатомическая панацея
imperial-changeling-power-panacea-desc = Выводит загрязнения из нашей формы: лечит болезни, удаляет паразитов, отрезвляет нас, очищает от токсинов и радиации, а также полностью сбрасывает наш генетический код. Стоит 20 химикатов.
imperial-changeling-power-panacea-help = Можно использовать в бессознательном состоянии.
ent-ActionImperialChangelingPanacea = Анатомическая панацея
    .desc = Выводит загрязнения из нашей формы. Стоит 20 химикатов.
imperial-changeling-panacea = Мы очищаем нашу форму от нечистот.

imperial-changeling-power-pheromone-name = Феромонные рецепторы
imperial-changeling-power-pheromone-desc = Мы настраиваем свои органы чувств, чтобы отслеживать других генокрадов по запаху. Чем ближе они, тем легче их найти.
imperial-changeling-power-pheromone-help = Мы будем знать общее направление движения близлежащих генокрадов, причем более близкие запахи будут сильнее. Во время действия этой функции наша химическая выработка замедлена.
ent-ActionImperialChangelingPheromone = Феромонные рецепторы
    .desc = Мы ищем по запаху ближайших генокрадов.
imperial-changeling-pheromone-on = Мы ищем по запаху ближайших генокрадов.
imperial-changeling-pheromone-off = Мы прекращаем поиски.

imperial-changeling-power-resonantshriek-name = Резонирующий крик
imperial-changeling-power-resonantshriek-desc = Наши легкие и голосовые связки смещаются, позволяя нам на короткое время издать звук, который оглушает и сбивает с толку не-генокрадов. Стоит 20 химикатов.
imperial-changeling-power-resonantshriek-help = Излучает высокочастотный звук, который сбивает с толку и оглушает людей, выбивает свет и перегружает сенсоры киборгов.
ent-ActionImperialChangelingResonantShriek = Резонирующий крик
    .desc = Мы издаём звук, сбивающий с толку не-генокрадов. Стоит 20 химикатов.

imperial-changeling-power-technoshriek-name = Технофагический крик
imperial-changeling-power-technoshriek-desc = Мы смещаем голосовые связки, чтобы выпустить высокочастотный звук, который перегружает близлежащую электронику. Стоит 20 химикатов.
imperial-changeling-power-technoshriek-help = Создаёт электромагнитный импульс и бьёт лампы вокруг.
ent-ActionImperialChangelingTechnoShriek = Технофагический крик
    .desc = Мы перегружаем ближайшую электронику. Стоит 20 химикатов.

imperial-changeling-power-spiders-name = Распространение заражения
imperial-changeling-power-spiders-desc = Наша форма делится, создавая скопление яиц, из которых вырастет смертельно опасный арахнид. Стоит 45 химикатов.
imperial-changeling-power-spiders-help = Пауки — безжалостные существа и могут напасть на своих создателей, когда вырастут. Требуется не менее 3 поглощений ДНК.
ent-ActionImperialChangelingSpiders = Распространение заражения
    .desc = Мы откладываем кладку паучьих яиц. Стоит 45 химикатов.
imperial-changeling-spiders-space = недостаточно места!
ent-ImperialChangelingSpiderEggs = кровавая кладка яиц
    .desc = Пульсирующие яйца. Что-то внутри шевелится.
imperial-changeling-spider-role-name = Паук из плоти
imperial-changeling-spider-role-desc = Мерзость, выпущенная на станцию генокрадами. Ваша единственная цель — убивать, наводить ужас и выживать.

imperial-changeling-power-muscles-name = Напряжённые мышцы
imperial-changeling-power-muscles-desc = Мы развиваем способность уменьшать накопление кислоты в наших мышцах, что позволяет нам двигаться гораздо быстрее.
imperial-changeling-power-muscles-help = Нагрузка вызовет у нас усталость, и мы быстро выдохнемся. Не может быть использовано в меньшей форме.
ent-ActionImperialChangelingMuscles = Напряжённые мышцы
    .desc = Мы двигаемся гораздо быстрее, но быстро устаём.
imperial-changeling-muscles-on = Наши мышцы напрягаются и укрепляются.
imperial-changeling-muscles-off = Наши мышцы расслабляются.
imperial-changeling-muscles-collapse = Мы падаем в изнеможении.
imperial-changeling-muscles-exhausted = Наши мышцы расслабляются, не получая энергии для их укрепления.
imperial-changeling-muscles-pain = Наши ноги начинают болеть...

imperial-changeling-power-voidadaption-name = Адаптация к пустоте
imperial-changeling-power-voidadaption-desc = Мы готовим наши клетки к противостоянию враждебной среде за пределами станции. Мы можем свободно перемещаться, куда пожелаем.
imperial-changeling-power-voidadaption-help = Эта способность пассивна и автоматически защищает в условиях сильного холода или вакуума, а также избавляет от необходимости дышать. Пока она защищает от холода или давления, химикаты вырабатываются медленнее.
imperial-changeling-void-on = Наши клетки укрепляются против { $reason }.
imperial-changeling-void-off = Наши клетки отдыхают в безопасном воздухе.
imperial-changeling-void-removed = Наши клетки расслабляются, несмотря на опасность!
imperial-changeling-void-vacuum = вакуума
imperial-changeling-void-cold = холода

imperial-changeling-power-mimicvoice-name = Имитация голоса
imperial-changeling-power-mimicvoice-desc = Мы формируем наши голосовые железы так, чтобы они звучали желаемым голосом. Поддержание этой силы замедляет выработку химических веществ.
imperial-changeling-power-mimicvoice-help = Превратит ваш голос в имя, которое вы введете. Мы должны постоянно расходовать химические вещества, чтобы поддерживать такую форму.
ent-ActionImperialChangelingMimicVoice = Имитация голоса
    .desc = Мы говорим чужим голосом.
imperial-changeling-mimic-title = Имитация голоса
imperial-changeling-mimic-prompt = Введите имя для подражания
imperial-changeling-mimic-on = Мы формируем наши железы так, чтобы они издавали голос { $name }. Это замедлит регенерацию химикатов. Используйте эту силу снова, чтобы вернуть прежний голос.
imperial-changeling-mimic-off = Мы возвращаем наши голосовые железы в исходное положение.

imperial-changeling-power-lesserform-name = Низшая форма
imperial-changeling-power-lesserform-desc = Мы переформируемся и становимся низшими. Мы становимся обезьяной. Стоит 5 химикатов.
imperial-changeling-power-lesserform-help = Трансформация значительно уменьшает наши размеры, позволяя нам выскальзывать из наручников. Повторное использование возвращает человеческую форму.
ent-ActionImperialChangelingLesserForm = Низшая форма
    .desc = Мы становимся обезьяной или возвращаемся в человеческую форму. Стоит 5 химикатов.
imperial-changeling-lesser-become = Наши гены вопят!
imperial-changeling-lesser-human = Мы трансформируем свою внешность.

imperial-changeling-power-lastresort-name = Последний шанс
imperial-changeling-power-lastresort-desc = Мы жертвуем нашим нынешним телом, бурно расширяясь, чтобы прорваться сквозь препятствия, и превращаемся в червя. Стоит 20 химикатов.
imperial-changeling-power-lastresort-help = Мы лопнем, а затем будем управлять маленьким хрупким существом. Укусив труп, мы отложим в него яйцо, из которого вырастет наша новая форма.
ent-ActionImperialChangelingLastResort = Последний шанс
    .desc = Мы взрываемся и становимся червем. Стоит 20 химикатов.
imperial-changeling-last-resort-confirm = Уверены ли мы в том, что хотим уничтожить свое тело и создать червя?
imperial-changeling-last-resort-swell = Тело { $user } начинает пульсировать и неестественно раздуваться!
imperial-changeling-last-resort-burst = Тело { $user } разрывается в жестоком взрыве биомассы!
imperial-changeling-last-resort-blind = Вы ослеплены кровавым дождем!
imperial-changeling-last-resort-borg = Ваши сенсоры отключены ливнем крови!
imperial-changeling-headslug-born = Вы вырываетесь из останков своего прежнего тела под ливнем кровавых ошметков!
ent-ImperialChangelingHeadslug = червь
    .desc = Маленькое существо, похожее на слизня, с большой зияющей пастью. Покрыто толстым слоем скользкой слизи.
imperial-changeling-headslug-pregnant = носитель уже беременный!
imperial-changeling-headslug-implant-others = { CAPITALIZE($slug) } вводит что-то в плоть { $target }!
imperial-changeling-headslug-implant-self = Мы вводим наше яйцо в тело { $target }!
imperial-changeling-headslug-dying = После того как мы отложили яйцо, наша смерть стремительно приближается...

imperial-changeling-preset-title = Генокрады
imperial-changeling-preset-description = Среди экипажа скрываются генокрады, способные принимать облик своих жертв.

ent-ImperialChangelingRule = Генокрады
    .desc = Правило: генокрады TG среди экипажа.

## Интерфейс TG: CellularEmporium

imperial-changeling-emporium-dna = ДНК
imperial-changeling-emporium-readapt-tip = Отменить развитие всех способностей и вернуть генетические очки.
imperial-changeling-emporium-readapt-tip-none = Нужно больше ДНК для переадаптации.
imperial-changeling-emporium-compact = Компактный вид
imperial-changeling-emporium-detailed = Подробный вид
imperial-changeling-emporium-none-found = Способности не найдены.
imperial-changeling-emporium-none-category = В этой категории нет способностей.
imperial-changeling-emporium-req-points = { $count } ДНК

## Интерфейс TG: AntagInfoChangeling

ent-ActionImperialChangelingInfo = Открыть информацию о «Генокраде»
    .desc = Досье генокрада: задачи, способности и украденные воспоминания.
imperial-changeling-info-greet = Для большей информации - прочтите панель. Вы можете вернуться снова, нажав кнопку сверху слева.
imperial-changeling-info-title = Генокрад
imperial-changeling-info-intro = Вступление
imperial-changeling-info-absorbed-dna = Поглощённое ДНК
imperial-changeling-info-you-are = [font size=20]Вы { $name } из [color=yellow]{ $hive }[/color].[/font]
imperial-changeling-info-objectives = [bold]Ваши текущие задачи:[/bold]
imperial-changeling-info-objectives-none = Задач нет.
imperial-changeling-info-objective = [bold]№{ $index }:[/bold] { $text }
imperial-changeling-info-abilities = Способности
imperial-changeling-info-ability-1 = Ваша способность[color=red][bold] Поглотить ДНК[/bold][/color] позволяет вам украсть ДНК и воспоминания жертвы. Способность[color=red][bold] Жало извлечения ДНК[/bold][/color] также крадет ДНК жертвы и не поддается обнаружению, но не дает вам их воспоминаний или образцов речи.
imperial-changeling-info-ability-2 = Ваша способность[color=lightblue][bold] Возрождающий стазис[/bold][/color] позволяет вам возродиться. Это значит, что ничто, кроме полного разрушения тела, не сможет остановить вас! Разумеется, это громко, поэтому не стоит делать это в присутствии людей, которых вы не собираетесь заткнуть.
imperial-changeling-info-ability-3 = Ваша способность[color=orange][bold] Трансформация[/bold][/color] позволяет вам превратиться в форму тех, у кого вы собрали ДНК, летально и нелетально. Она также будет имитировать (НЕ НАСТОЯЩУЮ ОДЕЖДУ) одежду, в которую они были одеты, для каждого вашего свободного слота.
imperial-changeling-info-ability-4 = [color=lightgreen][bold]Клеточный магазин[/bold][/color] — это место, где вы приобретаете новые способности, помимо стартового набора. У вас есть 10 генетических очков, которые вы можете потратить на способности, и вы можете реадаптироваться после поглощения тела, возвращая свои очки для разных наборов.
imperial-changeling-info-ability-5 = Все способности требуют[color=violet][bold] химикатов[/bold][/color], их количество показано на HUD слева. Наведите на него курсор, чтобы увидеть, сколько химикатов вы можете хранить. Это число можно увеличить,[color=red][bold] поглощая[/bold][/color] других генокрадов.
imperial-changeling-info-ability-6 = Все генокрады, независимо от происхождения, связаны[color=violet][bold] общим разумом[/bold][/color]. Вы можете общаться с другими генокрадами под своим мысленным псевдонимом[color=violet][bold] { $name }[/bold][/color], начиная сообщение с[color=violet][bold] :е[/bold][/color]. Работайте вместе, и вы приведёте станцию к новым высотам ужаса.
imperial-changeling-info-betrayal = Другие генокрады — сильные союзники, но некоторые из них могут вас предать. Генокрады сильно растут в силе, поглощая себе подобных, и поглощённый генокрад становится [color=black][bold]Падшим генокрадом[/bold][/color]. Нет большего унижения.
imperial-changeling-info-memories-title = Украденные воспоминания
imperial-changeling-info-memories-tip = Поглощая цели, вы можете получить их воспоминания. Они должны помочь вам выдать себя за свою цель!
imperial-changeling-info-stolen-title = Дополнительная украденная информация
imperial-changeling-info-absorb-first = Сначала поглотите жертву!

imperial-changeling-memory-story = Мы — { $name }. Наша работа на станции: { $job }.
imperial-changeling-memory-objectives-header = { $name }, задачи:
imperial-changeling-memory-objective = { " " }Задача #{ $index }: { $text }.
imperial-changeling-memory-end = { " " }Это все, что имел { $name }.{ " " }

## HUD и феромоны

imperial-changeling-hud-chems-help = Показывает, сколько у вас химикатов. Если навести на него курсор, покажет максимальное количество химикатов, которое вы можете хранить.
imperial-changeling-pheromone-alert-name = Аромат феромона
imperial-changeling-pheromone-alert-desc = Нос всегда знает.

## Окна выбора (tgui_alert, tgui_input_list)

imperial-changeling-choice-yes = Да
imperial-changeling-choice-no = Нет
imperial-changeling-choice-cancel = Отмена
imperial-changeling-fakedeath-confirm-title = Притвориться мертвым
imperial-changeling-mimic-choice = Выбрать самому имя или из существующих людей?
imperial-changeling-mimic-manual = Ручной ввод имени
imperial-changeling-mimic-existing = Существующий человек
imperial-changeling-mimic-pick = Выберите имя для подражания
imperial-changeling-fleshmend-alert-name = Заживление плоти
imperial-changeling-fleshmend-alert-desc = Наши раны быстро заживают. [italic]Этот эффект приостанавливается, если мы горим.[/italic]
ent-ImperialChangelingMonkeyifyEffect = { "" }
ent-ImperialChangelingHumanifyEffect = { "" }

imperial-changeling-armblade-pry = { CAPITALIZE($user) } втыкает клинок в { $door } и начинает его вскрывать!

## Конечности (regenerate_limbs, dismember)
imperial-changeling-limbs-regrow-others = Отсутствующие конечности { $user } срастаются, издавая громкий, жуткий звук!
imperial-changeling-limbs-form-others = Отсутствующие конечности { $user } формируются, издавая громкий, жуткий звук!
imperial-changeling-limbs-regrow-self = Ваши конечности отрастают, издавая громкий хрустящий звук и причиняя вам сильную боль!
imperial-changeling-arm-regrow-others = Отсутствующая рука { $user } реформируется, издавая громкий, жуткий звук!
imperial-changeling-arm-regrow-self = Ваша рука отрастает, издавая громкий хрустящий звук и причиняя вам сильную боль!
imperial-changeling-defib-arms-others = { CAPITALIZE($user) } внезапно просыпается, выхватывает дефибриллятор из рук { $target } при этом отрывая { $count ->
    [one] одну из рук
   *[other] обе руки
}!
imperial-changeling-defib-arms-target = { CAPITALIZE($user) } внезапно просыпается, отрывая { $count ->
    [one] одну из ваших рук
   *[other] ваши руки
}!
imperial-changeling-defib-arms-self = Сила дефибриллятора проходит через нас, оживляя нас из стазиса! С этой вновь обретенной энергией мы отрываем { $count ->
    [one] одну из рук
   *[other] руки
} { $target }!

## Тьма, речь жертвы, клетка
imperial-changeling-darkness-examine = [color=#e5c07b]Кожа { $user } неестественно мерцает на свету.[/color]
imperial-changeling-memory-speech-header = Некоторые образцы речи { $name }, мы должны изучить их, чтобы лучше изображать жертву:
imperial-changeling-memory-speech-end = . У нас нет больше знаний об образцах речи { $name }.
imperial-changeling-memory-speech-chat = Некоторые образцы речи { $name }, мы должны изучить их, чтобы лучше изображать жертву!
imperial-changeling-memory-speech-chat-end = У нас больше нет знаний об образцах речи { $name }.
imperial-changeling-last-resort-crush = Расширяющаяся форма { $user } начинает сокрушать окружающие преграды!

## Червь (headslug)
imperial-changeling-headslug-help = Найдите мёртвое тело гуманоида и укусите его в боевом режиме, чтобы отложить яйцо. Через 4 минуты из тела вылупится обезьяна — наша новая форма, а сам червь умрёт через 10 секунд после кладки.
imperial-changeling-headslug-examine-aimless = [color=#6685f5]Кажется, что он передвигается бесцельно.[/color]
imperial-changeling-headslug-examine-purpose = [color=#e5c07b]Он двигается осмысленно![/color]
imperial-changeling-headslug-examine-spent = [color=#6685f5]Его репродуктивный орган, похоже, иссяк.[/color]
