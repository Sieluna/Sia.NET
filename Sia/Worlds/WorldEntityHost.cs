namespace Sia;

using System.Runtime.CompilerServices;

public sealed class WorldEntityHost<T, TStorage>
    : Internal.EntityHost<T, WrappedStorage<T, TStorage>>
    where T : struct
    where TStorage : class, IStorage<T>
{
    public World World { get; }

    public WorldEntityHost(World world, TStorage storage)
        : base(new(storage))
    {
        World = world;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override EntityRef Create()
    {
        var entity = base.Create();
        World.Dispatcher.Send(entity, WorldEvents.Add.Instance);
        return entity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override EntityRef Create(in T initial)
    {
        var entity = base.Create(initial);
        World.Dispatcher.Send(entity, WorldEvents.Add.Instance);
        return entity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Release(long pointer)
    {
        World.Dispatcher.Send(new(pointer, this), WorldEvents.Remove.Instance);
        base.Release(pointer);
    }
}