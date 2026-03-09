#!/usr/bin/env python3
"""
extract_pitfalls.py — Scan Cursor agent transcripts and extract pitfall candidate signals.

Portable: this script auto-derives all paths from its own location.
Place it anywhere under {project}/.cursor/gamedev/coder/scripts/ and it will work.

Layout assumption:
    {project}/
    └── .cursor/
        ├── rules/coder-rules.mdc
        └── gamedev/coder/
            ├── reports/          ← output goes here
            └── scripts/
                └── extract_pitfalls.py   ← this file

Usage:
    python extract_pitfalls.py                  # scan since last run
    python extract_pitfalls.py --all            # scan all transcripts
    python extract_pitfalls.py --days 7         # scan last N days
    python extract_pitfalls.py --dry-run        # print report, don't save state

Output:
    .cursor/gamedev/coder/reports/YYYY-MM-DD-pitfall-candidates.md
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from pathlib import Path
from typing import Optional

# ─────────────────────────────────────────────────────────────
# Portable paths  (all derived from this file's location)
# ─────────────────────────────────────────────────────────────
#
# parents[0] = coder/scripts/
# parents[1] = coder/                 ← CODER_DIR
# parents[2] = .cursor/gamedev/
# parents[3] = .cursor/
# parents[4] = {project root}/        ← CLIENT_DIR  (where assemblies live)

SCRIPT_PATH = Path(__file__).resolve()
CODER_DIR   = SCRIPT_PATH.parents[1]
CLIENT_DIR  = SCRIPT_PATH.parents[4]

REPORTS_DIR  = CODER_DIR / "reports"
STATE_FILE   = SCRIPT_PATH.parent / ".extract_pitfalls_state.json"
CODER_RULES  = CLIENT_DIR.parent / ".cursor" / "rules" / "coder-rules.mdc"


def _find_transcript_dir() -> Optional[Path]:
    """
    Auto-detect Cursor's agent-transcripts folder for the current project.

    Cursor stores transcripts at:
        ~/.cursor/projects/{project-id}/agent-transcripts/

    The project-id is derived from the project path by:
        - lowercasing the drive letter
        - replacing path separators and underscores with '-'
    Example: C:\\XDTWorkspace\\linan1_stage_001\\Client
             → c-XDTWorkspace-linan1-stage-001-Client
    """
    cursor_projects = (
        Path(os.environ.get("USERPROFILE", os.environ.get("HOME", "~"))).expanduser()
        / ".cursor" / "projects"
    )
    if not cursor_projects.exists():
        return None

    # Derive expected project-id from CLIENT_DIR
    parts = CLIENT_DIR.parts          # ('C:\\', 'XDT...', ..., 'Client')
    drive = parts[0].rstrip(":\\/").lower()   # 'c'
    rest  = [p.replace("_", "-") for p in parts[1:]]
    project_id = "-".join([drive] + rest)

    candidate = cursor_projects / project_id / "agent-transcripts"
    if candidate.exists():
        return candidate

    # Fallback: scan ~/.cursor/projects/ for a dir whose name contains the project root name
    project_name_lc = CLIENT_DIR.name.lower()
    for d in cursor_projects.iterdir():
        if d.is_dir() and project_name_lc in d.name.lower():
            t = d / "agent-transcripts"
            if t.exists():
                return t

    return None


TRANSCRIPT_DIR: Optional[Path] = _find_transcript_dir()

# ─────────────────────────────────────────────────────────────
# Signal definitions
# ─────────────────────────────────────────────────────────────

# ── High-confidence: user explicitly corrects AI after AI made a code/tool action ──
USER_CORRECTION_PATTERNS = [
    (r"不对[，。！\s]", 0.9),
    (r"不是这样", 0.9),
    (r"你理解错了", 0.95),
    (r"你搞错了", 0.95),
    (r"理解有误", 0.9),
    (r"不要这样(做|改|写)", 0.85),
    (r"这是错的", 0.9),
    (r"我说的不是", 0.85),
    (r"不对，应该", 0.9),
    (r"\b(revert|undo|rollback)\b", 0.8),
    (r"\b(that.{0,5}not (what|right|correct))\b", 0.85),
    (r"\b(no[,.]? (that.{0,5}wrong|not (right|correct|what i (meant|wanted|said))))\b", 0.9),
    (r"\b(wrong (direction|approach|file|class|method|api))\b", 0.85),
    (r"\b(don.t (do|use|call|modify) that)\b", 0.8),
    (r"\b(you('re| are) (misunderstanding|wrong about|confusing))\b", 0.9),
    (r"撤销这(个|次|些)", 0.8),
    (r"回滚", 0.75),
]

# ── Medium-confidence: AI self-corrects / acknowledges its own mistake ──
AI_SELF_CORRECTION_PATTERNS = [
    # Chinese: explicit apology + correction
    (r"(抱歉|对不起)[，。！].{0,30}(修正|修复|重新|让我)", 0.85),
    (r"(我|刚才)(理解|做)(错了|有误)", 0.9),
    (r"(修正|纠正)(一下|命令|调用|参数)", 0.8),
    (r"(命令失败|调用失败|工具失败|执行失败).{0,60}(修正|换用|改用|重试)", 0.85),
    (r"(失败).{0,30}(让我|需要)(修正|重新|换)", 0.8),
    (r"(参数(有误|错误|不对)).{0,40}(修正|重新)", 0.85),
    # English: explicit self-correction phrases
    (r"\blet me (fix|correct|retry|try again|revise)\b", 0.85),
    (r"\b(my mistake|my bad|i was wrong|i made an error)\b", 0.85),
    (r"\b(apologies|sorry).{0,30}(fix|correct|retry|rethink)\b", 0.8),
    (r"\b(i misunderstood|i got that wrong|incorrect approach)\b", 0.9),
    (r"\b(that.{0,5}(wrong|incorrect|not right)).{0,30}(let me|i.ll|i will)\b", 0.85),
    # English: tool/command failure + pivot
    (r"\b(failed|error).{0,50}(let me|i.ll|need to|should).{0,30}(fix|change|use|try)\b", 0.8),
    (r"\b(the command failed|command not found|not recognized).{0,80}(fix|escape|use|change)\b", 0.85),
    (r"\b(powershell|bash|shell).{0,50}(interprets?|treats?|parses?).{0,50}(differently|as)\b", 0.8),
    # English: escaping/quoting pitfalls (common tool-call pattern)
    (r"\b(need to escape|needs? to be escaped|special.{0,10}(char|symbol|operator))\b", 0.8),
    (r"\b(splatting|splat operator|@ sign.{0,30}powershell)\b", 0.9),
    # English: wrong API / parameter usage
    (r"\b(used the wrong (method|api|parameter|argument|flag))\b", 0.85),
    (r"\b(should (have used|use) .{3,40} instead)\b", 0.8),
    (r"\b(deprecated|removed in).{0,50}(use .{3,40} instead)\b", 0.8),
]

# ── High-confidence error: tool/compile failures (specific, not generic) ──
TOOL_ERROR_PATTERNS = [
    (r"NullReferenceException", 0.9),
    (r"IndexOutOfRange\w*", 0.9),
    (r"StackOverflow\w*", 0.9),
    (r"CS\d{4}:", 0.9),                       # Unity compiler error codes
    (r"Assets/.*\.cs\(\d+,\d+\)", 0.9),       # Unity error location format
    (r"build failed", 0.85),
    (r"compile error", 0.85),
    (r"编译(失败|错误)", 0.85),
    (r"Exit code: [1-9]", 0.75),              # shell command failure
    (r"CommandException|ToolException", 0.8),
    (r"ParserError|InvalidOperation|ParameterBinding", 0.85),
    (r"is not recognized as.*(cmdlet|function|command)", 0.8),
    (r"无法识别|不是.*命令|不是.*函数", 0.8),
]

# ── Explicit pitfall/lesson keywords ──
EXPLICIT_PITFALL_PATTERNS = [
    (r"踩坑[了：:]", 0.95),
    (r"[^查]坑[，。！\s：:]", 0.85),
    (r"\bpitfall\b", 0.9),
    (r"\bgotcha\b", 0.85),
    (r"already known issue|known limitation", 0.8),
    (r"已知(问题|限制)", 0.8),
    (r"注意[:：].{5,}", 0.7),
    (r"不(能|可以|要)(直接|在这里)用", 0.8),
    (r"这里有个(坑|陷阱|问题)", 0.9),
]

# ── Noise filters: reduce confidence when context looks like report/help text ──
NOISE_CONTEXT_PATTERNS = [
    r"代码审查报告",
    r"每日代码审查",
    r"审查范围",
    r"skill.*说明",
    r"工作流程",
    r"功能[：:].{0,20}自动",
    r"以下.*功能",
    r"支持.*以下",
    r"触发方式",
]

# ─────────────────────────────────────────────────────────────
# Assembly detection  (loaded lazily from coder-rules.mdc)
# ─────────────────────────────────────────────────────────────

_ASSEMBLY_CACHE: list[str] = []


def _load_known_assemblies() -> list[str]:
    global _ASSEMBLY_CACHE
    if _ASSEMBLY_CACHE:
        return _ASSEMBLY_CACHE

    # Parse backtick-quoted names from coder-rules.mdc architecture section
    assemblies: list[str] = []
    if CODER_RULES.exists():
        text = CODER_RULES.read_text(encoding="utf-8", errors="replace")
        # Match bare identifiers on architecture tree lines (CamelCase words ≥4 chars)
        for m in re.finditer(r"\b([A-Z][A-Za-z0-9]{3,})\b", text):
            name = m.group(1)
            if name not in assemblies:
                assemblies.append(name)

    # Fallback hardcoded list
    if not assemblies:
        assemblies = [
            "CoreEntry", "GMTools", "XDTViewBase", "XDTLevelAndEntity",
            "Gameplay", "ViewEntity", "GameScenes", "BaseSystem",
            "XDTGameSystem", "SDKSystem", "Input", "GameplaySystem",
            "XDTDataAndProtocol", "Network", "Config", "Events", "ComponentsData",
            "EcsClient", "EcsSystem", "XDTBaseService",
            "Core", "Pool", "World", "Profiler", "Framework", "Foundations",
            "ActionGraph", "EventCenter", "FlowGraph", "Graph", "Hierarchies",
            "KDTree", "PhysicQuery", "Playable", "Priority", "QuadTree",
            "Utility", "Services", "Audio", "Cache", "Device",
            "JobBalancer", "ObjectPoolManager", "ObsSDKManager", "ResourceManager",
            "Scene", "SceneQuery", "SDK", "Texture", "Timeline",
            "EngineWrapper", "Assets", "Packages", "MonoApp", "XDTMonoProjects",
        ]

    _ASSEMBLY_CACHE = assemblies
    return assemblies


# ─────────────────────────────────────────────────────────────
# Data classes
# ─────────────────────────────────────────────────────────────

@dataclass
class Turn:
    role: str          # "user" | "assistant"
    text: str
    idx: int


@dataclass
class Signal:
    signal_type: str   # CORRECTION | AI_CORRECTION | TOOL_ERROR | EXPLICIT_PITFALL
    transcript_id: str
    turn_idx: int
    trigger_text: str
    context_window: list[str]
    assembly_hints: list[str]
    file_hints: list[str]
    confidence: float = 0.0


@dataclass
class TranscriptResult:
    transcript_id: str
    mtime: float
    signals: list[Signal] = field(default_factory=list)


# ─────────────────────────────────────────────────────────────
# State management
# ─────────────────────────────────────────────────────────────

def load_state() -> dict:
    if STATE_FILE.exists():
        return json.loads(STATE_FILE.read_text(encoding="utf-8"))
    return {"last_scan_ts": 0.0, "scanned_ids": []}


def save_state(state: dict) -> None:
    STATE_FILE.write_text(json.dumps(state, indent=2, ensure_ascii=False), encoding="utf-8")


# ─────────────────────────────────────────────────────────────
# Transcript parsing
# ─────────────────────────────────────────────────────────────

def parse_transcript(path: Path) -> list[Turn]:
    turns: list[Turn] = []
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            for idx, raw in enumerate(f):
                raw = raw.strip()
                if not raw:
                    continue
                try:
                    obj = json.loads(raw)
                except json.JSONDecodeError:
                    continue
                role = obj.get("role", "")
                content = obj.get("message", {}).get("content", [])
                text = " ".join(
                    c.get("text", "") for c in content if c.get("type") == "text"
                )
                for c in content:
                    if c.get("type") == "tool_result":
                        for inner in c.get("content", []):
                            text += " " + inner.get("text", "")
                turns.append(Turn(role=role, text=text, idx=idx))
    except Exception as e:
        print(f"  [warn] failed to parse {path.name}: {e}", file=sys.stderr)
    return turns


# ─────────────────────────────────────────────────────────────
# Signal detection
# ─────────────────────────────────────────────────────────────

def _matches_scored(text: str, patterns: list[tuple[str, float]]) -> Optional[tuple[str, float]]:
    for pat, score in patterns:
        m = re.search(pat, text, re.IGNORECASE)
        if m:
            return m.group(0), score
    return None


def _is_noisy_context(context_text: str) -> bool:
    for pat in NOISE_CONTEXT_PATTERNS:
        if re.search(pat, context_text, re.IGNORECASE):
            return True
    return False


def _extract_assembly_hints(text: str) -> list[str]:
    assemblies = _load_known_assemblies()
    found = []
    for asm in assemblies:
        if asm in text:
            found.append(asm)
    return list(dict.fromkeys(found))


def _extract_file_hints(text: str) -> list[str]:
    pattern = r"[\w/\\.\-]+\.(?:cs|prefab|unity|asset|json|yaml)[^\s\"']*"
    matches = re.findall(pattern, text)
    return list(dict.fromkeys(matches))[:5]


def _context_window(turns: list[Turn], center_idx: int, radius: int = 2) -> list[str]:
    lo = max(0, center_idx - radius)
    hi = min(len(turns) - 1, center_idx + radius)
    result = []
    for i in range(lo, hi + 1):
        t = turns[i]
        label = "U" if t.role == "user" else "A"
        snippet = t.text[:200].replace("\n", " ")
        marker = ">>>" if i == center_idx else "   "
        result.append(f"{marker}[{label}{i}] {snippet}")
    return result


def detect_signals(turns: list[Turn], transcript_id: str,
                   min_confidence: float = 0.7) -> list[Signal]:
    signals: list[Signal] = []

    for turn in turns:
        sig_type: Optional[str] = None
        trigger: Optional[str] = None
        confidence: float = 0.0

        if turn.role == "user":
            result = _matches_scored(turn.text, USER_CORRECTION_PATTERNS)
            if result:
                trigger, confidence = result
                sig_type = "CORRECTION"
            if not sig_type:
                result = _matches_scored(turn.text, EXPLICIT_PITFALL_PATTERNS)
                if result:
                    trigger, confidence = result
                    sig_type = "EXPLICIT_PITFALL"

        elif turn.role == "assistant":
            result = _matches_scored(turn.text, AI_SELF_CORRECTION_PATTERNS)
            if result:
                trigger, confidence = result
                sig_type = "AI_CORRECTION"
            if not sig_type:
                result = _matches_scored(turn.text, EXPLICIT_PITFALL_PATTERNS)
                if result:
                    trigger, confidence = result
                    sig_type = "EXPLICIT_PITFALL"

        if sig_type is None:
            result = _matches_scored(turn.text, TOOL_ERROR_PATTERNS)
            if result:
                trigger, confidence = result
                sig_type = "TOOL_ERROR"

        if sig_type is None or len(turn.text.strip()) < 20:
            continue

        ctx = _context_window(turns, turn.idx)
        combined = " ".join(t.text for t in turns[max(0, turn.idx - 3): turn.idx + 4])

        if _is_noisy_context(combined):
            confidence *= 0.3

        if confidence < min_confidence:
            continue

        signals.append(Signal(
            signal_type=sig_type,
            transcript_id=transcript_id,
            turn_idx=turn.idx,
            trigger_text=trigger or "",
            context_window=ctx,
            assembly_hints=_extract_assembly_hints(combined),
            file_hints=_extract_file_hints(combined),
            confidence=confidence,
        ))

    return signals


# ─────────────────────────────────────────────────────────────
# Report generation
# ─────────────────────────────────────────────────────────────

SIGNAL_EMOJI = {
    "CORRECTION":       "⚠️  用户纠正 AI",
    "AI_CORRECTION":    "🔧 AI 自我纠正",
    "TOOL_ERROR":       "🔴 工具/编译错误",
    "EXPLICIT_PITFALL": "📌 明确踩坑",
}


def format_report(results: list[TranscriptResult], scan_date: str, since_ts: float) -> str:
    total_signals = sum(len(r.signals) for r in results)
    since_str = (
        datetime.fromtimestamp(since_ts).strftime("%Y-%m-%d %H:%M")
        if since_ts > 0 else "全量"
    )

    lines = [
        f"# Pitfall 候选提取报告 — {scan_date}",
        "",
        f"扫描范围: {len(results)} 个 transcript（自 {since_str}）",
        f"检测到候选信号: **{total_signals}** 条",
        "",
        "---",
        "",
        "## 操作说明",
        "",
        "1. 浏览下方候选条目，评估是否值得记入 `pitfalls.md`",
        "2. 确认后将条目复制到对应程序集的 `.dna/pitfalls.md`",
        "3. 填写「根因」和「修复」字段",
        "4. 删除本报告中已处理的条目",
        "",
        "---",
        "",
    ]

    if total_signals == 0:
        lines.append("*本次扫描未检测到明确的踩坑信号。*")
        return "\n".join(lines)

    by_assembly: dict[str, list[tuple[TranscriptResult, Signal]]] = {}
    for result in results:
        for sig in result.signals:
            key = sig.assembly_hints[0] if sig.assembly_hints else "未识别程序集"
            by_assembly.setdefault(key, []).append((result, sig))

    for asm, pairs in sorted(by_assembly.items()):
        lines.append(f"## 程序集: `{asm}`")
        lines.append("")

        dna_path = _find_dna_path(asm)
        if dna_path:
            lines.append(f"> 写入目标: `{dna_path}`")
        else:
            lines.append(f"> 写入目标: (未找到 .dna/pitfalls.md，请手动确认)")
        lines.append("")

        for result, sig in pairs:
            label = SIGNAL_EMOJI.get(sig.signal_type, sig.signal_type)
            conf_bar = "█" * int(sig.confidence * 5) + "░" * (5 - int(sig.confidence * 5))
            conf_pct = f"{sig.confidence:.0%}"
            lines.append(
                f"### {label} | `{result.transcript_id[:8]}…` turn {sig.turn_idx}"
                f" | 置信度 {conf_bar} {conf_pct}"
            )
            lines.append("")
            lines.append(f"**触发词**: `{sig.trigger_text}`")
            if sig.file_hints:
                lines.append(f"**相关文件**: {', '.join(f'`{f}`' for f in sig.file_hints[:3])}")
            if len(sig.assembly_hints) > 1:
                lines.append(f"**其他程序集**: {', '.join(sig.assembly_hints[1:])}")
            lines.append("")
            lines.append("**上下文**:")
            lines.append("```")
            lines.extend(sig.context_window)
            lines.append("```")
            lines.append("")

            tag = _suggest_tag(sig)
            file_str = sig.file_hints[0] if sig.file_hints else asm
            lines.append("**建议 pitfalls.md 条目** (填写根因和修复后复制):")
            lines.append("```markdown")
            lines.append(f"- [{scan_date}] #{tag} (根据上下文补充简述) — auto-extracted")
            lines.append(f"  - 根因: ")
            lines.append(f"  - 修复: ")
            lines.append(f"  - 影响: {file_str}")
            lines.append("```")
            lines.append("")
            lines.append("---")
            lines.append("")

    return "\n".join(lines)


def _find_dna_path(assembly_name: str) -> Optional[str]:
    """Try to locate .dna/pitfalls.md for an assembly under CLIENT_DIR."""
    for dirpath, dirnames, _ in os.walk(CLIENT_DIR):
        dirnames[:] = [d for d in dirnames if not d.startswith(".") or d == ".dna"]
        if Path(dirpath).name == assembly_name:
            candidate = Path(dirpath) / ".dna" / "pitfalls.md"
            if candidate.exists():
                try:
                    return str(candidate.relative_to(CLIENT_DIR))
                except ValueError:
                    return str(candidate)
    return None


def _suggest_tag(sig: Signal) -> str:
    if sig.signal_type == "TOOL_ERROR":
        text = sig.trigger_text.lower()
        if any(k in text for k in ("null", "index", "ref", "exception")):
            return "logic"
        if any(k in text for k in ("cs", "compile", "build", "编译")):
            return "architecture"
        return "logic"
    if sig.signal_type == "CORRECTION":
        return "api"
    return "architecture"


# ─────────────────────────────────────────────────────────────
# Main
# ─────────────────────────────────────────────────────────────

def main() -> None:
    parser = argparse.ArgumentParser(
        description="Extract pitfall candidates from Cursor agent transcripts."
    )
    parser.add_argument("--all", action="store_true",
                        help="Scan all transcripts (ignore last-scan state)")
    parser.add_argument("--days", type=int, default=0,
                        help="Scan transcripts from the last N days")
    parser.add_argument("--dry-run", action="store_true",
                        help="Print report without saving state")
    parser.add_argument("--min-confidence", type=float, default=0.7,
                        help="Minimum confidence score (0.0–1.0, default 0.7)")
    parser.add_argument("--transcript-dir", type=Path, default=None,
                        help="Override transcript directory path")
    args = parser.parse_args()

    # Resolve transcript dir
    transcript_dir: Optional[Path] = args.transcript_dir or TRANSCRIPT_DIR
    if transcript_dir is None or not transcript_dir.exists():
        print(
            f"[error] Transcript directory not found: {transcript_dir}\n"
            f"  Auto-detected from CLIENT_DIR: {CLIENT_DIR}\n"
            f"  Use --transcript-dir to override.",
            file=sys.stderr,
        )
        sys.exit(1)

    state = load_state()
    since_ts = 0.0

    if args.all:
        since_ts = 0.0
    elif args.days > 0:
        since_ts = (datetime.now() - timedelta(days=args.days)).timestamp()
    else:
        since_ts = state["last_scan_ts"]

    all_files = sorted(transcript_dir.glob("*.jsonl"),
                       key=lambda p: p.stat().st_mtime)
    eligible = [p for p in all_files if p.stat().st_mtime > since_ts]
    already_scanned = set(state.get("scanned_ids", []))
    new_files = [p for p in eligible if p.stem not in already_scanned]

    print(f"Project root         : {CLIENT_DIR}")
    print(f"Transcript directory : {transcript_dir}")
    print(f"Scan since           : {datetime.fromtimestamp(since_ts).strftime('%Y-%m-%d %H:%M') if since_ts else 'beginning'}")
    print(f"Eligible files       : {len(eligible)} total, {len(new_files)} new")

    if not new_files:
        print("Nothing new to scan.")
        return

    results: list[TranscriptResult] = []
    for path in new_files:
        turns = parse_transcript(path)
        if not turns:
            continue
        signals = detect_signals(turns, path.stem, min_confidence=args.min_confidence)
        result = TranscriptResult(
            transcript_id=path.stem,
            mtime=path.stat().st_mtime,
            signals=signals,
        )
        results.append(result)
        if signals:
            print(f"  {path.name}: {len(signals)} signal(s)")

    total = sum(len(r.signals) for r in results)
    print(f"\nTotal signals found  : {total}")

    today = datetime.now().strftime("%Y-%m-%d")
    report = format_report(results, today, since_ts)

    REPORTS_DIR.mkdir(parents=True, exist_ok=True)
    report_path = REPORTS_DIR / f"{today}-pitfall-candidates.md"
    report_path.write_text(report, encoding="utf-8")
    print(f"Report written       : {report_path}")

    if not args.dry_run:
        new_scanned = list(already_scanned) + [p.stem for p in new_files]
        state["last_scan_ts"] = datetime.now().timestamp()
        state["scanned_ids"] = new_scanned
        save_state(state)
        print("State updated.")


if __name__ == "__main__":
    main()
