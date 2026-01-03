#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import platform
import subprocess
from pathlib import Path
from typing import List, Dict, Optional, Tuple

# ============================
# Konfiguration
# ============================

REPO_PATH = Path(r".\\")

# prePrompt: wird am Anfang eingefügt
PREPROMPT_CANDIDATES = [
    REPO_PATH / "prePrompt.md",
]

CONFIG_CANDIDATES = [
    Path(r"solar-moduls.json"),
]

OUTPUT_PATH = Path(r".\\Prompt.md")
# OUTPUT_PATH = Path(r"D:\Projekte\Create Prompt\spi2udp_Prompt.md")

REPO_TREE_IGNORE = {
    ".git",
    ".vs",
    "bin",
    "obj",
    "packages",
    "node_modules",
    "__pycache__",
    "git.log",
    "CreatePrompt.py",
    "prePrompt.md",
    "Prompt.md",
    "README.md",
    "LIZENCE",
    # "ConfigService.cs",
}

DATA_TREE_IGNORE = {"__pycache__"}

REPO_TREE_MAX_DEPTH = 100
REPO_TREE_MAX_ENTRIES_PER_DIR = 100
DATA_TREE_MAX_DEPTH = 100
TREE_MAX_ENTRIES_PER_DIR = 100

SOURCE_EXTS = {
    ".cs",
    ".xaml",
    ".xml",
    ".json",
    ".config",
    ".csproj",
    ".sln",
    ".props",
    ".targets",
    ".md",
}

# Dateien/Pattern ausschließen (case-insensitive, Wildcards erlaubt)
EXCLUDE_PATTERNS = {
    "*.bak",
    "TODO.md",
    "README.md",
    "CHANGELOG.md",
    # Beispiele:
    # "*.log",
    # "logs/*.log",
    # "*/bin/*",
}

# Optional Limits (None = kein Limit)
MAX_FILE_BYTES: Optional[int] = None
MAX_TOTAL_CHARS: Optional[int] = None

LOG_HEAD_LINES = 20
LOG_TAIL_LINES = 20

NL = "\n"

# ============================
# Utilities
# ============================


def ensure_parent_dir(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)


def run_cmd(
    cmd: List[str], cwd: Optional[Path] = None, timeout: int = 10
) -> Tuple[int, str, str]:
    try:
        proc = subprocess.run(
            cmd,
            cwd=str(cwd) if cwd else None,
            capture_output=True,
            text=True,
            timeout=timeout,
            encoding="utf-8",
            errors="replace",
        )
        return proc.returncode, proc.stdout.strip(), proc.stderr.strip()
    except Exception as ex:
        return 1, "", str(ex)


def get_git_info(repo: Path) -> Dict[str, str]:
    info: Dict[str, str] = {}
    if not repo.exists():
        info["error"] = "Repo-Pfad nicht gefunden: {0}".format(repo)
        return info
    rc, out, err = run_cmd(
        ["git", "-C", str(repo), "rev-parse", "--abbrev-ref", "HEAD"]
    )
    info["branch"] = out if rc == 0 else "unbekannt ({0})".format(err)
    rc, out, err = run_cmd(
        [
            "git",
            "-C",
            str(repo),
            "log",
            "-1",
            "--pretty=%H | %an | %ad | %s",
            "--date=iso",
        ]
    )
    info["last_commit"] = out if rc == 0 else "unbekannt ({0})".format(err)
    rc, out, err = run_cmd(["git", "-C", str(repo), "remote", "-v"])
    info["remotes"] = out if rc == 0 else "unbekannt ({0})".format(err)
    return info


def safe_read_bytes(path: Path, max_bytes: Optional[int]) -> bytes:
    try:
        if not path.exists():
            return "[Datei nicht gefunden: {0}]".format(path).encode(
                "utf-8", errors="replace"
            )
        size = path.stat().st_size
        with path.open("rb") as f:
            if max_bytes is not None and size > max_bytes:
                data = f.read(max_bytes)
                suffix = (
                    NL
                    + "[... Auszug; Datei größer als {0} Bytes, abgeschnitten ...]".format(
                        max_bytes
                    )
                    + NL
                )
                return data + suffix.encode("utf-8")
            return f.read()
    except Exception as ex:
        return "[Fehler beim Lesen von {0}: {1}]".format(path, ex).encode(
            "utf-8", errors="replace"
        )


def safe_read_text(path: Path, max_bytes: Optional[int]) -> str:
    data = safe_read_bytes(path, max_bytes)
    for enc in ("utf-8-sig", "utf-8", "latin-1", "cp1252"):
        try:
            return data.decode(enc)
        except Exception:
            continue
    return data.decode("utf-8", errors="replace")


def choose_existing(paths: List[Path]) -> Optional[Path]:
    for p in paths:
        if p.exists():
            return p
    return None


def file_language(ext: str) -> str:
    return {
        ".cs": "csharp",
        ".xaml": "xml",
        ".xml": "xml",
        ".json": "json",
        ".config": "xml",
        ".csproj": "xml",
        ".sln": "ini",
        ".props": "xml",
        ".targets": "xml",
        ".md": "markdown",
    }.get(ext.lower(), "text")


import fnmatch


def _norm(s: str) -> str:
    # Einheitliche, case-insensitive Pfad-/Namensvergleiche
    return s.replace("\\", "/").lower()


COMPILED_EXCLUDE_PATTERNS = tuple(_norm(p) for p in EXCLUDE_PATTERNS)


def is_excluded(path: Path, root: Path) -> bool:
    """
    True, wenn path anhand von EXCLUDE_PATTERNS ausgeschlossen werden soll.
    - Prüft sowohl den Dateinamen (basename) als auch den relativen Pfad zum root.
    - Matching ist case-insensitive; Wildcards werden unterstützt.
    """
    try:
        name = path.name.lower()
        rel = _norm(str(path.relative_to(root)))
    except Exception:
        # Fallback, falls relative_to fehlschlägt
        name = path.name.lower()
        rel = _norm(str(path))

    for pat in COMPILED_EXCLUDE_PATTERNS:
        if fnmatch.fnmatchcase(name, pat) or fnmatch.fnmatchcase(rel, pat):
            return True
    return False


def list_source_files(root: Path, exts: set, ignore_names: set) -> List[Path]:
    files: List[Path] = []
    if not root.exists():
        return files

    # Case-insensitive Vergleich für ignore_names (z. B. ".git", "bin", ...)
    ignore_names_lc = {n.lower() for n in ignore_names}

    for p in root.rglob("*"):
        try:
            # Verzeichnisse/Dateien ignorieren, wenn ein Pfadteil in ignore_names ist (case-insensitive)
            if any(part.lower() in ignore_names_lc for part in p.parts):
                continue

            if p.is_file() and p.suffix.lower() in exts:
                # Exclude-Patterns (case-insensitive, Wildcards) prüfen
                if is_excluded(p, root):
                    continue
                files.append(p)
        except Exception:
            continue

    files.sort(key=lambda x: str(x).lower())
    return files


def parse_csproj_tfm_and_lang(csproj: Path) -> Tuple[Optional[str], Optional[str]]:
    tfm = None
    lang = None
    try:
        text = safe_read_text(csproj, MAX_FILE_BYTES)
        import re

        m1 = re.search(r"<TargetFramework>([^<]+)</TargetFramework>", text)
        m2 = re.search(r"<TargetFrameworks>([^<]+)</TargetFrameworks>", text)
        ml = re.search(r"<LangVersion>([^<]+)</LangVersion>", text)
        if m1:
            tfm = m1.group(1).strip()
        elif m2:
            tfm = m2.group(1).strip()
        if ml:
            lang = ml.group(1).strip()
    except Exception:
        pass
    return tfm, lang


def detect_vs_version(repo: Path) -> str:
    vswhere = Path(
        r"C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"
    )
    if vswhere.exists():
        rc, out, _ = run_cmd(
            [
                str(vswhere),
                "-latest",
                "-products",
                "*",
                "-requires",
                "Microsoft.Component.MSBuild",
                "-property",
                "installationVersion",
            ]
        )
        if rc == 0 and out:
            try:
                major = int(out.split(".", 1)[0])
                return "Visual Studio {0} ({1})".format(
                    "2022" if major == 17 else ("2019" if major == 16 else "Unbekannt"),
                    out,
                )
            except Exception:
                return "Visual Studio (Version: {0})".format(out)
    slns = list(repo.glob("*.sln")) or list(repo.rglob("*.sln"))
    if slns:
        try:
            head = (
                slns[0].read_text(encoding="utf-8", errors="ignore").splitlines()[:10]
            )
            for line in head:
                if "VisualStudioVersion" in line:
                    ver = line.split("=", 1)[1].strip()
                    major = int(ver.split(".", 1)[0])
                    vs = (
                        "2022"
                        if major == 17
                        else ("2019" if major == 16 else "Unbekannt")
                    )
                    return "Visual Studio {0} ({1})".format(vs, ver)
                if "# Visual Studio Version" in line:
                    num = line.rsplit(" ", 1)[-1].strip()
                    vs = (
                        "2022"
                        if num.startswith("17")
                        else ("2019" if num.startswith("16") else "Unbekannt")
                    )
                    return "Visual Studio {0}".format(vs)
        except Exception:
            pass
    return "Visual Studio (Version nicht ermittelt)"


def get_repo_tree(
    root: Path, max_depth: int, ignore_names: set, max_entries_per_dir: int
) -> str:
    def helper(cur: Path, depth: int) -> List[str]:
        if depth > max_depth:
            return []
        if not cur.exists():
            return ["{0}[Pfad nicht gefunden] {1}".format("  " * depth, cur)]
        try:
            items = sorted(cur.iterdir(), key=lambda p: (p.is_file(), p.name.lower()))
        except Exception as ex:
            return [
                "{0}[Fehler beim Auflisten von {1}: {2}]".format("  " * depth, cur, ex)
            ]
        lines: List[str] = []
        count = 0
        for item in items:
            # Bestehender Ignore-Mechanismus nach Namen (z. B. ".git", "bin", ...)
            if item.name in ignore_names:
                continue

            # NEU: Dateien anhand EXCLUDE_PATTERNS ausblenden (case-insensitive, wildcards)
            if item.is_file() and is_excluded(item, root):
                continue

            display = (
                ("{0}{1}/".format("  " * depth, item.name))
                if item.is_dir()
                else ("{0}{1}".format("  " * depth, item.name))
            )
            lines.append(display)
            count += 1
            if count >= max_entries_per_dir:
                lines.append(
                    "{0}... (weitere Einträge ausgelassen)".format("  " * depth)
                )
                break
            if item.is_dir():
                lines.extend(helper(item, depth + 1))
        return lines

    lines = [str(root)]
    lines.extend(helper(root, 0))
    return NL.join(lines)


def get_data_tree_with_log_sample(
    root: Path, max_depth: int, ignore_names: set, max_entries_per_dir: int
) -> str:
    def helper(cur: Path, depth: int) -> List[str]:
        if depth > max_depth:
            return []
        if not cur.exists():
            return ["{0}[Pfad nicht gefunden] {1}".format("  " * depth, cur)]
        try:
            items = sorted(cur.iterdir(), key=lambda p: (p.is_file(), p.name.lower()))
        except Exception as ex:
            return [
                "{0}[Fehler beim Auflisten von {1}: {2}]".format("  " * depth, cur, ex)
            ]
        lines: List[str] = []
        count = 0
        for item in items:
            if item.name in ignore_names:
                continue

            # SPEZIALFALL: 05_Log – hier wird eine Beispiel-Logdatei gezeigt
            if item.is_dir() and item.name == "05_Log":
                lines.append("{0}{1}/".format("  " * depth, item.name))
                try:
                    logs = sorted(
                        [p for p in item.iterdir() if p.is_file()],
                        key=lambda p: p.name.lower(),
                    )
                    # NEU: Logs anhand EXCLUDE_PATTERNS filtern
                    logs = [p for p in logs if not is_excluded(p, root)]
                    if logs:
                        lines.append("{0}{1}".format("  " * (depth + 1), logs[0].name))
                except Exception:
                    pass
                continue

            # NEU: Dateien anhand EXCLUDE_PATTERNS ausblenden (case-insensitive, wildcards)
            if item.is_file() and is_excluded(item, root):
                continue

            display = (
                ("{0}{1}/".format("  " * depth, item.name))
                if item.is_dir()
                else ("{0}{1}".format("  " * depth, item.name))
            )
            lines.append(display)
            count += 1
            if count >= max_entries_per_dir:
                lines.append(
                    "{0}... (weitere Einträge ausgelassen)".format("  " * depth)
                )
                break
            if item.is_dir():
                lines.extend(helper(item, depth + 1))
        return lines

    lines = [str(root)]
    lines.extend(helper(root, 0))
    return NL.join(lines)


def tail_head_lines(path: Path, head: int, tail: int) -> str:
    if not path.exists():
        return "[Logdatei nicht gefunden: {0}]".format(path)
    try:
        with path.open("r", encoding="utf-8", errors="replace") as f:
            lines = f.readlines()
        total = len(lines)
        head_part = lines[: min(head, total)]
        tail_part = lines[max(0, total - tail) :]
        middle = []
        if total > head + tail:
            middle = [
                "... ({0} Zeilen ausgelassen) ...{1}".format(total - head - tail, NL)
            ]
        return "".join(head_part + middle + tail_part)
    except Exception as ex:
        return "[Fehler beim Lesen der Logdatei {0}: {1}]".format(path, ex)


def maybe_truncate_total(text: str, limit: Optional[int]) -> str:
    if limit is None or len(text) <= limit:
        return text
    keep = max(0, limit - 500)
    return (
        text[:keep]
        + NL
        + "[... Auszug aufgrund Gesamtgrößenlimit, restlicher Inhalt abgeschnitten ...]"
        + NL
    )


def read_preprompt(paths: List[Path]) -> str:
    p = choose_existing(paths)
    if p:
        return safe_read_text(p, MAX_FILE_BYTES).rstrip("\n") + NL + NL
    return "[prePrompt.md nicht gefunden]" + NL + NL


# ============================
# Prompt-Erstellung
# ============================


def build_prompt() -> str:
    parts: List[str] = []

    # prePrompt an den Anfang
    parts.append(read_preprompt(PREPROMPT_CANDIDATES))

    # Umgebung: C#/.NET/Visual Studio
    tfm, lang = None, None
    csprojs = sorted(REPO_PATH.glob("*.csproj")) or sorted(REPO_PATH.rglob("*.csproj"))
    if csprojs:
        tfm, lang = parse_csproj_tfm_and_lang(csprojs[0])
    vs_ver = detect_vs_version(REPO_PATH)
    env = [
        "## Umgebung",
        "- .NET TargetFramework(s): {0}".format(tfm if tfm else "unbekannt"),
        "- C# LangVersion: {0}".format(lang if lang else "Standard (SDK-gesteuert)"),
        "- Visual Studio: {0}".format(vs_ver),
        "- OS: {0}".format(platform.platform()),
    ]
    parts.append(NL.join(env))

    # Pfade und Git
    cfg_path = choose_existing(CONFIG_CANDIDATES)
    paths = [
        "## Pfade",
        "- Repository: {0}".format(REPO_PATH),
        # "- Datenwurzeln: {0}, {1}".format(PROJECT_ROOT, TNA_ROOT, FPT_ROOT),
        # "- Beispiel-Config: {0}".format(cfg_path if cfg_path else "[nicht gefunden]"),
        # "- Beispiel-Log: {0}".format(SAMPLE_LOG_PATH),
    ]
    parts.append(NL.join(paths))

    git = get_git_info(REPO_PATH)
    git_lines = [
        "## Git",
        "- Branch: {0}".format(git.get("branch", "unbekannt")),
        "- Letzter Commit: {0}".format(git.get("last_commit", "unbekannt")),
        "- Remotes:",
        "```",
        git.get("remotes", ""),
        "```",
    ]
    parts.append(NL.join(git_lines))

    # Verzeichnisbäume
    repo_tree = [
        "## Verzeichnisbaum (Repo)",
        "```text",
        get_repo_tree(
            REPO_PATH,
            REPO_TREE_MAX_DEPTH,
            REPO_TREE_IGNORE,
            REPO_TREE_MAX_ENTRIES_PER_DIR,
        ),
        "```",
    ]
    parts.append(NL.join(repo_tree))

    # Beispiel-Config
    cfg_section = ["## Beispiel settings.json"]
    if cfg_path:
        cfg_text = safe_read_text(cfg_path, MAX_FILE_BYTES)
        cfg_section.extend(["```json", cfg_text.rstrip("\n"), "```"])
    else:
        cfg_section.append("[Beispiel-Config nicht gefunden]")
    parts.append(NL.join(cfg_section))

    # Alle Quelltexte
    src_files = list_source_files(REPO_PATH, SOURCE_EXTS, REPO_TREE_IGNORE)
    files_section: List[str] = ["## Dateien (alle Quelltexte)"]
    for fp in src_files:
        try:
            rel = fp.relative_to(REPO_PATH).as_posix()
        except Exception:
            rel = str(fp)
        lang_hint = file_language(fp.suffix)
        content = safe_read_text(fp, MAX_FILE_BYTES)
        files_section.append("File: {0}".format(rel))
        files_section.append("```{0}".format(lang_hint))
        files_section.append(content.rstrip("\n"))
        files_section.append("```")
        files_section.append("")  # Leerzeile zwischen Dateien
    parts.append(NL.join(files_section))

    prompt = (NL + NL).join(parts)
    prompt = maybe_truncate_total(prompt, MAX_TOTAL_CHARS)
    return prompt


def main():
    ensure_parent_dir(OUTPUT_PATH)
    prompt = build_prompt()
    with OUTPUT_PATH.open("w", encoding="utf-8", errors="replace", newline="\n") as f:
        f.write(prompt + "\n")
    print("Prompt erzeugt: {0}".format(OUTPUT_PATH))


if __name__ == "__main__":
    main()
