using Content.Shared.DoAfter;

namespace Content.Shared.Imperial.Medieval.Magic;


public abstract partial class SharedMedievalMagicSystem
{
    private void InitializeInstantSpells()
    {
        #region Spells Events

        SubscribeLocalEvent<MedievalSpawnInHandSpellEvent>(OnInstantSpellCast);
        SubscribeLocalEvent<MedievalInstantSpawnEvent>(OnInstantSpellCast);

        #endregion
    }

    private void OnInstantSpellCast(MedievalInstantSpellEvent args)
    {
        if (args.Handled) return;
        if (_handsSystem.TryGetEmptyHand(args.Performer, out _) == false)
        {
            _popupSystem.PopupClient(Loc.GetString("medieval-magic-free-hand-required"), args.Performer);
            return;
        }

        if (!PassesSpellPrerequisites(args.Action, args.Performer, Transform(args.Performer).Coordinates)) return;

        args.Handled = true;
        AddToStack(args.Performer, args.SpeechPoints);

        if (args.SpellCastDoAfter == null)
        {
            CastSpell(args);

            return;
        }

        var casterComponent = EnsureComp<MedievalSpellCasterComponent>(args.Performer);
        var speedModifier = args.SpellCastDoAfter.SpeedModifier;

        casterComponent.SpeedModifiers.Add(speedModifier);

        Dirty(args.Performer, casterComponent);

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.Performer,
            args.SpellCastDoAfter.Delay,
            GetSpellDoAfterEvent(args),
            args.Performer,
            args.Performer
        );

        args.SpellCastDoAfter.CopyToDoAfter(ref doAfterArgs);
        _doAfterSystem.TryStartDoAfter(doAfterArgs);

        _speedModifierSystem.RefreshMovementSpeedModifiers(args.Performer);
    }

    #region Helpers

    protected void CastSpell(MedievalInstantSpellEvent args)
    {
        switch (args)
        {
            case MedievalSpawnInHandSpellEvent spawnInHandSpellEvent:
                CastSpawnInHandSpell(new MedievalSpawnInHandSpellData()
                {
                    Performer = GetNetEntity(spawnInHandSpellEvent.Performer),
                    CastSpeedModifier = spawnInHandSpellEvent.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                    Action = GetNetEntity(spawnInHandSpellEvent.Action),
                    SpawnedEntityPrototype = spawnInHandSpellEvent.SpawnedEntityPrototype
                });
                break;
            case MedievalInstantSpawnEvent spawnEvent:
                CastInstantSpawnSpell(new MedievalInstantSpawnData()
                {
                    Performer = GetNetEntity(spawnEvent.Performer),
                    CastSpeedModifier = spawnEvent.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                    Action = GetNetEntity(spawnEvent.Action),
                    SpawnedEntityPrototype = spawnEvent.SpawnedEntityPrototype
                });
                break;
        }
    }

    protected MedievalSpellDoAfterEvent GetSpellDoAfterEvent(MedievalInstantSpellEvent args)
    {
        return args switch
        {
            MedievalSpawnInHandSpellEvent spawnInHandSpellEvent =>
                new MedievalSpawnInHandSpellDoAfterEvent()
                {
                    SpellData = new MedievalSpawnInHandSpellData()
                    {
                        CastSpeedModifier = spawnInHandSpellEvent.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                        Performer = GetNetEntity(spawnInHandSpellEvent.Performer),
                        Action = GetNetEntity(spawnInHandSpellEvent.Action),
                        SpawnedEntityPrototype = spawnInHandSpellEvent.SpawnedEntityPrototype
                    }
                },
            MedievalInstantSpawnEvent spawnEvent =>
                new MedievalInstantSpawnDoAfterEvent()
                {
                    SpellData = new MedievalInstantSpawnData()
                    {
                        CastSpeedModifier = spawnEvent.SpellCastDoAfter?.SpeedModifier ?? 1.0f,
                        Performer = GetNetEntity(spawnEvent.Performer),
                        Action = GetNetEntity(spawnEvent.Action),
                        SpawnedEntityPrototype = spawnEvent.SpawnedEntityPrototype
                    }
                },
            _ => throw new ArgumentOutOfRangeException("Cannot find upcast method")
        };
    }

    #endregion

    #region Server/Client implementation

    protected virtual void CastSpawnInHandSpell(MedievalSpawnInHandSpellData args)
    {
        RaiseLocalEvent(GetEntity(args.Action), new MedievalAfterCastSpellEvent()
        {
            Action = GetEntity(args.Action),
            Performer = GetEntity(args.Performer)
        });
    }

    protected virtual void CastInstantSpawnSpell(MedievalInstantSpawnData args)
    {
        RaiseLocalEvent(GetEntity(args.Action), new MedievalAfterCastSpellEvent()
        {
            Action = GetEntity(args.Action),
            Performer = GetEntity(args.Performer)
        });
    }

    #endregion
}
