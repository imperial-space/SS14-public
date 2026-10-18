imperial-gun-maintenance-not-gun = Это не оружие!
imperial-gun-maintenance-no-need = Обслуживание не требуется.
imperial-gun-maintenance-done = Обслуживание завершено.
imperial-gun-maintenance-uses = Осталось применений: { $uses } из { $max }.

imperial-hair-dye-mode-hair = Режим: волосы.
imperial-hair-dye-mode-facial = Режим: борода.
imperial-hair-dye-title = Краска для волос
imperial-hair-dye-prompt = Цвет (#RRGGBB)
imperial-hair-dye-start = Вы начинаете наносить краску...
imperial-hair-dye-done = Краска нанесена.
imperial-hair-dye-nothing = Здесь нечего красить.

imperial-ph-booklet-empty = Книжка пуста!
imperial-ph-booklet-pages = Осталось полосок: { $pages }.
imperial-ph-paper-used = Эта полоска уже использована!
imperial-ph-paper-used-name = использованная полоска индикатора pH
imperial-ph-paper-used-desc = Кусочек бумаги, меняющий цвет в зависимости от pH раствора. Судя по цвету, pH около { $ph }.
imperial-ph-paper-result = Полоска показывает pH около { $ph }.

imperial-camera-film-full = В камере уже полный картридж.
imperial-camera-film-loaded = Вы вставляете новый картридж.

imperial-strange-seed-name = странное растение

imperial-dish-drive-placed = { CAPITALIZE($item) } в дисководе.
imperial-dish-drive-empty = Дисковод пуст.
imperial-dish-drive-taken = Вы достаёте { $item }.
imperial-dish-drive-examine-empty = Дисковод пуст.
imperial-dish-drive-examine = Внутри: { $list }.
imperial-dish-drive-verb-transmit = Отправить в утилизатор
imperial-dish-drive-no-bin = Дисковод жужжит: поблизости нет утилизатора!
imperial-dish-drive-transmitted = Дисковод с шумом отправляет { $count } шт. в { $bin }.
imperial-dish-drive-nothing = В дисководе нет мусора для утилизации.
imperial-dish-board-suction-on = Всасывание включено.
imperial-dish-board-suction-off = Всасывание выключено.
imperial-dish-board-transmit-on = Автоотправка в утилизатор включена.
imperial-dish-board-transmit-off = Автоотправка в утилизатор выключена.
imperial-dish-board-verb-transmit = Переключить автоотправку
imperial-dish-board-examine = Всасывание: { $suction ->
    [1] включено
   *[0] выключено
 }. Автоотправка: { $transmit ->
    [1] включена
   *[0] выключена
 }.

imperial-mail-sorter-full = Сортировщик переполнен.
imperial-mail-sorter-inserted = Вы кладёте { $item } в сортировщик.
imperial-mail-sorter-empty = В сортировщике нет почты!
imperial-mail-sorter-examine = Внутри писем: { $count }, свободно мест: { $free }.
imperial-mail-sorter-verb-dump = Высыпать почту
imperial-mail-sorter-verb-sort = Сортировать: { $department }
imperial-mail-sorter-dumped = Сортировщик высыпает { $count } писем на пол.
imperial-mail-sorter-none = Нет почты для отдела: { $department }.
imperial-mail-sorter-sorted = Успешно рассортировано писем: { $count }.
imperial-mail-sorter-unable = Не удалось рассортировать писем: { $count }.

imperial-custom-vendor-no-account = Счёт не найден.
imperial-custom-vendor-linked = Счёт привязан к автомату.
imperial-custom-vendor-unlinked = Счёт отвязан от автомата.
imperial-custom-vendor-restocked = Загружено предметов: { $count }.
imperial-custom-vendor-nothing = Нечего загружать!
imperial-custom-vendor-no-stacks = Россыпь может вызвать проблемы, заверните её в обёрточную бумагу.
imperial-custom-vendor-full = Слишком много товаров на складе.
imperial-custom-vendor-price-title = Цена товара
imperial-custom-vendor-price-prompt = Цена (кр.)
imperial-custom-vendor-loaded = { CAPITALIZE($item) } выставлен за { $price } кр.
imperial-custom-vendor-examine-locked = Автомат привязан к счёту: { $owner }. Отвяжите счёт перед разборкой!
imperial-custom-vendor-examine-count = Товаров: { $count } из { $max }.
imperial-custom-vendor-no-owner = У автомата нет владельца.
imperial-custom-vendor-reason = Покупка в автомате: { $vendor }
imperial-custom-vendor-no-funds = Недостаточно средств.
imperial-custom-vendor-sale = Продажа: { $item }
imperial-custom-vendor-sold = Продано: { $item } за { $price } кр.
imperial-custom-vendor-thanks = Спасибо за покупку!
imperial-custom-vendor-ui-no-owner = Владелец не назначен. Проведите картой или КПК.
imperial-custom-vendor-ui-owner = Владелец: { $owner } | Ваши деньги: { $cash } кр.
imperial-custom-vendor-ui-empty = Товаров нет.
imperial-custom-vendor-ui-set = Цена
imperial-custom-vendor-ui-take = Забрать

imperial-market-no-account = Аплинк искрит: у вас нет ID-карты или КПК со счётом.
imperial-market-reason = Сторонняя транзакция
imperial-market-no-funds = На счёте не хватает денег на «{ $item }».
imperial-market-launched = Аплинк сообщает: заказ запущен к станции с { $dir }.
imperial-market-dir-north = севера
imperial-market-dir-south = юга
imperial-market-dir-east = востока
imperial-market-dir-west = запада
imperial-market-teleporting = Аплинк сообщает: заказ будет телепортирован в точку ({ $x }, { $y }) через 60 секунд.
imperial-market-ui-title = Чёрный рынок
imperial-market-ui-shipping = Доставка:
imperial-market-ui-launch = Запуск (бесплатно)
imperial-market-ui-teleport = Телепорт (+75)
imperial-market-ui-no-account = Счёт не найден
imperial-market-ui-account = { $holder } | Баланс: { $balance } кр.
imperial-market-ui-empty = Товаров нет.
imperial-market-ui-stock = Осталось: { $stock }

imperial-service-constructor-soda = раздатчик газировки
imperial-service-constructor-booze = раздатчик алкоголя
imperial-service-constructor-sink = раковина
imperial-service-constructor-tank = бак с водой
imperial-service-constructor-grinder = измельчитель реагентов

imperial-camera-charging = Вспышка ещё заряжается!
imperial-camera-blending = Снимок ещё обрабатывается!
imperial-camera-no-film = Плёнка закончилась.
imperial-camera-left = Осталось снимков: { $count }.
imperial-camera-not-empty = Камера не пуста!
imperial-camera-hold = Камеру нужно держать в руках!
imperial-camera-examine-left = Осталось снимков: { $count }.
imperial-camera-examine-hint = Alt — настроить фокус (размер снимка), можно переключить печать в цвете или ч/б.
imperial-camera-examine-size = Размер снимка: [color=yellow]{ $width }x{ $height }[/color].
imperial-camera-verb-zoom = Настроить фокус
imperial-camera-zoom-title = Фокус
imperial-camera-zoom-x = Половина ширины апертуры (1–4)
imperial-camera-zoom-y = Половина высоты апертуры (1–4)
imperial-camera-verb-mono = Печатать в ч/б
imperial-camera-verb-color = Печатать в цвете
imperial-camera-mono = Печать: ч/б.
imperial-camera-color = Печать: в цвете.

imperial-photo-area = Это фотография участка { $width } на { $height } метров.
imperial-photo-mob = На фотографии также имеется { $name }.
imperial-photo-mob-hurt = На фотографии также имеется { $name } и выглядит немного раненым.
imperial-photo-mob-holding = В руках: { $items }.
imperial-photo-customise-title = Настройка фото (пустое поле — без изменений)
imperial-photo-customise-name = Название фото
imperial-photo-customise-desc = Описание
imperial-photo-customise-caption = Подпись
imperial-photo-closer = Чтобы рассмотреть фото, подойдите ближе!
imperial-photo-blank = { CAPITALIZE($photo) } кажется пустой...
imperial-photo-write-title = Надпись на обороте
imperial-photo-write-prompt = Что написать на обороте?
imperial-photo-verb-rename = Подписать фото
imperial-photo-rename-prompt = Как подписать фото?
imperial-photo-name = фотография
imperial-photo-name-label = фото - «{ $label }»
imperial-photo-scribble = На обороте написано: [italic]{ $text }[/italic]
