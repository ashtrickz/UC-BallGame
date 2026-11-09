using System;
using System.Collections.Generic;
using System.IO;
using Game.Audio;
using Game.Integration.Audio;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Installs the selected audio assets, preserving existing authored cue/mixer settings.</summary>
    public static class SelectedAudioLibraryInstaller
    {
        public const string AssetPath = "Assets/_Game/Resources/Audio/AudioLibrary.asset";
        private const string SpecPath = "Art/Audio/Selected/library.json";
        private const string ClipFolder = "Assets/_Game/Audio/Selected";

        [Serializable] private sealed class Specification { public Cue[] sounds; }
        [Serializable] private sealed class Cue
        {
            public string id, group;
            public string[] clips;
            public float volume;
            public float[] pitchRange;
            public bool loop;
            public float minInterval;
            public int maxVoices;
        }

        [MenuItem("Tools/Ball Game/Audio/Install Selected Library")]
        public static void Install()
        {
            var spec = JsonUtility.FromJson<Specification>(File.ReadAllText(SpecPath));
            foreach (var file in Directory.GetFiles(ClipFolder, "*.wav", SearchOption.AllDirectories))
            {
                var path = file.Replace('\\', '/');
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                GameAssetImportPolicy.ConfigureAudio(importer);
                importer.SaveAndReimport();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, AssetPath);
            }
            var sounds = new List<SoundDefinition>(library.Sounds);
            foreach (var cue in spec.sounds)
            {
                var id = (GameSound)Enum.Parse(typeof(GameSound), cue.id);
                if (sounds.Exists(sound => sound != null && sound.id == id)) continue;
                var clips = new List<AudioClip>();
                foreach (var path in cue.clips)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip == null) throw new FileNotFoundException("Selected audio clip is missing: " + path);
                    clips.Add(clip);
                }
                var definition = new SoundDefinition
                {
                    id = id,
                    clip = clips.Count > 0 ? clips[0] : null,
                    variants = clips.Count > 1 ? clips.GetRange(1, clips.Count - 1).ToArray() : Array.Empty<AudioClip>(),
                    volume = cue.volume,
                    pitchRange = new Vector2(cue.pitchRange[0], cue.pitchRange[1]),
                    group = (AudioGroup)Enum.Parse(typeof(AudioGroup), cue.group),
                    loop = cue.loop,
                    minInterval = cue.minInterval,
                    maxVoices = cue.maxVoices,
                };
                sounds.Add(definition);
            }
            library.Sounds = sounds.ToArray();
            var routes = new List<AudioMixerRoute>(library.MixerRoutes);
            foreach (var group in new[] { AudioGroup.Sfx, AudioGroup.UI, AudioGroup.Music })
                if (!routes.Exists(route => route != null && route.group == group))
                    routes.Add(new AudioMixerRoute { group = group });
            library.MixerRoutes = routes.ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);
            Selection.activeObject = library;
            EditorGUIUtility.PingObject(library);
            Debug.Log("Selected AudioLibrary installed: " + AssetPath);
        }
    }
}
