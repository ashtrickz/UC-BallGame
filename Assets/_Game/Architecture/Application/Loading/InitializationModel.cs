using System;

namespace Game.App.Loading
{
    public sealed class InitializationModel : IInitializationModel
    {
        private const string DefaultMessageKey = "loading.default";
        private const float CompletePercentage = 100f;

        public event Action Changed;
        public float InitPercentage { get; private set; } = 0;
        public string Message { get; private set; } = DefaultMessageKey;
        public bool HasError { get; private set; }
        public bool CanRetry { get; private set; }
        public bool IsComplete { get; private set; }

        public void SetStage(float percentage, string message)
        {
            Message = message;
            InitPercentage = percentage;
            HasError = CanRetry = IsComplete = false;
            Changed?.Invoke();
        }

        public void Fail(string message, bool canRetry)
        {
            Message = message;
            HasError = true;
            CanRetry = canRetry;
            Changed?.Invoke();
        }

        public void Complete()
        {
            InitPercentage = CompletePercentage;
            IsComplete = true;
            HasError = CanRetry = false;
            Changed?.Invoke();
        }
    }
}
