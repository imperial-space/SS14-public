imperial-blueprint-menu-button = Чертежи
imperial-blueprint-window-title = Чертежи построек
imperial-blueprint-save-title = Сохранить чертёж
imperial-blueprint-ghosts = Призраков строительства сейчас: { $count }. Расставьте их через меню строительства и сохраните.
imperial-blueprint-name-placeholder = Название чертежа
imperial-blueprint-save = Сохранить
imperial-blueprint-list-title = Мои чертежи
imperial-blueprint-refresh = Обновить
imperial-blueprint-folder = Открыть папку
imperial-blueprint-list-empty = Сохранённых чертежей пока нет.
imperial-blueprint-details = { $count } объектов · { $width }×{ $height } · автор: { $author }
imperial-blueprint-place = Разместить
imperial-blueprint-delete = Удалить
imperial-blueprint-delete-confirm = Точно?
imperial-blueprint-hint = Размещение: ЛКМ — поставить, R — повернуть, ПКМ — отмена. Файлы чертежей — обычный YAML, ими можно делиться.
imperial-blueprint-placing = Чертёж «{ $name }» на курсоре: ЛКМ — поставить, R — повернуть.
imperial-blueprint-saved = Чертёж «{ $name }» сохранён: { $count } объектов.
imperial-blueprint-deleted = Чертёж «{ $name }» удалён.
imperial-blueprint-placed = Поставлено призраков: { $placed }{ $skipped ->
    [0] {""}
   *[other] , не поместилось: { $skipped }
}{ $unknown ->
    [0] {""}
   *[other] , неизвестных рецептов: { $unknown }
}
imperial-blueprint-error-name = Введите название чертежа.
imperial-blueprint-error-empty = Нет призраков строительства — сначала расставьте их через меню строительства.
imperial-blueprint-error-io = Не удалось записать файл чертежа.
imperial-blueprint-error-grid = Чертёж можно поставить только на станции или шаттле.
