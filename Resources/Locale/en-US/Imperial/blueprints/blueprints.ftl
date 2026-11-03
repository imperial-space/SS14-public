imperial-blueprint-menu-button = Blueprints
imperial-blueprint-window-title = Construction blueprints
imperial-blueprint-save-title = Save blueprint
imperial-blueprint-ghosts = Construction ghosts placed: { $count }. Place them with the construction menu, then save.
imperial-blueprint-name-placeholder = Blueprint name
imperial-blueprint-save = Save
imperial-blueprint-list-title = My blueprints
imperial-blueprint-refresh = Refresh
imperial-blueprint-folder = Open folder
imperial-blueprint-list-empty = No saved blueprints yet.
imperial-blueprint-details = { $count } objects · { $width }×{ $height } · author: { $author }
imperial-blueprint-place = Place
imperial-blueprint-delete = Delete
imperial-blueprint-delete-confirm = Sure?
imperial-blueprint-hint = Placing: LMB — place, R — rotate, RMB — cancel. Blueprint files are plain YAML and can be shared.
imperial-blueprint-placing = Blueprint "{ $name }" is on the cursor: LMB — place, R — rotate.
imperial-blueprint-saved = Blueprint "{ $name }" saved: { $count } objects.
imperial-blueprint-deleted = Blueprint "{ $name }" deleted.
imperial-blueprint-placed = Ghosts placed: { $placed }{ $skipped ->
    [0] {""}
   *[other] , did not fit: { $skipped }
}{ $unknown ->
    [0] {""}
   *[other] , unknown recipes: { $unknown }
}
imperial-blueprint-error-name = Enter a blueprint name.
imperial-blueprint-error-empty = No construction ghosts — place some with the construction menu first.
imperial-blueprint-error-io = Could not write the blueprint file.
imperial-blueprint-error-grid = Blueprints can only be placed on a station or shuttle.
