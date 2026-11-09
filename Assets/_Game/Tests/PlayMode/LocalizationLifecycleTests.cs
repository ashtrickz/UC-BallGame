using Slime.Foundation;
using System;
using System.Collections;
using NUnit.Framework;
using Slime.Infrastructure.Localization;
using Slime.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Slime.Tests.PlayMode
{
    public sealed class LocalizationLifecycleTests
    {
        [UnityTest]
        public IEnumerator Disabled_labels_unsubscribe_and_refresh_after_enabling()
        {
            var service = new Localization();
            var builder = new ContainerBuilder();
            builder.RegisterInstance<ILocalizationService>(service);
            using var container = builder.Build();
            var root = new GameObject("Localization test", typeof(RectTransform), typeof(TextMeshProUGUI));
            try
            {
                var label = root.GetComponent<TMP_Text>();
                var localized = root.AddComponent<LocalizedText>();
                localized.SetKey("title");
                container.Inject(localized);
                yield return null;
                Assert.AreEqual("Original", label.text);
                root.SetActive(false);
                service.Update("Updated");
                Assert.AreEqual("Original", label.text);
                root.SetActive(true);
                Assert.AreEqual("Updated", label.text);
                UnityEngine.Object.Destroy(root);
                yield return null;
                Assert.DoesNotThrow(() => service.Update("After destruction"));
            }
            finally { if (root != null) UnityEngine.Object.Destroy(root); }
        }

        private sealed class Localization : ILocalizationService
        {
            public event Action Changed;
            public string CurrentLanguage => "en";
            private string _value = "Original";
            public string Get(string key) => _value;
            public void Update(string value) { _value = value; Changed?.Invoke(); }
        }
    }
}
