## Маг Федерации волшебников (wizard.dm)

roles-antag-imperial-wizard-name = Космический волшебник
roles-antag-imperial-wizard-objective = Продемонстрируйте свои невероятные и разрушительные магические способности.
imperial-wizard-round-end-agent-name = космический волшебник
objective-issuer-imperial-wizard = [color=#8a2be2]Федерация волшебников[/color]
imperial-wizard-briefing =
    Вы — космический волшебник!
    Федерация волшебников дала вам задание. Купите заклинания в книге, телепортируйтесь на станцию свитком и действуйте!
imperial-wizard-rename-title = Смена имени
imperial-wizard-rename-prompt = Ваше имя — «{ $random }». Хотите изменить его на другое?
imperial-wizard-roundend-name = [bold]{ $name }[/bold]: завершено Великих ритуалов — { $rituals }.
imperial-wizard-roundend-none = { $name } не покупал никаких заклинаний!
imperial-wizard-roundend-used = { $name } использовал следующие заклинания: { $spells }.

## Телепорт и свиток

imperial-wizard-teleport-title = Телепорт
imperial-wizard-teleport-prompt = Выберите зону для телепортации.
imperial-wizard-teleport-no-room = В этой зоне некуда переместиться!
imperial-wizard-scroll-uses = Осталось использований: { $uses }.
imperial-wizard-scroll-dust = { CAPITALIZE($scroll) } исчерпывает силы и рассыпается в прах!
imperial-list-input-search = Поиск...
imperial-list-input-submit = Выбрать
imperial-list-input-cancel = Отмена

## Заклинания (spell.dm)

imperial-spell-not-here = Здесь нельзя произнести «{ $spell }»!
imperial-spell-need-vow = Сначала нужно дать обет молчания!
imperial-spell-need-robe = Без мантии вы недостаточно сильны!
imperial-spell-need-hat = Без шляпы вы недостаточно сильны!
imperial-spell-humans-only = «{ $spell }» могут произносить только люди!
imperial-spell-cant-speak = Вы не можете выговорить слова заклинания «{ $spell }»!
imperial-spell-cant-gesture = Вы не можете правильно сложить руки для «{ $spell }»!
imperial-spell-title = { $name } [{ $title }]
imperial-spell-title-2 = эффективное
imperial-spell-title-3 = ускоренное
imperial-spell-title-4 = бесплатное
imperial-spell-title-5 = мгновенное
imperial-spell-title-6 = нелепое
imperial-spell-teleport-invocation = SCYAR NILA

## Книга заклинаний (wizard_spellbook.dm, tgui Spellbook)

imperial-spellbook-title = Книга заклинаний
imperial-spellbook-bind = Вы привязываете { $book } к себе.
imperial-spellbook-not-owner = Вы не признаётесь владельцем { $book }, и она не собирается открываться!
imperial-spellbook-apprentice = Если вас поймают за подглядыванием в книгу заклинаний учителя, вас, скорее всего, отчислят из Академии волшебников. Лучше не стоит.
imperial-spellbook-examine-owner = На передней обложке есть небольшая подпись: «{ $owner }».
imperial-spellbook-examine-no-owner = Похоже, у неё нет автора.
imperial-spellbook-lesser-life = Книга, похоже, не слушает низшие формы жизни.
imperial-spellbook-max-level = Это заклинание не может быть более улучшено!
imperial-spellbook-improved = Вы улучшили «{ $before }» в «{ $after }».
imperial-spellbook-learned = Вы выучили — { $spell }.
imperial-spellbook-perk-known = Этот перк уже изучен!
imperial-spellbook-lottery-free = Это заклинание досталось вам бесплатно!
imperial-spellbook-no-refund = Без возврата.
imperial-spellbook-refund-den = Вы можете вернуть очки за заклинания только в логове волшебника!
imperial-spellbook-need-all-points = Для этого вам нужны все очки заклинаний!
imperial-spellbook-item-no-refund = Похоже, книга не хочет возвращать очки за { $item }.
imperial-spellbook-item-used = Предмет уже использован, вы не можете вернуть себе очки!
imperial-spellbook-item-refunded = Вы возвращаете предмет в книгу заклинаний, возвращая себе очки.
imperial-spellbook-points = Осталось очков: { $points }
imperial-spellbook-search = Поиск заклинания...
imperial-spellbook-stop-search = Остановить { $verb }
imperial-spellbook-prev = Предыдущая страница
imperial-spellbook-next = Следующая страница
imperial-spellbook-cast-times = Прочитано раз: { $times }
imperial-spellbook-not-cast = Ещё не прочитано.
imperial-spellbook-cooldown = { $seconds }с перезарядка
imperial-spellbook-cost = Очки: { $cost }
imperial-spellbook-garb-required = Требуется одежда волшебника.
imperial-spellbook-garb-free = Можно произносить без одежды волшебника.
imperial-spellbook-buyword-learn = Выучить
imperial-spellbook-buyword-summon = Призвать
imperial-spellbook-buyword-cast = Провести
imperial-spellbook-buyword-accept = Принять
imperial-spellbook-refund = Вернуть
imperial-spellbook-no-refund-notice = Возврата нет.
imperial-spellbook-empty = Здесь пока пусто.
imperial-spellbook-federation-locked = Федерация волшебников заблокировала эту страницу.
imperial-spellbook-point-locked = У вас недостаточно очков для использования этой страницы.

imperial-spellbook-page-enscribedname = Вписанное имя
imperial-spellbook-page-tableofcontents = Оглавление
imperial-spellbook-page-offensive = Наступление
imperial-spellbook-page-defensive = Защита
imperial-spellbook-page-mobility = Мобильность
imperial-spellbook-page-assistance = Поддержка
imperial-spellbook-page-challenges = Испытания
imperial-spellbook-page-rituals = Ритуалы
imperial-spellbook-page-loadouts = Комплекты
imperial-spellbook-page-randomize = Случайно
imperial-spellbook-page-perks = Перки
imperial-spellbook-page-tableofcontents2 = Оглавление

imperial-spellbook-blurb-enscribedname = Эта книга отвечает только своему владельцу и, конечно, должна его иметь. Постоянство договора между книгой заклинаний и её владельцем гарантирует, что такой мощный артефакт не попадёт в руки врага и не будет использован способами, нарушающими правила Федерации, такими как обмен заклинаниями.
imperial-spellbook-blurb-offensive = Заклинания и предметы, направленные на ослабление и уничтожение.
imperial-spellbook-blurb-defensive = Заклинания и предметы, направленные на повышение вашей выживаемости или снижение способности врагов атаковать.
imperial-spellbook-blurb-mobility = Заклинания и предметы, направленные на улучшение вашей способности к перемещению. Рекомендуется взять хотя бы одно.
imperial-spellbook-blurb-assistance = Заклинания и предметы, направленные на привлечение внешних сил для помощи вам или улучшения ваших других предметов и способностей.
imperial-spellbook-blurb-challenges = Федерация волшебников ищет проявления силы. Вооружив станцию против вас, вы увеличите опасность, но получите больше зарядов для вашей книги заклинаний.
imperial-spellbook-blurb-rituals = Эти мощные заклинания изменяют саму ткань реальности. Не всегда в вашу пользу.
imperial-spellbook-blurb-loadouts = Федерация волшебников признаёт, что иногда выбор труден. Здесь вы можете выбрать одобренные комплекты волшебника.
imperial-spellbook-blurb-randomize = Если вам не понравились предложенные комплекты, вы можете предаться хаосу. Не рекомендуется для начинающих волшебников.
imperial-spellbook-blurb-perks = Перки — это полезные (и не очень) улучшения души и тела, собранные со всех уголков вселенной.

imperial-spellbook-toc-rename = Смена имени
imperial-spellbook-toc-contents = Содержание
imperial-spellbook-toc-deadly = Смертельные заклинания
imperial-spellbook-toc-defensive = Защитные заклинания
imperial-spellbook-toc-travel = Магическое перемещение
imperial-spellbook-toc-assistance = Помощь и призыв
imperial-spellbook-toc-challenges = Испытания
imperial-spellbook-toc-rituals = Ритуалы
imperial-spellbook-toc-loadouts = Одобренные наборы волшебника
imperial-spellbook-toc-random = Мистический рандомайзер
imperial-spellbook-toc-perks = Перки

imperial-spellbook-loadout-buy = Приобрести набор
imperial-spellbook-loadout-confirm = Подтвердить покупку?
imperial-spellbook-loadout_classic-name = Классический Волшебник
imperial-spellbook-loadout_classic-blurb = Это классический волшебник, безумно популярный в 2550-х годах. Включает Огненный шар, Волшебную ракету, Ei Nath и Эфирное перемещение. Каждую часть этого набора легко использовать.
imperial-spellbook-loadout_classic-author = Добавлено Архиканцлером Греем.
imperial-spellbook-loadout_hammer-name = Сила Мьёльнира
imperial-spellbook-loadout_hammer-blurb = Сила могучего Мьёльнира! Лучше не терять его. Этот набор включает Призыв предмета, Мутацию, Скачок, Силовую стену, Разряд Теслы и Мьёльнир. Мутация — ваша утилита в этом случае.
imperial-spellbook-loadout_hammer-author = Добавлено Егудиэлем Миродрожателем.
imperial-spellbook-loadout_army-name = Фантастическая Армия
imperial-spellbook-loadout_army-blurb = Зачем убивать, когда другие с радостью сделают это за вас? Примите хаос: Осколки камня душ, Посох изменения, Некромантский камень, Телепорт и Эфирное перемещение!
imperial-spellbook-loadout_army-author = Добавлено Просперо Зачарованным Камнем.
imperial-spellbook-loadout_tap-name = Похититель Душ
imperial-spellbook-loadout_tap-blurb = Примите тьму и воспользуйтесь своей душой. Используйте мощные заклинания, такие как Ei Nath, меняя тела Обменом разумом и начиная Высасывание души заново.
imperial-spellbook-loadout_tap-author = Добавлено Томом Пустым.

imperial-spellbook-semirandom = Частичная рандомизация
imperial-spellbook-fullrandom = Полная рандомизация
imperial-spellbook-random-confirm = Погнали?
imperial-spellbook-semirandom-desc = Частичная рандомизация гарантирует получение хотя бы некоторой мобильности и смертоносности. Гарантировано получение заклинаний на { $points } очков.
imperial-spellbook-fullrandom-desc = Полная рандомизация даст что угодно. И назад дороги нет! Гарантировано получение заклинаний на { $points } очков.

## Записи книги

imperial-spellbook-teleport-name = Телепорт
imperial-spellbook-teleport-desc = Телепортирует вас в выбранную вами область.
