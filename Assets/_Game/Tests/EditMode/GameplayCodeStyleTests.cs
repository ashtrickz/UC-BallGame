using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Game.Foundation.Tests
{
    /// <summary>
    /// Keeps the project code on its style: layout, constants instead of literals, English only, documentation on
    /// types only, and logs under GAME_DEBUG. Runtime rules cover the runtime folders; language and documentation
    /// rules cover editor and test code too.
    /// </summary>
    public sealed class GameplayCodeStyleTests
    {
        private const string SourceRoot = "_Game";
        private const string Extension = "*.cs";
        private const string DebugDefine = "GAME_DEBUG";
        private const string DocPrefix = "///";
        private const int ReportLimit = 30;

        private static readonly string[] RuntimeFolders =
        {
            "Gameplay/Core",
            "Gameplay/View",
            "Architecture",
            "ArchitectureIntegration",
        };

        private static readonly string[] AllFolders =
        {
            "Gameplay",
            "Architecture",
            "ArchitectureIntegration",
            "Editor",
            "Tests",
        };

        // Debug tooling moves under GAME_DEBUG with stage 11; its literals are not gameplay tuning.
        private static readonly string[] LiteralWhitelist = { "FrameRecorder.cs" };

        // The debug log facade is the one place allowed to call Debug.Log: its methods are [Conditional].
        private static readonly string[] LogWhitelist = { "GameLog.cs" };

        private static readonly Regex Cyrillic = new Regex(@"[\u0400-\u04FF]");
        private static readonly Regex TypeDeclaration = new Regex(@"\b(class|interface|struct|enum|record)\s+\w+");
        private static readonly Regex ControlHeader = new Regex(@"^(else\s+if|if|while|foreach|for|using|lock)\s*\(");
        private static readonly Regex ElseStatement = new Regex(@"^else\s+[^{\s]");
        private static readonly Regex Jump = new Regex(@"^(return|continue|break|throw)\b.*;$");
        private static readonly Regex GuardHeader = new Regex(@"^(else\s+if|if|else|while|foreach|for)\b");
        private static readonly Regex NumericLiteral = new Regex(@"(?<![\w.])(0x[0-9A-Fa-f]+|\d+(\.\d+)?([eE]-?\d+)?)[fFdDmMuUlL]*(?![\w.])");
        private static readonly Regex LocalizationKey = new Regex("\"[a-z_]+\\.[a-z_.]+\"");
        private static readonly Regex StringLiteral = new Regex("\\$?@?\"(?:[^\"\\\\]|\\\\.)*\"|'(?:[^'\\\\]|\\\\.)'");
        private static readonly Regex InfoLog = new Regex(@"\bDebug\.(Log|LogWarning|LogFormat)\s*\(");
        private static readonly Regex DataEntry = new Regex(@"^\w+\s*=\s*[\w.|\s]+,?$");
        private static readonly HashSet<string> TrivialNumbers = new HashSet<string> { "0", "1", "0.0", "1.0" };

        [Test]
        public void Gameplay_sources_have_no_cyrillic()
        {
            var offenders = Lines(AllFolders)
                .Where(line => Cyrillic.IsMatch(line.Text))
                .Select(line => line.Location);

            AssertNone(offenders);
        }

        [Test]
        public void Documentation_comments_are_only_on_types()
        {
            var offenders = new List<string>();
            foreach (var file in Files(AllFolders))
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].Trim().StartsWith(DocPrefix))
                        continue;

                    var next = i;
                    while (next < lines.Length && IsDocOrAttribute(lines[next]))
                        next++;

                    if (next < lines.Length && !TypeDeclaration.IsMatch(lines[next]))
                        offenders.Add(Location(file, i));

                    i = next;
                }
            }

            AssertNone(offenders);
        }

        [Test]
        public void Runtime_code_has_one_statement_per_line_and_no_one_line_guards()
        {
            var offenders = Lines(RuntimeFolders)
                .Where(line => IsOneLineGuard(line.Code) || CountStatements(line.Code) > 1)
                .Select(line => line.Location);

            AssertNone(offenders);
        }

        [Test]
        public void Guard_jumps_are_followed_by_a_blank_line()
        {
            var offenders = new List<string>();
            foreach (var file in Files(RuntimeFolders))
            {
                var lines = File.ReadAllLines(file).Select(line => line.Trim()).ToArray();
                for (var i = 1; i < lines.Length - 1; i++)
                {
                    var guard = GuardHeader.IsMatch(lines[i - 1]) && !lines[i - 1].EndsWith("{") && !lines[i - 1].EndsWith(";");
                    if (!guard || !Jump.IsMatch(lines[i]))
                        continue;

                    var next = lines[i + 1];
                    var closes = next.Length == 0 || next.StartsWith("}") || next.StartsWith("else") || next.StartsWith("#")
                        || next.StartsWith("case ") || next.StartsWith("default:");
                    if (!closes)
                        offenders.Add(Location(file, i));
                }
            }

            AssertNone(offenders);
        }

        [Test]
        public void Runtime_code_names_its_numbers_and_localization_keys()
        {
            var offenders = Lines(RuntimeFolders)
                .Where(line => !LiteralWhitelist.Contains(Path.GetFileName(line.File)))
                .Where(line => !IsDeclarationOfConstant(line.Code) && !line.Code.StartsWith("["))
                .Where(line => !DataEntry.IsMatch(line.Code))
                .Where(line => HasMagicNumber(line.Code) || LocalizationKey.IsMatch(line.Code))
                .Select(line => line.Location + "  " + line.Code);

            AssertNone(offenders);
        }

        [Test]
        public void Information_logs_compile_only_with_the_debug_define()
        {
            var offenders = new List<string>();
            foreach (var file in Files(RuntimeFolders).Where(file => !LogWhitelist.Contains(Path.GetFileName(file))))
            {
                var lines = File.ReadAllLines(file);
                var debugDepth = new Stack<bool>();
                for (var i = 0; i < lines.Length; i++)
                {
                    var trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("#if"))
                        debugDepth.Push(trimmed.Contains(DebugDefine));
                    else if (trimmed.StartsWith("#endif") && debugDepth.Count > 0)
                        debugDepth.Pop();
                    else if (InfoLog.IsMatch(trimmed) && !debugDepth.Contains(true))
                        offenders.Add(Location(file, i));
                }
            }

            AssertNone(offenders);
        }

        private sealed class SourceLine
        {
            public string File;
            public int Index;
            public string Text;
            public string Code;

            public string Location => GameplayCodeStyleTests.Location(File, Index);
        }

        private static IEnumerable<string> Files(IEnumerable<string> folders)
        {
            var root = Path.Combine(UnityEngine.Application.dataPath, SourceRoot);
            foreach (var folder in folders)
            {
                var path = Path.Combine(root, folder);
                if (!Directory.Exists(path))
                    continue;

                foreach (var file in Directory.GetFiles(path, Extension, SearchOption.AllDirectories))
                {
                    var normalized = file.Replace('\\', '/');
                    var runtimeOnly = folders == (IEnumerable<string>)RuntimeFolders;
                    if (runtimeOnly && (normalized.Contains("/Editor/") || normalized.Contains("/Tests/")))
                        continue;

                    yield return file;
                }
            }
        }

        private static IEnumerable<SourceLine> Lines(IEnumerable<string> folders)
        {
            foreach (var file in Files(folders))
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("#") || trimmed.Length == 0)
                        continue;

                    yield return new SourceLine { File = file, Index = i, Text = lines[i], Code = StripComment(trimmed) };
                }
            }
        }

        private static string StripComment(string line)
        {
            var code = StringLiteral.Replace(line, match => new string('s', match.Length));
            var comment = code.IndexOf("//", System.StringComparison.Ordinal);

            return comment >= 0
                ? line.Substring(0, comment).TrimEnd()
                : line;
        }

        private static bool IsDocOrAttribute(string line)
        {
            var trimmed = line.Trim();

            return trimmed.StartsWith(DocPrefix) || trimmed.StartsWith("[") || trimmed.StartsWith("#");
        }

        private static bool IsOneLineGuard(string code)
        {
            if (ElseStatement.IsMatch(code) && code.EndsWith(";"))
                return true;

            var header = ControlHeader.Match(code);
            if (!header.Success || code.StartsWith("using"))
                return false;

            var close = MatchingParenthesis(code, code.IndexOf('(', header.Index));
            if (close < 0)
                return false;

            var rest = code.Substring(close + 1).Trim();

            return rest.Length > 0 && !rest.StartsWith("{") && rest.EndsWith(";");
        }

        private static int CountStatements(string code)
        {
            var masked = StringLiteral.Replace(code, match => new string('s', match.Length));
            var depth = 0;
            var braces = 0;
            var count = 0;
            foreach (var c in masked)
            {
                if (c == '(')
                    depth++;
                else if (c == ')')
                    depth--;
                else if (c == '{')
                    braces++;
                else if (c == '}')
                    braces--;
                else if (c == ';' && depth == 0 && braces == 0)
                    count++;
            }

            return count;
        }

        private static int MatchingParenthesis(string code, int open)
        {
            var depth = 0;
            for (var i = open; i < code.Length; i++)
            {
                if (code[i] == '(')
                    depth++;
                else if (code[i] == ')' && --depth == 0)
                    return i;
            }

            return -1;
        }

        private static bool IsDeclarationOfConstant(string code)
        {
            return code.Contains(" const ") || code.StartsWith("const ") || code.Contains("static readonly");
        }

        private static bool HasMagicNumber(string code)
        {
            var masked = StringLiteral.Replace(code, string.Empty);
            foreach (Match match in NumericLiteral.Matches(masked))
            {
                if (!TrivialNumbers.Contains(match.Groups[1].Value))
                    return true;
            }

            return false;
        }

        private static string Location(string file, int index)
        {
            var relative = file.Replace('\\', '/');
            var start = relative.IndexOf(SourceRoot + "/", System.StringComparison.Ordinal);

            return (start >= 0 ? relative.Substring(start) : relative) + ":" + (index + 1);
        }

        private static void AssertNone(IEnumerable<string> offenders)
        {
            var list = offenders.ToList();
            Assert.IsEmpty(list, list.Count + " violations:\n" + string.Join("\n", list.Take(ReportLimit)));
        }
    }
}
