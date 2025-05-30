using Content.Server.ADT.Salvage.Components;
using Content.Server.Interaction;
using Content.Shared.Access.Systems;
using Content.Shared.ADT.Salvage.Components;
using Content.Shared.Chasm;
using Content.Shared.Inventory;
using Content.Shared.Lathe;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.ADT.Salvage.Systems;

public sealed class PreventChasmFallingSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PreventChasmFallingComponent, BeforeChasmFallingEvent>(OnBeforeFall);
        SubscribeLocalEvent<InventoryComponent, BeforeChasmFallingEvent>(Relay);
    }

    private void OnBeforeFall(EntityUid uid, PreventChasmFallingComponent comp, ref BeforeChasmFallingEvent args)
    {
        args.Cancelled = true;
        bool coordsValid = false;
        EntityCoordinates coords = Transform(args.Entity).Coordinates;

        while (!coordsValid)
        {
            var newCoords = new EntityCoordinates(Transform(args.Entity).ParentUid, coords.X + _random.NextFloat(-5f, 5f), coords.Y + _random.NextFloat(-5f, 5f));
            if (_interaction.InRangeUnobstructed(args.Entity, newCoords, -1f) && _lookup.GetEntitiesInRange<ChasmComponent>(newCoords, 1f).Count <= 0)
            {
                _transform.SetCoordinates(args.Entity, newCoords);
                _transform.AttachToGridOrMap(args.Entity, Transform(args.Entity));
                _audio.PlayPvs("/Audio/Items/Mining/fultext_launch.ogg", args.Entity);
                if (args.Entity != uid)
                    QueueDel(uid);
                coordsValid = true;
                break;
            }
        }
    }

    private void Relay(EntityUid uid, InventoryComponent comp, ref BeforeChasmFallingEvent args)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var containerManager))
            return;

        RelayEvent(uid, ref args);
    }

    private void RelayEvent(EntityUid uid, ref BeforeChasmFallingEvent ev)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var containerManager))
            return;

        foreach (var container in containerManager.Containers.Values)
        {
            if (ev.Cancelled)
                break;

            foreach (var entity in container.ContainedEntities)
            {
                RaiseLocalEvent(entity, ref ev);
                if (ev.Cancelled)
                    break;
                RelayEvent(entity, ref ev);
            }
        }
    }
}
