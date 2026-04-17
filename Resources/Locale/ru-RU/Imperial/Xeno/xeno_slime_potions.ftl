# Сообщения системы ксенобиологических зелий

## Зелье телепортации (Bluespace вода)
xeno-teleport-potion-saved = Точка телепортации сохранена!
xeno-teleport-potion-returned = Телепортация завершена!

## Зелье сепии / ZA WARUDO (Sepia плазма)
xeno-sepia-za-warudo-incoming = Время... останавливается...
xeno-sepia-za-warudo-activated = Время встало!

## Зелье послушания (Pink плазма)
xeno-obedience-potion-success = { $count ->
    [one] { $count } слайм успокоился.
    [few] { $count } слайма успокоились.
   *[other] { $count } слаймов успокоились.
}

## Зелье усиления экстрактора (Cerulean плазма)
xeno-extract-amplified = Экстракт усилен! Следующее использование даст в 3 раза больше реагентов.

## Зелье переноса сознания (Rainbow кровь)
xeno-consciousness-transfer-no-mind = У вас нет разума для переноса!
xeno-consciousness-transfer-no-target = Поблизости нет подходящей цели для переноса сознания.
xeno-consciousness-transfer-success = Ваше сознание перенесено в новое тело!

## Guidebook описания эффектов реагентов
reagent-effect-guidebook-xeno-teleport-potion = { $chance ->
   *[nonzero] Шанс { NATURALPERCENT($chance) }: телепортирует пользователя.
    [1] Телепортирует пользователя (сохраняет точку при первом применении, возвращает при втором).
}
reagent-effect-guidebook-xeno-sepia-za-warudo = { $chance ->
   *[nonzero] Шанс { NATURALPERCENT($chance) }: после 5 секундной задержки парализует все существа в радиусе 2.5 тайла на 15 секунд.
    [1] После 5 секундной задержки парализует все существа в радиусе 2.5 тайла на 15 секунд.
}
reagent-effect-guidebook-xeno-obedience-potion = { $chance ->
   *[nonzero] Шанс { NATURALPERCENT($chance) }: усмиряет ближайших слаймов.
    [1] Усмиряет ближайших слаймов, делая их нейтральными к пользователю.
}
reagent-effect-guidebook-xeno-slime-mutation-boost = { $chance ->
   *[nonzero] Шанс { NATURALPERCENT($chance) }: повышает шанс мутации при делении слайма.
    [1] Повышает шанс мутации при следующем делении слайма.
}
reagent-effect-guidebook-xeno-consciousness-transfer = { $chance ->
   *[nonzero] Шанс { NATURALPERCENT($chance) }: переносит сознание в ближайшее существо.
    [1] Переносит сознание пользователя в ближайшее живое существо в радиусе 3 тайлов.
}
