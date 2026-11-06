## Команда makechaplain

cmd-makechaplain-desc = Выдаёт игроку святую роль и снаряжение капеллана.
cmd-makechaplain-help = makechaplain <ckey> [auto|highpriest|priest|deacon]
cmd-makechaplain-role-auto = Верховный жрец, если религии ещё нет, иначе жрец.
cmd-makechaplain-role-highpriest = Верховный жрец: выбирает религию, бога, название и обложку библии.
cmd-makechaplain-role-priest = Жрец: присоединяется к религии станции, получает снаряжение.
cmd-makechaplain-role-deacon = Дьякон: святая роль без снаряжения и без права проводить обряды.
cmd-makechaplain-no-player = Игрок { $player } не найден или у него нет тела.
cmd-makechaplain-bad-role = Неизвестная роль «{ $role }». Доступно: auto, highpriest, priest, deacon.
cmd-makechaplain-success = { $player } получил святую роль { $role }.

## Религия станции

imperial-religion-default-religion = Христианство
imperial-religion-default-deity = Космический Иисус
imperial-religion-default-bible = Библия по умолчанию
imperial-religion-greeting-high-priest = Вы — верховный жрец религии «{ $religion }». Ваш бог — { $deity }. Проводите службы, защищайте экипаж от культистов и несите веру.
imperial-religion-greeting-priest = На станции уже есть религия. Вы — последователь бога { $deity }. Подчиняйтесь капеллану.
imperial-religion-greeting-deacon = Вы стали дьяконом бога { $deity }. Святые предметы подвластны вам, но обряды проводит капеллан.
imperial-religion-bible-examine = [color=lightblue]Эта библия одобрена богом { $deity }.[/color]

## Молитва

imperial-religion-pray-action-name = Помолиться
imperial-religion-pray-action-desc = Обратиться к своему богу. Молитва святого слышна особенно хорошо.
imperial-religion-pray-title = Молитва
imperial-religion-pray-prefix = МОЛИТВА КАПЕЛЛАНА
imperial-religion-pray-sent = Вы возносите молитву богу { $deity }.

## Маяк вооружения

holy-armaments-already-chosen = Выбор уже сделан. Маяк доставляет набор станции.

## Предметы

ent-ClothingBackpackImperialCultpack = трофейная стойка
    .desc = Пригодится и чтобы носить лишнее снаряжение, и чтобы гордо заявить о своём безумии.
ent-HolyArmamentsBeacon = маяк вооружения
    .desc = Вызывает набор стандартного вооружения капеллана, утверждённого Межзвёздной ассоциацией по сохранению религии.
ent-BoxImperialChaplainVestments = коробка облачений капеллана
    .desc = Коробка с церемониальными облачениями капеллана.

ent-ClothingOuterImperialHolidayPriest = праздничная ряса
    .desc = Хороший праздник, сын мой.
ent-ClothingOuterImperialHabit = религиозная туника
    .desc = Никакой монашеской скромности.
ent-ClothingOuterImperialBishopRobe = епископское облачение
    .desc = Приятно видеть, что собранная десятина потрачена с толком.
ent-ClothingOuterImperialMonkHabit = монашеская ряса
    .desc = На пару ступеней выше рваной мешковины.
ent-ClothingHeadImperialMonkHood = монашеский капюшон
    .desc = Когда хочется прикрыть тонзуру.
ent-ClothingOuterImperialMonkRobeEast = одеяние восточного монаха
    .desc = Лучше всего сочетается с бритой головой.
ent-ClothingOuterImperialWhiteRobe = белая мантия
    .desc = Хороша для клириков и сонных членов экипажа.
ent-ClothingOuterImperialShrinehand = облачение служителя святилища
    .desc = С духами говорить не поможет, но выглядеть будете как надо.

ent-BoxImperialHolyTemplar = набор храмовника
    .desc = Набор святого вооружения капеллана.
ent-ClothingHeadImperialTemplar = шлем крестоносца
    .desc = Deus Vult.
ent-ClothingOuterImperialTemplar = доспех крестоносца
    .desc = Так хочет Бог!
ent-BoxImperialHolyForgotten = набор забытого
    .desc = Набор святого вооружения капеллана.
ent-ClothingHeadImperialForgotten = забытый шлем
    .desc = В нём непреклонный взгляд навеки забытого бога.
ent-ClothingOuterImperialForgotten = забытый доспех
    .desc = Звучит как шипение пара и тиканье шестерёнок, давно умолкших. Похож на мёртвую машину, которая пытается ожить.
ent-BoxImperialHolyStudent = набор нечестивого учёного
    .desc = Набор святого вооружения капеллана.
ent-ClothingOuterImperialStudentRobe = мантия студента
    .desc = Форма давно исчезнувшего учебного заведения.
ent-ClothingHeadImperialCage = клетка
    .desc = Клетка, сдерживающая волю, чтобы видеть мирскую суету такой, какая она есть.
ent-BoxImperialHolySentinel = набор каменного стража
    .desc = Набор святого вооружения капеллана.
ent-ClothingOuterImperialAncient = древний доспех
    .desc = Храни сокровище...
ent-ClothingHeadImperialAncient = древний шлем
    .desc = Никто не пройдёт!
ent-BoxImperialHolyWitchhunter = набор охотника на ведьм
    .desc = Набор святого вооружения капеллана.
ent-ClothingOuterImperialWitchhunter = одеяние охотника на ведьм
    .desc = Этот поношенный наряд повидал немало.
ent-ClothingHeadImperialWitchhunterHat = шляпа охотника на ведьм
    .desc = Эта шляпа повидала немало.
ent-BoxImperialHolyAdept = набор божественного адепта
    .desc = Набор святого вооружения капеллана.
ent-ClothingOuterImperialAdept = одеяние адепта
    .desc = Идеальный наряд, чтобы жечь неверных.
ent-ClothingHeadImperialAdept = капюшон адепта
    .desc = Ересь — это только когда так делают другие.
ent-ClothingHandsImperialBracers = наручи
    .desc = Пара наручей.
ent-BoxImperialHolyFollower = набор последователей капеллана
    .desc = Набор святого вооружения капеллана: четыре балахона последователей и один для лидера.
ent-ClothingOuterImperialFollower = балахон последователя
    .desc = Балахон для последователей капеллана.
ent-ClothingHeadImperialFollowerHood = капюшон последователя
    .desc = Капюшон для последователей капеллана.
ent-ClothingOuterImperialFollowerLeader = балахон лидера
    .desc = Теперь можно и за святой водой по 50 кредитов.
ent-ClothingHeadImperialFollowerHoodLeader = капюшон лидера
    .desc = Святую воду искать не обязательно. Просто, по-моему, стоит.
ent-BoxImperialHolyDivineArcher = набор божественного лучника
    .desc = Набор святого вооружения капеллана.
ent-ClothingUniformImperialDivineArcher = одеяние божественного лучника
    .desc = Священное одеяние лучников веры.
ent-ClothingOuterImperialDivineArcher = плащ божественного лучника
    .desc = Плащ лучников веры.
ent-ClothingHeadImperialDivineArcherHood = капюшон божественного лучника
    .desc = Капюшон лучников веры.
ent-ClothingHandsImperialDivineArcher = наручи божественного лучника
    .desc = Наручи лучников веры.
ent-ClothingShoesImperialDivineArcher = сапоги божественного лучника
    .desc = Сапоги лучников веры.
ent-BoxImperialHolyOccultist = набор оккультиста
    .desc = Набор святого вооружения капеллана.
ent-ClothingOuterImperialOccultist = одеяние оккультиста
    .desc = Защищает тело от того, чего другие не замечают.
ent-ClothingHeadImperialOccultist = капюшон оккультиста
    .desc = Скрывает ваше лицо от тех, кто смотрит в ответ.

## Выбор религии (настройки religion_name, deity_name, bible_name из SS13)

imperial-religion-setup-title = Религия станции
imperial-religion-setup-hint = Вы — верховный жрец. Выберите свою религию.
imperial-religion-setup-religion = Название религии
imperial-religion-setup-deity = Имя божества
imperial-religion-setup-bible = Название библии
imperial-religion-setup-random = Случайная религия
imperial-religion-setup-confirm = Подтвердить
imperial-religion-bible-replace = Священная книга { $religion }
imperial-religion-servicianism-bible-desc = Счастлив, сыт, чист. Живи этим и дари это.
imperial-religion-insuls-name = изоляция
imperial-religion-insuls-desc = Жалкая копия настоящей изоляции.

## Обложки библии

imperial-bible-skin-bible = Библия
imperial-bible-skin-koran = Коран
imperial-bible-skin-scrapbook = Альбом
imperial-bible-skin-burning = Горящая библия
imperial-bible-skin-honk1 = Клоунская библия
imperial-bible-skin-honk2 = Банановая библия
imperial-bible-skin-creeper = Библия крипера
imperial-bible-skin-white = Белая библия
imperial-bible-skin-holylight = Святой свет
imperial-bible-skin-atheist = Бог как иллюзия
imperial-bible-skin-tome = Том
imperial-bible-skin-kingyellow = Король в жёлтом
imperial-bible-skin-ithaqua = Итаква
imperial-bible-skin-scientology = Саентология
imperial-bible-skin-melted = Расплавленная библия
imperial-bible-skin-necronomicon = Некрономикон
imperial-bible-skin-insuls = Изоляционизм
imperial-bible-skin-gurugranthsahib = Гуру Грантх Сахиб
imperial-bible-skin-kojiki = Кодзики

ent-BibleImperialChaplain = библия
    .desc = Применять к голове многократно.
    .suffix = Капеллан

## Посвящение в дьякона (religion_rites/deaconize из SS13)

imperial-deaconize-only-priests = Проводить обряды могут только жрецы.
imperial-deaconize-in-progress = Обряд уже проводится.
imperial-deaconize-used = Посвящение уже проведено: посвятить можно только одного человека.
imperial-deaconize-nobody = На { $altar } никого нет! Уложите претендента на алтарь.
imperial-deaconize-not-conscious = { CAPITALIZE($target) } должен быть жив и в сознании, чтобы вступить!
imperial-deaconize-already-holy = { CAPITALIZE($target) } уже является членом этой религии!
imperial-deaconize-invited = Претенденту была предложена возможность вступить в наши ряды. Подождите, пока он решит, и попробуйте ещё раз.
imperial-deaconize-left = { CAPITALIZE($target) } больше не находится на алтаре!
imperial-deaconize-no-mind = Разум { $target } находится в другом месте!
imperial-deaconize-cultist = { $deity } увидел истинное, тёмное зло в сердце { $target } и поразил его!
imperial-deaconize-success = { $deity } связал { $target } с кодексом! Теперь он занимает святую роль (пусть и самого низшего ранга)!
imperial-deaconize-invocation-1 = Хороший, благородный человек был приведён сюда верой...
imperial-deaconize-invocation-2 = С руками, готовыми служить...
imperial-deaconize-invocation-3 = С сердцем, готовым слушать...
imperial-deaconize-invocation-4 = И душой, готовой следовать...
imperial-deaconize-invocation-5 = Да предложим мы свою руку взамен...
imperial-deaconize-invoke = И использовать их наилучшим образом.
imperial-deaconize-invite-title = Приглашение
imperial-deaconize-invite-text = Присоединиться к { $deity }? Ожидается, что вы будете следовать указам священника.
imperial-deaconize-invite-yes = Да
imperial-deaconize-invite-no = Нет

## Святая вода и защита разума

imperial-antimagic-mind-shielded = Что-то защищает разум { $target }!
imperial-holy-water-cult-start = Мерзкая святость начинает расползаться сияющими щупальцами по вашему разуму, вытравливая влияние Геометра Крови!
imperial-holy-water-cult-spells = Ваши кровавые обряды слабеют: святая вода выжигает их из тела!
imperial-holy-water-cult-seizure = У { $target } начинается припадок!
imperial-holy-water-cult-purged = Святая вода очищает ваш разум от власти Нар'Си!

## Удар библией по полу

imperial-bible-floor-smacked = Вы бьёте библией по полу.
imperial-bible-floor-revealed = Святая сила срывает покров: скрытое культом проступает вокруг!
