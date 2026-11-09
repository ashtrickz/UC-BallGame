using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityEngine;

namespace Game.Foundation.Tests
{
    /// <summary>Prevents project runtime components from introducing hidden Unity frame loops.</summary>
    public sealed class RuntimeFrameArchitectureTests
    {
        private const string ProjectAssemblyPrefix = "Game.";
        private const string SlimeLabAssembly = "Game.SlimeLab";
        private const string CameraType = "Game.Gameplay.View.CameraRig";
        private const string UpdateMethod = "Update";
        private const string LateUpdateMethod = "LateUpdate";
        private const string FixedUpdateMethod = "FixedUpdate";

        [Test]
        public void Runtime_components_only_declare_explicitly_allowed_frame_messages()
        {
            var runtime = new HashSet<string>(CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies)
                .Where(assembly => assembly.name.StartsWith(ProjectAssemblyPrefix, StringComparison.Ordinal))
                .Select(assembly => assembly.name));
            var violations = new List<string>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (!runtime.Contains(name) || name == SlimeLabAssembly)
                    continue;

                foreach (var type in assembly.GetTypes())
                {
                    if (!typeof(MonoBehaviour).IsAssignableFrom(type))
                        continue;

                    foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (method.Name != UpdateMethod && method.Name != LateUpdateMethod && method.Name != FixedUpdateMethod)
                            continue;

                        if (type.FullName == CameraType && method.Name == LateUpdateMethod)
                            continue;

                        violations.Add(type.FullName + "." + method.Name);
                    }
                }
            }

            Assert.IsNotEmpty(runtime, "The architecture check must inspect the project's player assemblies.");
            Assert.IsEmpty(violations, string.Join(Environment.NewLine, violations));
        }
    }
}
