
namespace Sia;

using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using static WorldHostUtils;

public sealed record WrappedWorldEntityHost<TEntity, TEntityHost>(World World)
    : IEntityHost<TEntity>, IReactiveEntityHost
    where TEntity : IHList
    where TEntityHost : IEntityHost<TEntity>, new()
{
    public event Action<IEntityHost>? OnDisposed {
        add => _host.OnDisposed += value;
        remove => _host.OnDisposed -= value;
    }

    public event EntityHandler? OnEntityCreated;
    public event EntityHandler? OnEntityReleased;

    public Type InnerEntityType => _host.InnerEntityType;
    public EntityDescriptor Descriptor => _host.Descriptor;

    public TEntityHost InnerHost => _host;

    public int Capacity => _host.Capacity;
    public int Count => _host.Count;
    public ReadOnlySpan<StorageSlot> AllocatedSlots => _host.AllocatedSlots;

    private readonly TEntityHost _host = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityRef Create()
    {
        var entity = _host.Create();
        var dispatcher = World.Dispatcher;

        World.Count++;
        OnEntityCreated?.Invoke(entity);
        dispatcher.Send(entity, WorldEvents.Add.Instance);

        ref var data = ref _host.GetRef(entity.Slot);
        data.HandleHead(new EntityHeadAddEventSender(entity, dispatcher));
        data.HandleTail(new EntityTailAddEventSender(entity, dispatcher));

        return entity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityRef Create(in TEntity initial)
    {
        var entity = _host.Create(initial);
        var dispatcher = World.Dispatcher;

        World.Count++;
        OnEntityCreated?.Invoke(entity);
        dispatcher.Send(entity, WorldEvents.Add.Instance);

        ref var data = ref _host.GetRef(entity.Slot);
        data.HandleHead(new EntityHeadAddEventSender(entity, dispatcher));
        data.HandleTail(new EntityTailAddEventSender(entity, dispatcher));

        return entity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release(in StorageSlot slot)
    {
        var entity = new EntityRef(slot, this);
        var dispatcher = World.Dispatcher;

        ref var data = ref _host.GetRef(entity.Slot);
        data.HandleHead(new EntityHeadRemoveEventSender(entity, dispatcher));
        data.HandleTail(new EntityTailRemoveEventSender(entity, dispatcher));

        dispatcher.Send(entity, WorldEvents.Remove.Instance);
        dispatcher.UnlistenAll(entity);

        World.Count--;
        OnEntityReleased?.Invoke(entity);
        _host.Release(slot);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityRef MoveIn(in HList<Identity, TEntity> data)
    {
        var entity = _host.MoveIn(data);
        OnEntityCreated?.Invoke(entity);
        return entity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MoveOut(in StorageSlot slot)
    {
        OnEntityReleased?.Invoke(new(slot, this));
        _host.MoveOut(slot);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityRef Add<TComponent>(in StorageSlot slot, in TComponent initial)
    {
        var e = _host.Add(slot, initial);
        World.Dispatcher.Send(e, WorldEvents.Add<TComponent>.Instance);
        return e;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityRef AddMany<TList>(in StorageSlot slot, in TList list)
        where TList : IHList
    {
        var e = _host.AddMany(slot, list);
        list.HandleHead(new EntityHeadAddEventSender(e, World.Dispatcher));
        list.HandleTail(new EntityTailAddEventSender(e, World.Dispatcher));
        return e;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityRef Remove<TComponent>(in StorageSlot slot)
    {
        var e = _host.Remove<TComponent>(slot);
        World.Dispatcher.Send(e, WorldEvents.Remove<TComponent>.Instance);
        return e;
    }

    public bool IsValid(in StorageSlot slot)
        => _host.IsValid(slot);

    public unsafe ref byte GetByteRef(in StorageSlot slot)
        => ref _host.GetByteRef(slot);

    public unsafe ref byte UnsafeGetByteRef(in StorageSlot slot)
        => ref _host.UnsafeGetByteRef(slot);

    public ref HList<Identity, TEntity> GetRef(in StorageSlot slot)
        => ref _host.GetRef(slot);

    public ref HList<Identity, TEntity> UnsafeGetRef(in StorageSlot slot)
        => ref _host.UnsafeGetRef(slot);

    public void GetHList<THandler>(in StorageSlot slot, in THandler handler)
        where THandler : IRefGenericHandler<IHList>
        => _host.GetHList(slot, handler);
    
    public object Box(in StorageSlot slot)
        => _host.Box(slot);

    public IEnumerator<EntityRef> GetEnumerator() => _host.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _host.GetEnumerator();

    public void Dispose() => _host.Dispose();
}