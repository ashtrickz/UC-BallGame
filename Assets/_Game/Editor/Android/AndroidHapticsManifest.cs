using System;
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEditor.Build;

namespace Game.Editor.Android
{
    // Native JNI calls do not rely on Unity detecting an otherwise unused Handheld.Vibrate method.
    public sealed class AndroidHapticsManifest : IPostGenerateGradleAndroidProject
    {
        public const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        public const string VibratePermission = "android.permission.VIBRATE";
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
                throw new BuildFailedException("Haptics manifest was not found: " + manifestPath);
            var document = new XmlDocument { XmlResolver = null };
            document.Load(manifestPath);
            if (EnsurePermission(document)) document.Save(manifestPath);
        }

        public static bool EnsurePermission(XmlDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var manifest = document.DocumentElement;
            if (manifest == null || manifest.Name != "manifest")
                throw new InvalidDataException("Expected an Android manifest root.");
            foreach (XmlNode node in manifest.ChildNodes)
                if (node is XmlElement permission && permission.Name == "uses-permission" &&
                    permission.GetAttribute("name", AndroidNamespace) == VibratePermission)
                    return false;
            var entry = document.CreateElement("uses-permission");
            var name = document.CreateAttribute("android", "name", AndroidNamespace);
            name.Value = VibratePermission;
            entry.Attributes.Append(name);
            manifest.PrependChild(entry);
            return true;
        }
    }
}
