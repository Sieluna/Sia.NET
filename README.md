# Sia.NET

Elegant ECS framework for .NET

## Example

```C#
namespace Sia.Example;

using System.Numerics;

public static class Program
{
    public class GameWorld : World
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }

        public Scheduler Scheduler { get; } = new();

        public void Update(float deltaTime)
        {
            DeltaTime = deltaTime;
            Time += deltaTime;
            Scheduler.Tick();
        }
    }

    public struct Transform
    {
        public Vector2 Position;
        public float Angle;

        public record SetPosition
            : SingleValuePooledCommand<SetPosition, Vector2>
            , IExecutable
        {
            public void Execute(EntityRef target)
            {
                ref var trans = ref target.Get<Transform>();
                trans.Position = Value;
            }
        }

        public record SetAngle
            : SingleValuePooledCommand<SetAngle, float>
            , IExecutable
        {
            public void Execute(EntityRef target)
            {
                ref var trans = ref target.Get<Transform>();
                trans.Angle = Value;
            }
        }
    }

    public struct Health
    {
        public float Value = 100;
        public float Debuff = 0;

        public Health() {}
        public Health(float value)
        {
            Value = value;
        }

        public record Damage
            : SingleValuePooledCommand<Damage, float>
            , IExecutable
        {
            public void Execute(EntityRef target)
            {
                ref var health = ref target.Get<Health>();
                health.Value -= Value;
            }
        }

        public record SetDebuff
            : SingleValuePooledCommand<SetDebuff, float>
            , IExecutable
        {
            public void Execute(EntityRef target)
            {
                ref var health = ref target.Get<Health>();
                health.Debuff = Value;
            }
        }
    }

    public class HealthUpdateSystem : SystemBase<GameWorld>
    {
        public HealthUpdateSystem()
        {
            Matcher = new TypeUnion<Health>();
        }

        public override void Execute(GameWorld world, Scheduler scheduler, EntityRef entity)
        {
            ref var health = ref entity.Get<Health>();
            if (health.Debuff != 0) {
                world.Modify(entity, Health.Damage.Create(health.Debuff * world.DeltaTime));
                Console.WriteLine($"Damage: HP {entity.Get<Health>().Value}");
            }
        }
    }

    public class DeathSystem : SystemBase<GameWorld>
    {
        public DeathSystem()
        {
            Matcher = new TypeUnion<Health>();
            Dependencies = new SystemUnion<HealthUpdateSystem>();
        }

        public override void Execute(GameWorld world, Scheduler scheduler, EntityRef entity)
        {
            if (entity.Get<Health>().Value <= 0) {
                world.Remove(entity);
                Console.WriteLine("Dead!");
            }
        }
    }

    public class HealthSystems : SystemBase<GameWorld>
    {
        public HealthSystems()
        {
            Children = new SystemUnion<HealthUpdateSystem, DeathSystem>();
        }
    }

    public class LocationDamageSystem : SystemBase<GameWorld>
    {
        public LocationDamageSystem()
        {
            Matcher = new TypeUnion<Transform, Health>();
            Trigger = new CommandUnion<WorldCommands.Add, Transform.SetPosition>();
        }

        public override void Execute(GameWorld world, Scheduler scheduler, EntityRef entity)
        {
            var pos = entity.Get<Transform>().Position;
            if (pos.X == 1 && pos.Y == 1) {
                world.Modify(entity, Health.Damage.Create(10));
                Console.WriteLine($"Damage: HP {entity.Get<Health>().Value}");
            }
            if (pos.X == 1 && pos.Y == 2) {
                world.Modify(entity, Health.SetDebuff.Create(100));
                Console.WriteLine("Debuff!");
            }
        }
    }

    public class GameplaySystems : SystemBase<GameWorld>
    {
        public GameplaySystems()
        {
            Children = new SystemUnion<LocationDamageSystem>();
            Dependencies = new SystemUnion<HealthSystems>();
        }
    }

    public struct Player
    {
        public Transform Transform;
        public Health Health;
    }

    public static void Main()
    {
        var world = new GameWorld();

        var healthSystemsHandle =
            new HealthSystems().Register(world, world.Scheduler);
        var gameplaySystemsHandle =
            new GameplaySystems().Register(world, world.Scheduler);

        var player = new Player() {
            Transform = new() {
                Position = new(1, 1)
            },
            Health = new(200)
        };
        var playerRef = EntityRef.Create(ref player);

        world.Add(playerRef);
        world.Update(0.5f);

        world.Modify(playerRef, Transform.SetPosition.Create(new(1, 2)));
        world.Update(0.5f);

        world.Scheduler.CreateTask(() => {
            Console.WriteLine("Callback invoked after health and gameplay systems");
            return true; // remove task
        }, new[] {healthSystemsHandle.Task, gameplaySystemsHandle.Task});
    
        world.Modify(playerRef, Transform.SetPosition.Create(new(1, 3)));
        world.Update(0.5f);
        world.Update(0.5f);
        world.Update(0.5f);
        world.Update(0.5f); // player dead

        gameplaySystemsHandle.Dispose();
        healthSystemsHandle.Dispose();
    }
}
```