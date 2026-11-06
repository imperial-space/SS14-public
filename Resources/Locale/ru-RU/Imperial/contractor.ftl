## Аплинк Синдиката (code/modules/uplink/uplink_items/contractor.dm)

uplink-contractor-kit-name = Набор контрактника
uplink-contractor-kit-desc = Набор Синдиката для работы контрактником: планшет с программой контрактов, специализированный скафандр, форма и маска-хамелеон, дубинка контрактника и руководство. Открывает в аплинке товары контрактника.

uplink-contractor-reroll-name = Перевыбор контрактов
uplink-contractor-reroll-desc = Запросите обновление вашего текущего списка контрактов. Создаст новую цель, оплату и места для транспортировки ваших текущих доступных контрактов.

uplink-contractor-pinpointer-name = Пинпоинтер контрактника
uplink-contractor-pinpointer-desc = Пинпоинтер, который находит цели даже без активных датчиков костюма. Из-за использования эксплойта внутри системы он не может определять местоположение с той же точностью, что и традиционные модели. Становится навсегда заблокированным для пользователя, который первым его активировал.

uplink-contractor-extraction-name = Набор эвакуации Фултона
uplink-contractor-extraction-desc = Для того, чтобы доставлять вашу цель через станцию к трудным зонам отправки. Установите маяк в безопасное место и соедините устройство. Активация устройства на вашей цели отправит её к маяку — убедитесь, что она не сможет просто убежать!

uplink-contractor-partner-name = Подкрепление контрактника
uplink-contractor-partner-desc = Подкрепление в виде оперативника будет отправлено для помощи в достижении ваших целей — оно оплачивается отдельно и не отнимает часть вашей прибыли.

ghost-role-information-contractor-partner-name = Агент поддержки Синдиката
ghost-role-information-contractor-partner-description = Следуйте приказам контрактника, который вас вызвал.

## Роль

roles-antag-contractor-name = Контрактник
roles-antag-contractor-objective = Выполняйте контракты Синдиката на похищение через программу контрактов на планшете.
role-subtype-contractor = контрактник

## Программа Syndicate Contract Uplink (contractor_program.dm)

contractor-uplink-window-title = Syndicate Contract Uplink
contractor-uplink-login = ЗАРЕГИСТРИРОВАННЫЙ ПОЛЬЗОВАТЕЛЬ
contractor-uplink-continue = ПРОДОЛЖИТЬ
contractor-uplink-view-info = Посмотреть информацию еще раз
contractor-uplink-status-title = Статус контрактника
contractor-uplink-status-redeemable = Доступные ТК
contractor-uplink-status-earned = Заработанные ТК
contractor-uplink-status-completed = Выполненные контракты
contractor-uplink-status-current = Текущий статус
contractor-uplink-status-active = АКТИВНЫЙ
contractor-uplink-redeem = Забрать
contractor-uplink-contracts-title = Доступные контракты
contractor-uplink-call-extraction = Call Extraction
contractor-uplink-accept = Accept
contractor-uplink-abort = Abort
contractor-uplink-payout = {$payout} (+{$bonus}) ТК
contractor-uplink-dropoff = Dropoff Location:
contractor-uplink-invalid-target = Неверная цель
contractor-uplink-dropoff-locator = Локатор зоны отправки
contractor-uplink-dismiss = Dismiss

contractor-error-unauthorized = UNAUTHORIZED USER
contractor-error-dropoff = Either both you or your target aren't at the dropoff location, or the pod hasn't got a valid place to land. Clear space, or make sure you're both inside.
contractor-error-already-extracting = Already extracting... Place the target into the pod. If the pod was destroyed, this contract is no longer possible.

contractor-payment-hands = Ваша оплата материализуется в ваших руках!
contractor-payment-floor = Ваша оплата материализуется на полу.

contractor-rank-unknown = Неизвестно
contractor-location-confirmed = LOCATION CONFIRMED
contractor-direction-north = Север
contractor-direction-south = Юг
contractor-direction-east = Восток
contractor-direction-west = Запад
contractor-direction-northeast = Северо-восток
contractor-direction-northwest = Северо-запад
contractor-direction-southeast = Юго-восток
contractor-direction-southwest = Юго-запад
contractor-direction-invalid = ???

## Контракт (syndicate_contract.dm)

contractor-ransom-paid = Вот ваша доля выкупа, агент: { $amount } кредитов.

contractor-victim-1 = Вы чувствуете себя странно...
contractor-victim-2 = Эта капсула что-то сделала с вами...
contractor-victim-3 = Ваша голова раскалывается... Такое ощущение, что ваш череп сейчас лопнет!
contractor-victim-4 = Ваша голова болит...
contractor-victim-5 = Множество голосов отдаются эхом в вашей голове... «Вам разум хранил множество важных секретов — мы благодарим вас за предоставления их нам. Ваша ценность упала, и вы будете выкуплены обратно на свою станцию. Мы всегда получаем свое, поэтому это только дело времени когда вы вернетесь обратно на ваш корабль...»
contractor-victim-returned = Капсула выбрасывает вас обратно на станцию. Голова кружится, а вещи разбросаны вокруг.

contractor-round-end-header = [color=red]Контрактники Синдиката:[/color]
contractor-round-end-line = [bold]{$name}[/bold] — выполнено контрактов: {$completed}, заработано ТК: {$tc}.

contractor-command-no-body = У игрока нет тела.
contractor-command-not-traitor = Игрок не является предателем.

## Сообщение о розыске (strings/wanted_message.json)

contractor-wanted-message = {$base} {$verb} {$noun} {$location}.

contractor-wanted-base-1 = Скрывается от закона из-за того, что
contractor-wanted-base-2 = Владеет ценной информацией о том, как
contractor-wanted-base-3 = Должен быть допрошен о том, как
contractor-wanted-base-4 = Может быть выкуплен Nanotrasen за то, что
contractor-wanted-base-5 = Разыскивается Синдикатом за то, что

contractor-wanted-verb-1 = случайно уничтожил
contractor-wanted-verb-2 = убил по заказу
contractor-wanted-verb-3 = подружился с
contractor-wanted-verb-4 = близко дружил с
contractor-wanted-verb-5 = взорвал
contractor-wanted-verb-6 = клонировал
contractor-wanted-verb-7 = сотрудничал с
contractor-wanted-verb-8 = уничтожил
contractor-wanted-verb-9 = проигнорировал
contractor-wanted-verb-10 = похитил
contractor-wanted-verb-11 = убил
contractor-wanted-verb-12 = знает информацию о
contractor-wanted-verb-13 = зверски убил
contractor-wanted-verb-14 = выдавал себя за
contractor-wanted-verb-15 = отробастил
contractor-wanted-verb-16 = саботировал
contractor-wanted-verb-17 = соблазнил
contractor-wanted-verb-18 = подскользнул
contractor-wanted-verb-19 = украл

contractor-wanted-noun-soap = кусок мыла
contractor-wanted-noun-cult = культ
contractor-wanted-noun-gondola = гондолу
contractor-wanted-noun-gold-gondola = гондолу из чистого золота
contractor-wanted-noun-supermatter = осколок суперматерии
contractor-wanted-noun-bus = подозрительный автобус
contractor-wanted-noun-toolbox = ящик с инструментами
contractor-wanted-noun-nothing = абсолютно ничего
contractor-wanted-noun-admins = администрацию
contractor-wanted-noun-centcom-uniform = форму Центкома NT
contractor-wanted-noun-bluespace = блюспейс-кристаллы
contractor-wanted-noun-clown-tears = слёзы клоуна
contractor-wanted-noun-pete = Кубинского Пита
contractor-wanted-noun-nt-tech = экспериментальную технологию Nanotrasen
contractor-wanted-noun-syndicate-docs = внутренние документы Синдиката
contractor-wanted-noun-jfk = Джона Ф. Кеннеди 2
contractor-wanted-noun-lizards = унатхов
contractor-wanted-noun-calendar = календарь Nanotrasen с купальниками
contractor-wanted-noun-beepsky = офицера Бипски
contractor-wanted-noun-billion = миллиард кредитов
contractor-wanted-noun-floor-clowns = исследования напольных клоунов
contractor-wanted-noun-plans = секретные планы
contractor-wanted-noun-shapeshifters = существ-перевёртышей
contractor-wanted-noun-anime = архив аниме
contractor-wanted-noun-hand-tele = ручной телепорт
contractor-wanted-noun-two-million = два миллиона кредитов
contractor-wanted-noun-wgw = WGW
contractor-wanted-noun-yalper = YALPER
contractor-wanted-noun-mother = твою мамку

contractor-wanted-location-necropolis = глубоко в некрополе
contractor-wanted-location-bar-fight = во время пьяной драки в баре
contractor-wanted-location-mars = в подворотне на Марсе
contractor-wanted-location-nt-facility = на охраняемом объекте NT
contractor-wanted-location-syndicate-base = на удалённой базе Синдиката
contractor-wanted-location-station = на космической станции
contractor-wanted-location-virgo = на орбитальной станции Вирго
contractor-wanted-location-deep-space = где-то в глубоком космосе
contractor-wanted-location-centcom = во время проникновения в штаб Центкома
contractor-wanted-location-shuttle = на эвакуационном шаттле
contractor-wanted-location-bathroom = застряв в туалете

## Руководство контрактника (contractor_items.dm)

contractor-guide-content = Приветствую вас, агент, поздравляю с новой должностью контрактника. Помимо уже поставленных целей, этот комплект предоставит вам возможность брать контракты с оплатой в виде телекристаллов.

    В комплекте мы выдаем вам специализированный скафандр контрактника. Он намного компактнее, и в нем удобнее передвигаться, в отличие от обычного скафандра Синдиката, доступного в вашем аплинке. Мы также предоставляем вам костюм и маску хамелеон, которые оба могут быть изменены в любую форму, которая вам может быть необходима.

    Выданная вам дубинка бьет сильнее, чем та, к которой вы привыкли, и, скорее всего, будет вашим лучшим оружием для похищения ваших целей.

    В аплинке Синдиката после входа в программу контрактов откроются уникальные предметы и возможности контрактника.

    {"["}head=3]Использование планшета[/head]
    1. Откройте программу Syndicate Contract Uplink.
    2. В этой программе вы можете принять контракт, а также получить оплату с выполненных контрактов.
    3. Цифры в скобках — это ваша премия за то, что вы доставите вашу цель [bold]живой[/bold]. Вы получите определённую сумму независимо от того, будет ли ваша цель живой или нет.
    4. Чтобы выполнить контракт, вам нужно принести цель до назначенного места высадки, вызвать под отправки и положить цель внутрь пода.

    Будьте осторожны, принимая контракт. Хоть вы и сможете видеть местоположение точки высадки, отмена контракта сделает его более недоступным для выбора.

    {"["}head=3]Отправление[/head]
    1. Убедитесь, что и вы, и ваша цель находитесь на месте высадки.
    2. Вызовите под отправки и отойдите от зоны приземления.
    3. Если попытка вызвать под провалилась, проверьте, чтобы ваша цель была внутри зоны и было достаточно места для приземления пода.
    4. Схватите свою цель и поместите её в под.

    {"["}head=3]Выкупы[/head]
    Нам нужна ваша цель по нашим собственным причинам, но мы вернём её за выкуп в район вашей миссии, как только она будет больше не нужна. Она вернётся обратно туда, откуда вы её отправили, через несколько минут. Не волнуйтесь, мы отдаём вам часть выкупа — наличными, помимо оплаты телекристаллами.

    Удачи, агент. Вы можете сжечь этот документ с помощью прилагаемой зажигалки.

## Окно аплинка контрактника: загрузка терминала и справка
contractor-uplink-terminal-1 = Запись биометрических данных...
contractor-uplink-terminal-2 = Анализ встроенной информации о синдикате...
contractor-uplink-terminal-3 = СТАТУС ПОДТВЕРЖДЕН
contractor-uplink-terminal-4 = Обращение к базе данных синдиката...
contractor-uplink-terminal-5 = Ожидание ответа...
contractor-uplink-terminal-6 = Ожидание ответа...
contractor-uplink-terminal-7 = Ожидание ответа...
contractor-uplink-terminal-8 = Ожидание ответа...
contractor-uplink-terminal-9 = Ожидание ответа...
contractor-uplink-terminal-10 = Ожидание ответа...
contractor-uplink-terminal-11 = Ответ получен, аккаунт 4851234...
contractor-uplink-terminal-12 = ПОДТВЕРДИТЬ АККАУНТ { $account }
contractor-uplink-terminal-13 = Настройка личных аккаунтов...
contractor-uplink-terminal-14 = АККАУНТ КОНТРАКТНИКА СОЗДАН
contractor-uplink-terminal-15 = Поиск доступных контрактов...
contractor-uplink-terminal-16 = Поиск доступных контрактов...
contractor-uplink-terminal-17 = Поиск доступных контрактов...
contractor-uplink-terminal-18 = Поиск доступных контрактов...
contractor-uplink-terminal-19 = КОНТРАКТЫ НАЙДЕНЫ
contractor-uplink-terminal-20 = ДОБРО ПОЖАЛОВАТЬ, АГЕНТ
contractor-uplink-info-1 = SyndTract v2.0
contractor-uplink-info-2 = {""}
contractor-uplink-info-3 = Мы определили потенциально ценные цели, которые
contractor-uplink-info-4 = в настоящее время находятся в районе вашей миссии. Они могут
contractor-uplink-info-5 = хранить ценную информацию, которая может иметь важное
contractor-uplink-info-6 = значение для нашей организации.
contractor-uplink-info-7 = {""}
contractor-uplink-info-8 = Ниже перечислены все доступные вам контракты. Вы
contractor-uplink-info-9 = должны довести заданную цель до назначенной
contractor-uplink-info-10 = зоны отправки, и связаться с нами через аплинк. Мы отправим
contractor-uplink-info-11 = специализированную капсулу для транспортировки, куда нужно поместить тело.
contractor-uplink-info-12 = {""}
contractor-uplink-info-13 = Мы хотим, чтобы цели были живыми; мы платим меньшие
contractor-uplink-info-14 = суммы если цель будет мертва, так как вы просто не получите указанный
contractor-uplink-info-15 = бонус. Вы можете получить свою оплату через этот аплинк в
contractor-uplink-info-16 = форме телекристаллов, которые могут быть вложены в
contractor-uplink-info-17 = обычный аплинк Синдиката для приобретения необходимого вам снаряжения.
contractor-uplink-info-18 = Мы предоставим вам эти кристаллы в тот момент, когда вы отправите
contractor-uplink-info-19 = цель к нам, телекристаллы можно получить в любое время через
contractor-uplink-info-20 = эту систему.
contractor-uplink-info-21 = {""}
contractor-uplink-info-22 = Похищенные цели будут выкуплены обратно на станцию после того,
contractor-uplink-info-23 = как их знания будут извлечены, и вам будет предоставлена
contractor-uplink-info-24 = часть выкупа. Вам следует помнить, что они могут
contractor-uplink-info-25 = идентифицировать вас, когда они вернутся. Мы предоставляем вам
contractor-uplink-info-26 = стандартное снаряжение контрактника, которое поможет скрыть вашу
contractor-uplink-info-27 = личность.
