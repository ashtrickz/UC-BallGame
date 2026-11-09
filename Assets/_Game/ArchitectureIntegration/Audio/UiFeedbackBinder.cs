using Slime.Infrastructure.Frame;
using Slime.UI;
using UnityEngine;

namespace Slime.Infrastructure.Audio
{
    /// <summary>Binds authored and dynamically created buttons at integration boundaries.</summary>
    public sealed class UiFeedbackBinder
    {
        private readonly IUiFeedback _feedback;
        private readonly UiLifecycleBinder _lifecycle;

        public UiFeedbackBinder(IUiFeedback feedback, UiLifecycleBinder lifecycle)
        {
            _feedback = feedback;
            _lifecycle = lifecycle;
        }

        public void Bind(GameObject root)
        {
            foreach (var button in root.GetComponentsInChildren<FeedbackButton>(true))
                button.Bind(_feedback);
            _lifecycle.Bind(root);
        }
    }
}
