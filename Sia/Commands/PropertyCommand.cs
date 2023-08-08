namespace Sia;

public abstract class ImpurePropertyCommand<TCommand, TTarget, TValue>
    : SingleValuePooledEvent<TCommand, TValue>, ICommand<TTarget>
    where TCommand : ImpurePropertyCommand<TCommand, TTarget, TValue>, new()
    where TTarget : notnull
{
    public abstract void Execute(World<TTarget> world, in TTarget target);
}

public abstract class ImpurePropertyCommand<TCommand, TValue>
    : ImpurePropertyCommand<TCommand, EntityRef, TValue>, ICommand
    where TCommand : ImpurePropertyCommand<TCommand, TValue>, new()
{
}

public abstract class PropertyCommand<TCommand, TTarget, TValue>
    : SingleValuePooledEvent<TCommand, TValue>, ICommand<TTarget>
    where TCommand : PropertyCommand<TCommand, TTarget, TValue>, new()
    where TTarget : notnull
{
    public void Execute(World<TTarget> world, in TTarget target)
        => Execute(target);
    
    public abstract void Execute(in TTarget target);
}

public abstract class PropertyCommand<TCommand, TValue>
    : PropertyCommand<TCommand, EntityRef, TValue>, ICommand
    where TCommand : PropertyCommand<TCommand, EntityRef, TValue>, new()
{
}