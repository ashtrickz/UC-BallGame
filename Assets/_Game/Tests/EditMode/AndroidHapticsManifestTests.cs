using System;
using System.IO;
using System.Xml;
using Game.Editor.Android;
using NUnit.Framework;

namespace Game.Foundation.Tests
{
    public sealed class AndroidHapticsManifestTests
    {
        [Test]
        public void Generated_manifest_gets_vibration_permission_once_and_preserves_other_settings()
        {
            var directory = Path.Combine(Path.GetTempPath(), "BallGameManifestTests-" + Guid.NewGuid().ToString("N"));
            var manifestPath = Path.Combine(directory, "src", "main", "AndroidManifest.xml");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
                File.WriteAllText(manifestPath,
                    "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"com.ballgame\">" +
                    "<uses-permission android:name=\"android.permission.INTERNET\"/><application android:label=\"Ball Game\"/></manifest>");
                var processor = new AndroidHapticsManifest();
                processor.OnPostGenerateGradleAndroidProject(directory);
                var first = File.ReadAllText(manifestPath);
                processor.OnPostGenerateGradleAndroidProject(directory);
                Assert.AreEqual(first, File.ReadAllText(manifestPath), "Repeated builds must not rewrite or duplicate the permission.");
                var document = new XmlDocument();
                document.Load(manifestPath);
                var namespaces = new XmlNamespaceManager(document.NameTable);
                namespaces.AddNamespace("android", AndroidHapticsManifest.AndroidNamespace);
                Assert.AreEqual(1, document.SelectNodes("/manifest/uses-permission[@android:name='android.permission.VIBRATE']", namespaces).Count);
                Assert.AreEqual(1, document.SelectNodes("/manifest/uses-permission[@android:name='android.permission.INTERNET']", namespaces).Count);
                Assert.AreEqual("Ball Game", ((XmlElement)document.SelectSingleNode("/manifest/application")).GetAttribute("label", AndroidHapticsManifest.AndroidNamespace));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        [Test]
        public void Existing_permission_is_recognized_even_with_another_namespace_prefix()
        {
            var document = new XmlDocument();
            document.LoadXml("<manifest xmlns:a=\"http://schemas.android.com/apk/res/android\"><uses-permission a:name=\"android.permission.VIBRATE\"/></manifest>");
            Assert.IsFalse(AndroidHapticsManifest.EnsurePermission(document));
            Assert.AreEqual(1, document.GetElementsByTagName("uses-permission").Count);
        }
    }
}
