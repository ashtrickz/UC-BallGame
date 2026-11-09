namespace Slime.Gameplay.Presentation
{
    /// <summary>
    /// The only way debug tooling can steer the attempt: an exact hold limit and whether the tutorial may start.
    /// Release builds register <see cref="NoGameplayOverrides"/>.
    /// </summary>
    public interface IGameplayOverrides
    {
        double? HoldLimit { get; }

        bool TutorialAllowed { get; }
    }

    /// <summary>Player-driven holds and the normal tutorial rule.</summary>
    public sealed class NoGameplayOverrides : IGameplayOverrides
    {
        public double? HoldLimit => null;

        public bool TutorialAllowed => true;
    }
}
