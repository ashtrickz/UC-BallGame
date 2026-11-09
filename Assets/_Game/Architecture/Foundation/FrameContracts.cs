namespace Game.Foundation
{
    /// <summary>Receives one explicit frame step from its owning loop.</summary>
    public interface IFrameTickable
    {
        void Tick(float deltaTime);
    }

    /// <summary>Tracks enabled dynamic clients without exposing a container to views.</summary>
    public interface ITickRegistry
    {
        void Register(IFrameTickable client);
        void Unregister(IFrameTickable client);
    }

    /// <summary>Accepts frame registration at a view's integration boundary.</summary>
    public interface ITickRegistryClient : IFrameTickable
    {
        void BindTicks(ITickRegistry registry);
    }

    /// <summary>Supplies unscaled elapsed time for project UI and intro clients.</summary>
    public interface IFrameTimeSource
    {
        float UnscaledDeltaTime
        {
            get;
        }
    }
}
