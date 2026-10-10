"""Static repository checks that need no Unity, .NET or network.
They cannot replace a Unity compile, but they catch the mistakes that otherwise only show up there:
missing/duplicate .meta GUIDs, unbalanced braces, wrong MonoBehaviour file names, engine code leaking
into the engine-independent Domain assembly, and broken JSON/Python.
"""
import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"
IGNORED = {".DS_Store"}


def assets():
    for path in sorted(ASSETS.rglob("*")):
        if path.name in IGNORED or path.name.startswith("."):
            continue
        yield path


def csharp_files():
    return sorted(list(ASSETS.rglob("*.cs")) + list((ROOT / "Tests").rglob("*.cs")))


def balance_problem(path):
    """Returns a description of the first unbalanced bracket/unterminated string, or None."""
    s = path.read_text(encoding="utf-8")
    i, n, line, stack = 0, len(s), 1, []
    closers = {")": "(", "]": "[", "}": "{"}
    while i < n:
        c = s[i]
        if c == "\n":
            line += 1
        if s.startswith("//", i):
            while i < n and s[i] != "\n":
                i += 1
            continue
        if s.startswith("/*", i):
            j = s.index("*/", i + 2)
            line += s[i:j].count("\n")
            i = j + 2
            continue
        if c == "@" and s[i + 1:i + 2] == '"':
            j = i + 2
            while True:
                if s[j] == '"' and s[j + 1:j + 2] == '"':
                    j += 2
                    continue
                if s[j] == '"':
                    break
                j += 1
            line += s[i:j].count("\n")
            i = j + 1
            continue
        if c == '"':
            j = i + 1
            while s[j] != '"':
                if s[j] == "\\":
                    j += 1
                if s[j] == "\n":
                    return "unterminated string at line %d" % line
                j += 1
            i = j + 1
            continue
        if c == "'":
            j = i + 1
            while s[j] != "'":
                if s[j] == "\\":
                    j += 1
                j += 1
            i = j + 1
            continue
        if c in "([{":
            stack.append((c, line))
        elif c in ")]}":
            if not stack or stack[-1][0] != closers[c]:
                return "mismatched %s at line %d" % (c, line)
            stack.pop()
        i += 1
    return "unclosed %s from line %d" % stack[-1] if stack else None


class RepositoryTests(unittest.TestCase):
    def test_every_asset_has_a_meta_file_and_no_meta_is_orphaned(self):
        missing, orphaned = [], []
        for path in assets():
            if path.suffix == ".meta":
                if not path.with_name(path.name[:-5]).exists():
                    orphaned.append(str(path.relative_to(ROOT)))
            elif not path.with_name(path.name + ".meta").exists():
                missing.append(str(path.relative_to(ROOT)))
        self.assertEqual(missing, [], "assets without .meta (Unity would invent new GUIDs)")
        self.assertEqual(orphaned, [], ".meta files without an asset")

    def test_guids_are_unique(self):
        seen = {}
        for meta in ASSETS.rglob("*.meta"):
            match = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(encoding="utf-8"), re.M)
            self.assertIsNotNone(match, "no valid guid in %s" % meta)
            self.assertNotIn(match.group(1), seen, "duplicate GUID %s: %s and %s" % (match.group(1), meta, seen.get(match.group(1))))
            seen[match.group(1)] = meta

    def test_csharp_brackets_balance(self):
        problems = {str(p.relative_to(ROOT)): balance_problem(p) for p in csharp_files()}
        self.assertEqual({k: v for k, v in problems.items() if v}, {})

    def test_script_file_names_match_unity_component_class_names(self):
        pattern = re.compile(r"\bclass\s+(\w+)\s*:\s*(?:MonoBehaviour|NetworkBehaviour|ScriptableObject)\b")
        wrong = []
        for path in ASSETS.rglob("*.cs"):
            for name in pattern.findall(path.read_text(encoding="utf-8")):
                if name != path.stem:
                    wrong.append("%s declares %s" % (path.relative_to(ROOT), name))
        self.assertEqual(wrong, [], "Unity cannot attach a component whose class name differs from its file name")

    def test_domain_assembly_stays_engine_independent(self):
        offenders = [str(p.relative_to(ROOT)) for p in (ASSETS / "Bidwarss" / "Domain").glob("*.cs")
                     if re.search(r"using\s+(UnityEngine|Unity\.)", p.read_text(encoding="utf-8"))]
        self.assertEqual(offenders, [])

    def test_json_files_parse(self):
        for path in list(ASSETS.rglob("*.asmdef")) + [ROOT / "Packages" / "bidwarss-dependencies.json"]:
            json.loads(path.read_text(encoding="utf-8"))

    def test_python_files_compile(self):
        for path in list((ROOT / "LeaderboardServer").glob("*.py")) + list((ROOT / "Tests").glob("*.py")):
            compile(path.read_text(encoding="utf-8"), str(path), "exec")  # syntax check only, writes nothing

    def test_documents_linked_from_readme_exist(self):
        readme = (ROOT / "README.md").read_text(encoding="utf-8")
        for needle in ("DedicatedServer/README.md", "LeaderboardServer/README.md", "Documentation/VALIDATION.md"):
            self.assertTrue(needle in readme, needle + " is not linked from README.md")
            self.assertTrue((ROOT / needle).exists(), needle + " does not exist")


if __name__ == "__main__":
    unittest.main()
