using Content.Server.MedievalPasport.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Content.Server.Polymorph.Systems;
using Content.Shared.Alert;
using Content.Shared.Popups;
using Content.Shared.Item;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Factions;

namespace Content.Server.MedievalPasport
{
    public sealed partial class MedievalPasportSystem : EntitySystem
    {
        [Dependency] internal readonly IEntityManager _entityManager = default!;
        [Dependency] internal readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly SharedAudioSystem Audio = default!;
        [Dependency] private readonly MetaDataSystem _metaData = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;


        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MedievalPasportPersonComponent, ComponentStartup>(OnStart);
            SubscribeLocalEvent<MedievalPasportComponent, ExaminedEvent>(OnExamine);

        }

        private void OnExamine(EntityUid uid, MedievalPasportComponent component, ExaminedEvent args)
        {
            args.PushMarkup("Имя: [color=white]" + component.PersonName + "[/color]");
            if (component.PersonGender == "мужской")
                args.PushMarkup("Пол: [color=cyan]" + component.PersonGender + "[/color]");
            else
                args.PushMarkup("Пол: [color=pink]" + component.PersonGender + "[/color]");
            args.PushMarkup("Возраст: [color=white]" + component.PersonAge + "[/color]");
            args.PushMarkup("Должность: " + component.PersonJob);
            args.PushMarkup($"Раса: {Loc.GetString($"species-colored-{component.PersonRace}")}");
        }

        public void OnStart(EntityUid uid, MedievalPasportPersonComponent comp, ComponentStartup args)
        {
            var ev = new StartupFactionDataEvent(comp.PersonJob, comp.JobPrefix);
            RaiseLocalEvent(uid, ev);

            if (comp.Pasport == "no") return;
            var xform = Transform(comp.Owner);
            var coords = xform.Coordinates;
            comp.PasportEntity = Spawn(comp.Pasport, coords);
            _hands.TryPickupAnyHand(uid, comp.PasportEntity.Value);
            var pasport = EnsureComp<MedievalPasportComponent>(comp.PasportEntity.Value);
            if (TryComp<MetaDataComponent>(uid, out var metadata))
                pasport.PersonName = metadata.EntityName;
            if (TryComp<HumanoidAppearanceComponent>(uid, out var appearance))
            {
                if (appearance.Sex == Sex.Male)
                    pasport.PersonGender = "мужской";
                else
                    pasport.PersonGender = "женский";
                pasport.PersonAge = appearance.Age.ToString();
                pasport.PersonRace = appearance.Species.ToString();
            }
            pasport.PersonJob = comp.PersonJob;
            _metaData.SetEntityName(comp.PasportEntity.Value, "волшебное удостоверение " + pasport.PersonName);
        }

    }

}
