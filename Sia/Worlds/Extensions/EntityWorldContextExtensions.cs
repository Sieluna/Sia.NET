namespace Sia;

using System.Runtime.CompilerServices;

public static class EntityWorldContextExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Send<TEvent>(this Entity entity, in TEvent e)
        where TEvent : IEvent
        => World.Current.Send(entity, e);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Modify<TCommand>(this Entity entity, in TCommand command)
        where TCommand : ICommand
        => World.Current.Modify(entity, command);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Modify<TComponent, TCommand>(
        this Entity entity, ref TComponent component, in TCommand command)
        where TCommand : ICommand<TComponent>
        => World.Current.Modify(entity, ref component, command);
}