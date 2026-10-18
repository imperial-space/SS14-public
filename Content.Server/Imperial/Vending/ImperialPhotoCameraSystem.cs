using System.Linq;
using System.Numerics;
using Content.Server.Administration;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Vending;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Vending;

/// <summary>
/// Камера TG (code/modules/photography): клиент фотографа рисует область (2·aperture−1)² клеток,
/// сервер добавляет описание существ в кадре и печатает фото. Плёнка, вспышка, фокус и ч/б печать — как в TG.
/// </summary>
public sealed class ImperialPhotoCameraSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly PointLightSystem _light = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private const int MaxImageBytes = 512 * 1024;
    private const int ThumbnailBytes = 8 * 8 * 4;
    private static readonly ProtoId<TagPrototype> WriteTag = "Write";
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(10);

    private int _nextRequest;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialPhotoCameraComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ImperialPhotoCameraComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ImperialPhotoCameraComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ImperialPhotoCameraComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeNetworkEvent<ImperialCameraCaptureResponseEvent>(OnCaptureResponse);

        SubscribeLocalEvent<ImperialPhotoComponent, UseInHandEvent>(OnPhotoUse);
        SubscribeLocalEvent<ImperialPhotoComponent, ExaminedEvent>(OnPhotoExamined);
        SubscribeLocalEvent<ImperialPhotoComponent, InteractUsingEvent>(OnPhotoInteractUsing);
        SubscribeLocalEvent<ImperialPhotoComponent, GetVerbsEvent<Verb>>(OnPhotoVerbs);
    }

    private static int Meters(int aperture) => 2 * aperture - 1;

    #region Камера

    /// <summary>ranged_interact_with_atom() → attempt_picture().</summary>
    private void OnAfterInteract(Entity<ImperialPhotoCameraComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        var target = _transform.ToMapCoordinates(args.ClickLocation);

        if (!ent.Comp.On)
        {
            _popup.PopupEntity(Loc.GetString("imperial-camera-charging"), user, user);
            return;
        }

        if (ent.Comp.Blending)
        {
            _popup.PopupEntity(Loc.GetString("imperial-camera-blending"), user, user);
            return;
        }

        // target_turf in view(view_size, user)
        if (!_examine.InRangeUnOccluded(user, target, ent.Comp.Range))
            return;

        // Центр клетки, как get_turf().
        var rotation = Angle.Zero;
        if (_transform.GetGrid(args.ClickLocation) is { } grid && TryComp<Robust.Shared.Map.Components.MapGridComponent>(grid, out var gridComp))
        {
            rotation = _transform.GetWorldRotation(grid);
            target = _map.GridTileToWorld(grid, gridComp, _map.TileIndicesFor(grid, gridComp, args.ClickLocation));
        }
        else
        {
            target = new MapCoordinates(new Vector2(MathF.Floor(target.X) + 0.5f, MathF.Floor(target.Y) + 0.5f), target.MapId);
        }
        OnFlash(ent);

        ent.Comp.Blending = true;
        ent.Comp.Photographer = user;
        ent.Comp.Target = target;
        ent.Comp.Rotation = rotation;
        ent.Comp.Description = Describe(ent, target);
        var id = ent.Comp.RequestId = ++_nextRequest;

        if (TryComp<ActorComponent>(user, out var actor))
        {
            RaiseNetworkEvent(new ImperialCameraCaptureRequestEvent(id, target, rotation, Meters(ent.Comp.SizeX), Meters(ent.Comp.SizeY), ent.Comp.Monochrome), actor.PlayerSession);
            var uid = ent.Owner;
            Robust.Shared.Timing.Timer.Spawn(CaptureTimeout, () =>
            {
                if (TryComp<ImperialPhotoCameraComponent>(uid, out var cam) && cam.Blending && cam.RequestId == id)
                    cam.Blending = false;
            });
        }
        else
        {
            AfterPicture(ent, user, Array.Empty<byte>(), Array.Empty<byte>());
        }
    }

    /// <summary>on_flash(): камера гаснет на cooldown, вспышка светит FLASH_LIGHT_DURATION.</summary>
    private void OnFlash(Entity<ImperialPhotoCameraComponent> ent)
    {
        ent.Comp.On = false;
        _appearance.SetData(ent, ImperialCameraVisuals.On, false);
        _light.SetEnabled(ent, true);

        var uid = ent.Owner;
        Robust.Shared.Timing.Timer.Spawn(ent.Comp.FlashDuration, () =>
        {
            if (!Deleted(uid))
                _light.SetEnabled(uid, false);
        });
        Robust.Shared.Timing.Timer.Spawn(ent.Comp.Cooldown, () =>
        {
            if (!TryComp<ImperialPhotoCameraComponent>(uid, out var cam))
                return;
            cam.On = true;
            _appearance.SetData(uid, ImperialCameraVisuals.On, true);
        });
    }

    /// <summary>capture_image(): «Это фотография участка W на H метров.» и get_photo_description() каждого существа.</summary>
    private string Describe(Entity<ImperialPhotoCameraComponent> ent, MapCoordinates center)
    {
        var width = Meters(ent.Comp.SizeX);
        var height = Meters(ent.Comp.SizeY);
        var lines = new List<string> { Loc.GetString("imperial-photo-area", ("width", width), ("height", height)) };

        var box = new Box2Rotated(Box2.CenteredAround(center.Position, new Vector2(width, height)), ent.Comp.Rotation, center.Position);
        foreach (var mob in _lookup.GetEntitiesIntersecting(center.MapId, box, LookupFlags.Uncontained).Where(HasComp<MobStateComponent>))
        {
            var name = Identity.Name(mob, EntityManager);
            var hurt = false;
            if (TryComp<DamageableComponent>(mob, out var damageable) && _thresholds.TryGetThresholdForState(mob, MobState.Critical, out var crit))
                hurt = _damageable.GetTotalDamage((mob, damageable)) > crit.Value * 0.25f;

            var held = _hands.EnumerateHeld(mob).Select(h => Name(h)).ToList();
            var line = Loc.GetString(hurt ? "imperial-photo-mob-hurt" : "imperial-photo-mob", ("name", name));
            if (held.Count > 0)
                line += " " + Loc.GetString("imperial-photo-mob-holding", ("items", string.Join(", ", held)));
            lines.Add(line);
        }

        return string.Join("\n", lines);
    }

    private void OnCaptureResponse(ImperialCameraCaptureResponseEvent msg, EntitySessionEventArgs args)
    {
        var query = EntityQueryEnumerator<ImperialPhotoCameraComponent>();
        while (query.MoveNext(out var uid, out var cam))
        {
            if (!cam.Blending || cam.RequestId != msg.Id || cam.Photographer is not { } user || args.SenderSession.AttachedEntity != user)
                continue;

            var (image, thumb) = Process(msg.Image, cam.Monochrome);
            AfterPicture((uid, cam), user, image, thumb);
            return;
        }
    }

    /// <summary>print_monochrome и get_small_icon(): ч/б перевод и миниатюра 8×8 (усреднение блоков).</summary>
    private static (byte[] Image, byte[] Thumbnail) Process(byte[] png, bool monochrome)
    {
        if (png.Length == 0 || png.Length > MaxImageBytes)
            return (Array.Empty<byte>(), Array.Empty<byte>());

        try
        {
            if (!ImperialPng.TryDecode(png, out var width, out var height, out var rgba) || width > 7 * 32 || height > 7 * 32)
                return (Array.Empty<byte>(), Array.Empty<byte>());

            if (monochrome)
                png = ImperialPng.Encode(width, height, rgba, true);

            var thumb = new byte[ThumbnailBytes];
            for (var ty = 0; ty < 8; ty++)
            for (var tx = 0; tx < 8; tx++)
            {
                int x0 = tx * width / 8, x1 = Math.Max(x0 + 1, (tx + 1) * width / 8);
                int y0 = ty * height / 8, y1 = Math.Max(y0 + 1, (ty + 1) * height / 8);
                long r = 0, g = 0, b = 0, n = 0;
                for (var y = y0; y < y1; y++)
                for (var x = x0; x < x1; x++)
                {
                    var o = (y * width + x) * 4;
                    r += rgba[o]; g += rgba[o + 1]; b += rgba[o + 2]; n++;
                }

                var i = (ty * 8 + tx) * 4;
                byte cr = (byte) (r / n), cg = (byte) (g / n), cb = (byte) (b / n);
                if (monochrome)
                    cr = cg = cb = (byte) Math.Clamp(0.299f * cr + 0.587f * cg + 0.114f * cb, 0, 255);
                thumb[i] = cr;
                thumb[i + 1] = cg;
                thumb[i + 2] = cb;
                thumb[i + 3] = 255;
            }

            return (png, thumb);
        }
        catch (Exception)
        {
            return (Array.Empty<byte>(), Array.Empty<byte>());
        }
    }

    /// <summary>after_picture() → printpicture(): звук, вопрос о подписи, печать фото в руки.</summary>
    private void AfterPicture(Entity<ImperialPhotoCameraComponent> ent, EntityUid user, byte[] image, byte[] thumbnail)
    {
        ent.Comp.Blending = false;
        ent.Comp.LastImage = image;
        _audio.PlayPvs(ent.Comp.SnapSound, ent);

        if (ent.Comp.PicturesLeft <= 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-camera-no-film"), user, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-camera-left", ("count", ent.Comp.PicturesLeft)), user, user);
        ent.Comp.PicturesLeft--;
        Dirty(ent);

        var description = ent.Comp.Description;
        if (!TryComp<ActorComponent>(user, out var actor))
        {
            Print(ent, user, image, thumbnail, null, description, null);
            return;
        }

        // can_customise: имя, описание и подпись; пустое поле — без изменений.
        _quickDialog.OpenDialog(actor.PlayerSession, Loc.GetString("imperial-photo-customise-title"),
            Loc.GetString("imperial-photo-customise-name"), Loc.GetString("imperial-photo-customise-desc"), Loc.GetString("imperial-photo-customise-caption"),
            (string name, LongString desc, LongString caption) =>
            {
                Print(ent, user, image, thumbnail,
                    string.IsNullOrWhiteSpace(name) ? null : name.Trim()[..Math.Min(32, name.Trim().Length)],
                    string.IsNullOrWhiteSpace(desc.String) ? description : $"{desc.String.Trim()} - {description}",
                    string.IsNullOrWhiteSpace(caption.String) ? null : caption.String.Trim());
            },
            () => Print(ent, user, image, thumbnail, null, description, null));
    }

    private void Print(Entity<ImperialPhotoCameraComponent> ent, EntityUid user, byte[] image, byte[] thumbnail, string? name, string description, string? caption)
    {
        if (Deleted(user))
            return;

        var photo = Spawn(ent.Comp.PhotoPrototype, Transform(user).Coordinates);
        var comp = EnsureComp<ImperialPhotoComponent>(photo);
        comp.Image = image;
        comp.Thumbnail = thumbnail;
        comp.Scribble = caption;
        Dirty(photo, comp);

        if (name != null)
            _metaData.SetEntityName(photo, name);
        _metaData.SetEntityDescription(photo, description);
        _hands.PickupOrDrop(user, photo);
    }

    /// <summary>item_interaction(): новая плёнка вставляется только в пустую камеру.</summary>
    private void OnInteractUsing(Entity<ImperialPhotoCameraComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<ImperialCameraFilmComponent>(args.Used))
            return;

        args.Handled = true;
        if (ent.Comp.PicturesLeft > 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-camera-not-empty"), ent, args.User);
            return;
        }

        QueueDel(args.Used);
        ent.Comp.PicturesLeft = ent.Comp.PicturesMax;
        Dirty(ent);
        _audio.PlayPvs(ent.Comp.ClickSound, ent);
    }

    private void OnExamined(Entity<ImperialPhotoCameraComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("imperial-camera-examine-left", ("count", ent.Comp.PicturesLeft)));
        args.PushMarkup(Loc.GetString("imperial-camera-examine-hint"));
        args.PushMarkup(Loc.GetString("imperial-camera-examine-size", ("width", Meters(ent.Comp.SizeX)), ("height", Meters(ent.Comp.SizeY))));
    }

    /// <summary>click_alt(): фокус; item_ctrl_click(): режим печати.</summary>
    private void OnVerbs(Entity<ImperialPhotoCameraComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryComp<ActorComponent>(args.User, out var actor))
            return;

        var user = args.User;
        var session = actor.PlayerSession;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-camera-verb-zoom"),
            Priority = 2,
            Act = () => _quickDialog.OpenDialog(session, Loc.GetString("imperial-camera-zoom-title"),
                Loc.GetString("imperial-camera-zoom-x"), Loc.GetString("imperial-camera-zoom-y"), (int x, int y) =>
                {
                    if (!_hands.IsHolding(user, ent))
                    {
                        _popup.PopupEntity(Loc.GetString("imperial-camera-hold"), user, user);
                        return;
                    }

                    ent.Comp.SizeX = Math.Clamp(x, 1, ImperialPhotoCameraComponent.SizeLimit);
                    ent.Comp.SizeY = Math.Clamp(y, 1, ImperialPhotoCameraComponent.SizeLimit);
                    Dirty(ent);
                    _audio.PlayPvs(ent.Comp.ClickSound, ent);
                    _popup.PopupEntity(Loc.GetString("imperial-camera-examine-size", ("width", Meters(ent.Comp.SizeX)), ("height", Meters(ent.Comp.SizeY))), user, user);
                }),
        });
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Monochrome ? "imperial-camera-verb-color" : "imperial-camera-verb-mono"),
            Act = () =>
            {
                ent.Comp.Monochrome = !ent.Comp.Monochrome;
                Dirty(ent);
                _audio.PlayPvs(ent.Comp.ClickSound, ent);
                _popup.PopupEntity(Loc.GetString(ent.Comp.Monochrome ? "imperial-camera-mono" : "imperial-camera-color"), user, user);
            },
        });
    }

    #endregion

    #region Фото

    /// <summary>attack_self() → show().</summary>
    private void OnPhotoUse(Entity<ImperialPhotoComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Show(ent, args.User);
    }

    /// <summary>examine(): вблизи показывает снимок, издалека — «подойдите ближе».</summary>
    private void OnPhotoExamined(Entity<ImperialPhotoComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
        {
            args.PushMarkup(Loc.GetString("imperial-photo-closer"));
            return;
        }

        Show(ent, args.Examiner);
    }

    private void Show(Entity<ImperialPhotoComponent> ent, EntityUid user)
    {
        if (ent.Comp.Image.Length == 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-photo-blank", ("photo", ent.Owner)), user, user);
            return;
        }

        if (!HasComp<ActorComponent>(user))
            return;

        _ui.OpenUi(ent.Owner, ImperialPhotoUiKey.Key, user);
        _ui.SetUiState(ent.Owner, ImperialPhotoUiKey.Key, new ImperialPhotoState(Name(ent), ent.Comp.Image, ent.Comp.Scribble));
    }

    /// <summary>attackby(ручка): надпись на обороте.</summary>
    private void OnPhotoInteractUsing(Entity<ImperialPhotoComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tag.HasTag(args.Used, WriteTag) || !TryComp<ActorComponent>(args.User, out var actor))
            return;

        args.Handled = true;
        _quickDialog.OpenDialog(actor.PlayerSession, Loc.GetString("imperial-photo-write-title"), Loc.GetString("imperial-photo-write-prompt"), (LongString text) =>
        {
            if (Deleted(ent) || string.IsNullOrWhiteSpace(text.String))
                return;

            var value = text.String.Trim();
            ent.Comp.Scribble = value[..Math.Min(128, value.Length)];
            Dirty(ent);
        });
    }

    /// <summary>verb/rename(): «фото - 'метка'».</summary>
    private void OnPhotoVerbs(Entity<ImperialPhotoComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryComp<ActorComponent>(args.User, out var actor))
            return;

        var session = actor.PlayerSession;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("imperial-photo-verb-rename"),
            Act = () => _quickDialog.OpenDialog(session, Loc.GetString("imperial-photo-verb-rename"), Loc.GetString("imperial-photo-rename-prompt"), (string text) =>
            {
                if (Deleted(ent))
                    return;
                var label = text.Trim();
                _metaData.SetEntityName(ent, string.IsNullOrEmpty(label) ? Loc.GetString("imperial-photo-name") : Loc.GetString("imperial-photo-name-label", ("label", label)));
            }),
        });
    }

    #endregion
}
