using Slime.Foundation;
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Slime.Gameplay.Core.Levels;
using Slime.Gameplay.Presentation;
using Slime.Infrastructure.Audio;
using Slime.Composition;
using Slime.Infrastructure.Frame;
using Slime.Menu;

namespace Slime.Tests.EditMode
{
    /// <summary>Feature boundaries are enforced by assembly references, not by convention.</summary>
    public sealed class AssemblyBoundaryTests
    {
        private const string MenuFeature = "Slime.Menu";
        private const string GameFeature = "Slime.Gameplay.Presentation";
        private const string Composition = "Slime.Composition";
        private const string DebugTools = "Slime.Diagnostics";
        private const string UnityPrefix = "UnityEngine";

        [Test]
        public void Menu_and_game_features_do_not_reference_each_other()
        {
            CollectionAssert.DoesNotContain(References(typeof(MenuController)), GameFeature);
            CollectionAssert.DoesNotContain(References(typeof(GameplayFlow)), MenuFeature);
        }

        [Test]
        public void Services_and_features_do_not_reference_composition_or_debug_tools()
        {
            foreach (var type in new[] { typeof(UiFeedbackBinder), typeof(MenuController), typeof(GameplayFlow) })
            {
                var references = References(type);
                CollectionAssert.DoesNotContain(references, Composition, type.Assembly.GetName().Name);
                CollectionAssert.DoesNotContain(references, DebugTools, type.Assembly.GetName().Name);
            }

            Assert.AreEqual(Composition, typeof(ProjectScope).Assembly.GetName().Name);
        }

        [Test]
        public void Gameplay_core_does_not_reference_unity()
        {
            var unity = References(typeof(ILevelFactory)).Where(name => name.StartsWith(UnityPrefix, StringComparison.Ordinal));

            CollectionAssert.IsEmpty(unity);
        }

        private static string[] References(Type type)
        {
            return type.Assembly.GetReferencedAssemblies().Select(name => name.Name).ToArray();
        }
    }
}
