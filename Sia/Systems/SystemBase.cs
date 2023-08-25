namespace Sia;

public class SystemBase<TWorld> : ISystem
    where TWorld : World<EntityRef>
{
    public ISystemUnion? Children { get; init; }
    public ISystemUnion? Dependencies { get; init; }
    public IMatcher? Matcher { get; init; }
    public IEventUnion? Trigger { get; init; }
    public IEventUnion? Filter { get; init; }

    public virtual void Initialize(TWorld world, Scheduler scheduler) {}
    public virtual void Uninitialize(TWorld world, Scheduler scheduler) {}

    public virtual void BeforeExecute(TWorld world, Scheduler scheduler) {}
    public virtual void AfterExecute(TWorld world, Scheduler scheduler) {}
    public virtual void Execute(TWorld world, Scheduler scheduler, in EntityRef entity) {}

    public virtual bool OnTriggerEvent(TWorld world, Scheduler scheduler, in EntityRef entity, IEvent e) => true;
    public virtual bool OnFilterEvent(TWorld world, Scheduler scheduler, in EntityRef entity, IEvent e) => true;

    SystemHandle ISystem.Register(
        World<EntityRef> world, Scheduler scheduler, Scheduler.TaskGraphNode[]? dependedTasks)
        => Register((TWorld)world, scheduler, dependedTasks);

    public SystemHandle Register(
        TWorld world, Scheduler scheduler, Scheduler.TaskGraphNode[]? dependedTasks = null)
    {
        void AddDependedSystemTasks(ISystemUnion systemTypes, List<Scheduler.TaskGraphNode> result)
        {
            foreach (var systemType in systemTypes.ProxyTypes) {
                var sysData = SystemGlobalData.Get(systemType)
                    ?? throw new ArgumentException(
                        $"Failed to register system: invalid depended system type '{systemType}'");
                if (!sysData.RegisterEntries.TryGetValue((world, scheduler), out var taskNode)) {
                    throw new InvalidSystemDependencyException(
                        $"Failed to register system: Depended system type '{this}' is not registered.");
                }
                result.Add(taskNode);
            }
        }

        SystemHandle[] RegisterChildren(
            ITypeUnion children, Scheduler.TaskGraphNode taskNode)
        {
            var types = children.ProxyTypes;
            int count = types.Length;
            var handles = new SystemHandle[count];
            var childDependedTasks = new[] { taskNode };

            for (int i = 0; i != count; ++i) {
                var childSysData = SystemGlobalData.Get(types[i]);
                if (childSysData == null) {
                    for (int j = 0; j != i; ++j) {
                        handles[j].Dispose();
                    }
                    throw new InvalidSystemChildException(
                        $"Failed to register system: invalid child system type '{types[i]}'");
                }
                handles[i] = childSysData.Creator!().Register(world, scheduler, childDependedTasks);
            }

            return handles;
        }

        void DoRegisterSystem(SystemGlobalData sysData, Scheduler.TaskGraphNode task)
        {
            if (!sysData.RegisterEntries.TryAdd((world, scheduler), task)) {
                throw new SystemAlreadyRegisteredException(
                    "Failed to register system: system already registered in World and Scheduler pair");
            }
            Initialize(world, scheduler);
        }

        void DoUnregisterSystem(SystemGlobalData sysData, Scheduler.TaskGraphNode task)
        {
            if (!sysData.RegisterEntries.Remove((world, scheduler), out var removedTask)) {
                throw new ObjectDisposedException("System has been disposed");
            }
            if (removedTask != task) {
                throw new InvalidOperationException("Internal error: removed task is not the task to be disposed");
            }
            Uninitialize(world, scheduler);
        }

        var sysData = SystemGlobalData.Acquire(GetType());

        var dependedTasksResult = new List<Scheduler.TaskGraphNode>();
        if (dependedTasks != null) {
            dependedTasksResult.AddRange(dependedTasks);
        }

        if (Dependencies != null) {
            AddDependedSystemTasks(Dependencies, dependedTasksResult);
        }

        Scheduler.TaskGraphNode? task;
        SystemHandle[]? childrenHandles;

        var matcher = Matcher;
        if (matcher == null || matcher == Matchers.None) {
            task = scheduler.CreateTask(dependedTasksResult);
            task.UserData = this;

            DoRegisterSystem(sysData, task);
            childrenHandles = Children != null ? RegisterChildren(Children, task) : null;

            return new SystemHandle(
                this, task,
                handle => {
                    DoUnregisterSystem(sysData, task);

                    if (childrenHandles != null) {
                        for (int i = childrenHandles.Length - 1; i >= 0; --i) {
                            childrenHandles[i].Dispose();
                        }
                    }
                    scheduler.RemoveTask(handle.Task);
                });
        }

        Func<bool> taskFunc;
        Action disposeFunc;

        var trigger = Trigger;
        var filter = Filter;
        var dispatcher = world.Dispatcher;

        if (trigger == null && filter == null) {
            Group<EntityRef> group;

            if (matcher == Matchers.Any) {
                group = world;
                disposeFunc = () => {};
            }
            else {
                var groupCacheHandle = WorldGroupCache.Acquire(world, matcher);
                group = groupCacheHandle.Group;
                disposeFunc = groupCacheHandle.Dispose;
            }

            taskFunc = () => {
                var span = group.AsSpan();
                if (span.Length == 0) {
                    return false;
                }
                BeforeExecute(world, scheduler);
                foreach (var entity in span) {
                    Execute(world, scheduler, entity);
                }
                AfterExecute(world, scheduler);
                return false;
            };
        }
        else {
            var group = new Group<EntityRef>();

            taskFunc = () => {
                int count = group.Count;
                if (count == 0) {
                    return false;
                }
                BeforeExecute(world, scheduler);
                for (int i = 0; i < count; ++i) {
                    Execute(world, scheduler, group[i]);
                    count = group.Count;
                }
                AfterExecute(world, scheduler);
                group.Clear();
                return false;
            };

            if (trigger != null && filter != null) {
                var triggerTypes = new HashSet<Type>(trigger.ProxyTypes);
                var filterTypes = new HashSet<Type>(filter.ProxyTypes);

                foreach (var filterType in filterTypes) {
                    triggerTypes.Remove(filterType);
                }

                bool hasRemoveTrigger = triggerTypes.Contains(typeof(WorldEvents.Remove));

                if (matcher == Matchers.Any) {
                    bool OnEvent(in EntityRef target, IEvent e)
                    {
                        if (e == WorldEvents.Remove.Instance) {
                            if (hasRemoveTrigger) {
                                if (OnTriggerEvent(world, scheduler, target, e)) {
                                    group.Add(target);
                                }
                            }
                            else {
                                group.Remove(target);
                            }
                            return false;
                        }

                        var type = e.GetType();

                        if (triggerTypes.Contains(type)) {
                            if (OnTriggerEvent(world, scheduler, target, e)) {
                                group.Add(target);
                            }
                        }
                        else if (filterTypes.Contains(type)) {
                            if (OnFilterEvent(world, scheduler, target, e)) {
                                group.Remove(target);
                            }
                        }
                        return false;
                    }

                    dispatcher.Listen(OnEvent);
                    disposeFunc = () => dispatcher.Unlisten(OnEvent);
                }
                else {
                    var listeners = new Dictionary<EntityRef, Dispatcher.Listener>();
                    bool hasAddTrigger = triggerTypes.Contains(typeof(WorldEvents.Add));

                    bool OnEvent(in EntityRef target, IEvent e)
                    {
                        if (e == WorldEvents.Remove.Instance) {
                            listeners.Remove(target);
                            if (hasRemoveTrigger) {
                                if (OnTriggerEvent(world, scheduler, target, e)) {
                                    group.Add(target);
                                }
                            }
                            else {
                                group.Remove(target);
                            }
                            return true;
                        }

                        var type = e.GetType();

                        if (triggerTypes.Contains(type)) {
                            if (OnTriggerEvent(world, scheduler, target, e)) {
                                group.Add(target);
                            }
                        }
                        else if (filterTypes.Contains(type)) {
                            if (OnFilterEvent(world, scheduler, target, e)) {
                                group.Remove(target);
                            }
                        }
                        return false;
                    }

                    bool OnEntityAdded(in EntityRef target, IEvent e)
                    {
                        if (matcher.Match(target)) {
                            dispatcher.Listen(target, OnEvent);
                            listeners.Add(target, OnEvent);

                            if (hasAddTrigger) {
                                if (OnTriggerEvent(world, scheduler, target, e)) {
                                    group.Add(target);
                                }
                            }
                        }
                        return false;
                    }

                    dispatcher.Listen<WorldEvents.Add>(OnEntityAdded);

                    disposeFunc = () => {
                        dispatcher.Unlisten<WorldEvents.Add>(OnEntityAdded);
                        foreach (var (entity, listener) in listeners) {
                            dispatcher.Unlisten(entity, listener);
                        }
                    };
                }
            }
            else if (trigger != null) {
                var triggerTypes = new HashSet<Type>(trigger.ProxyTypes);
                bool hasRemoveTrigger = triggerTypes.Contains(typeof(WorldEvents.Remove));

                if (matcher == Matchers.Any) {
                    bool OnEvent(in EntityRef target, IEvent e)
                    {
                        if (e == WorldEvents.Remove.Instance) {
                            if (hasRemoveTrigger) {
                                if (OnTriggerEvent(world, scheduler, target, e)) {
                                    group.Add(target);
                                }
                            }
                            else {
                                group.Remove(target);
                            }
                        }
                        else if (triggerTypes.Contains(e.GetType())) {
                            if (OnTriggerEvent(world, scheduler, target, e)) {
                                group.Add(target);
                            }
                        }
                        return false;
                    }

                    dispatcher.Listen(OnEvent);
                    disposeFunc = () => dispatcher.Unlisten(OnEvent);
                }
                else {
                    var listeners = new Dictionary<EntityRef, Dispatcher.Listener>();
                    bool hasAddTrigger = triggerTypes.Contains(typeof(WorldEvents.Add));

                    bool OnEvent(in EntityRef target, IEvent e)
                    {
                        if (e == WorldEvents.Remove.Instance) {
                            listeners.Remove(target);
                            if (hasRemoveTrigger) {
                                if (OnTriggerEvent(world, scheduler, target, e)) {
                                    group.Add(target);
                                }
                            }
                            else {
                                group.Remove(target);
                            }
                            return true;
                        }
                        if (triggerTypes.Contains(e.GetType())) {
                            if (OnTriggerEvent(world, scheduler, target, e)) {
                                group.Add(target);
                            }
                        }
                        return false;
                    }

                    bool OnEntityAdded(in EntityRef target, IEvent e)
                    {
                        if (matcher.Match(target)) {
                            dispatcher.Listen(target, OnEvent);
                            listeners.Add(target, OnEvent);

                            if (hasAddTrigger) {
                                if (OnTriggerEvent(world, scheduler, target, e)) {
                                    group.Add(target);
                                }
                            }
                        }
                        return false;
                    }

                    dispatcher.Listen<WorldEvents.Add>(OnEntityAdded);

                    disposeFunc = () => {
                        dispatcher.Unlisten<WorldEvents.Add>(OnEntityAdded);
                        foreach (var (entity, listener) in listeners) {
                            dispatcher.Unlisten(entity, listener);
                        }
                    };
                }
            }
            else {
                throw new InvalidSystemAttributeException(
                    "Failed to register system: system must have non-null trigger when filter is specified");
            }
        }

        task = scheduler.CreateTask(taskFunc, dependedTasksResult);
        task.UserData = this;

        DoRegisterSystem(sysData, task);
        childrenHandles = Children != null ? RegisterChildren(Children, task) : null;

        SystemHandle? handle = null;

        void OnWorldDisposed(World<EntityRef> world) => handle!.Dispose();
        world.OnDisposed += OnWorldDisposed;

        handle = new SystemHandle(
            this, task,
            handle => {
                world.OnDisposed -= OnWorldDisposed;

                DoUnregisterSystem(sysData, task);
                disposeFunc();

                if (childrenHandles != null) {
                    for (int i = childrenHandles.Length - 1; i >= 0; --i) {
                        childrenHandles[i].Dispose();
                    }
                }
                scheduler.RemoveTask(handle.Task);
            });

        return handle;
    }
}

public class SystemBase : SystemBase<World>
{
}