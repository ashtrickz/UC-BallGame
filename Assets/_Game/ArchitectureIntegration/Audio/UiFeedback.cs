using System;
using Slime.Audio;
using Slime.UI;

namespace Slime.Infrastructure.Audio
{
    /// <summary>Translates UI feedback into audio cues and native tap feedback.</summary>
    public sealed class UiFeedback : IUiFeedback
    {
        private readonly IAudioService _audio;
        private readonly IHaptics _haptics;

        public UiFeedback(IAudioService audio, IHaptics haptics)
        {
            _audio = audio;
            _haptics = haptics;
        }

        public void Play(UiFeedbackSound sound)
        {
            try
            {
                _audio.PlaySound(ToGameSound(sound));
            }
            catch (InvalidOperationException)
            {
                // Loading and retry buttons must remain usable before audio initialization.
            }

            _haptics.Play(Slime.Audio.HapticKind.Tap);
        }

        private static GameSound ToGameSound(UiFeedbackSound sound)
        {
            switch (sound)
            {
                case UiFeedbackSound.Click:
                    return GameSound.UiClick;

                case UiFeedbackSound.Confirm:
                    return GameSound.UiConfirm;

                case UiFeedbackSound.Back:
                    return GameSound.UiBack;

                case UiFeedbackSound.PanelOpen:
                    return GameSound.UiPanelOpen;

                case UiFeedbackSound.PanelClose:
                    return GameSound.UiPanelClose;

                case UiFeedbackSound.ToggleOn:
                    return GameSound.UiToggleOn;

                case UiFeedbackSound.ToggleOff:
                    return GameSound.UiToggleOff;

                case UiFeedbackSound.SliderTick:
                    return GameSound.UiSliderTick;

                case UiFeedbackSound.Coin:
                    return GameSound.UiCoin;

                default:
                    throw new ArgumentOutOfRangeException(nameof(sound));
            }
        }
    }
}
