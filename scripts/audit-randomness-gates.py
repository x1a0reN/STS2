#!/usr/bin/env python3
"""Audit D1-D10 puzzle randomness gates and stale enumeration methods.

This script is intentionally a meta-audit, not a full probability solver. It
checks whether the current puzzle document and legacy audit scripts violate the
resource-first rule: resources decide viability; draw order only changes odds.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DOC = ROOT / "docs" / "D1-D10_final_puzzles.md"
AUDIT_DIR = (
    ROOT
    / "docs"
    / "slay_spire_puzzle_handoff_20260603"
    / "05_审计脚本与结果"
    / "第一组"
)

AUDIT_SCRIPTS = {
    1: ROOT / "scripts" / "enumerate-difficulty1-cultist.py",
    2: ROOT / "scripts" / "enumerate-difficulty2-armor-threshold.py",
    3: AUDIT_DIR / "difficulty3_double_sly_controlled_audit.js",
    4: AUDIT_DIR / "difficulty4_burn_countdown_controlled_audit.js",
    5: AUDIT_DIR / "difficulty5_void_collapse_priority_audit.js",
    6: AUDIT_DIR / "difficulty6_stance_controlled_audit.js",
    7: AUDIT_DIR / "difficulty7_poison_antidote_audit.cpp",
    8: AUDIT_DIR / "difficulty8_orb_insulation_audit.cpp",
    9: AUDIT_DIR / "difficulty9_divinity_mirror_audit.cpp",
    10: AUDIT_DIR / "difficulty10_time_rift_audit.js",
}


def split_stages(text: str) -> dict[int, str]:
    matches = list(re.finditer(r"^# 难度\s+(\d+)\s+", text, re.M))
    out: dict[int, str] = {}
    for i, match in enumerate(matches):
        stage = int(match.group(1))
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        out[stage] = text[match.start() : end]
    return out


def fixed_priority_risk(path: Path) -> str | None:
    if not path.exists():
        return "missing_script"
    text = path.read_text(encoding="utf-8", errors="ignore")
    if "bestFromCtx" in text and "decision_model" in text:
        return None
    greedy_js = re.search(r"while\s*\([^)]*plays[^)]*\)\s*{[\s\S]{0,500}?for\s*\((?:const|let)\s+card[\s\S]{0,500}?break\s*;", text)
    greedy_cpp = re.search(r"while\s*\([^)]*plays[^)]*\)\s*{[\s\S]{0,500}?for\s*\(\s*int\s+card[\s\S]{0,500}?break\s*;", text)
    ordered_cpp = re.search(r"const\s+int\s+order\[[^\]]+\]\s*=.*?while\s*\([^)]*plays[^)]*\).*?for\s*\(\s*int\s+oi", text, re.S)
    fixed_named = ("PLAY_PRIORITY" in text or "PLAY_ORDER" in text) and "bestFromCtx" not in text
    if greedy_js or greedy_cpp or ordered_cpp or fixed_named:
        return "fixed_priority_not_full_action_search"
    return None


def stage_risks(stage: int, body: str) -> list[str]:
    risks: list[str] = []
    if "待重新枚举" in body or "重新枚举" in body:
        risks.append("has_stale_probability")
    if stage == 5:
        if "若手牌中有虚空" in body:
            risks.append("d5_same_hand_void_text")
        if "手牌或弃牌堆中的虚空都可作为资源" not in body:
            risks.append("d5_missing_hand_or_discard_void_rule")
    if stage == 10 and "裂隙抽取" in body:
        risks.append("intentional_random_rift_requires_family_audit")
    return risks


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser()
    parser.add_argument("--strict", action="store_true", help="return non-zero when any risk is found")
    args = parser.parse_args()

    doc_text = DOC.read_text(encoding="utf-8")
    stages = split_stages(doc_text)
    any_risk = False

    print("stage,status,doc_risks,method_risk,script")
    for stage in range(1, 11):
        body = stages.get(stage, "")
        doc_risks = stage_risks(stage, body) if body else ["missing_stage_doc"]
        method_risk = fixed_priority_risk(AUDIT_SCRIPTS[stage])
        risks = doc_risks + ([method_risk] if method_risk else [])
        any_risk = any_risk or bool(risks)
        status = "WARN" if risks else "PASS"
        print(
            f"D{stage},{status},"
            f"{'|'.join(doc_risks) if doc_risks else '-'},"
            f"{method_risk or '-'},"
            f"{AUDIT_SCRIPTS[stage]}"
        )

    return 1 if args.strict and any_risk else 0


if __name__ == "__main__":
    raise SystemExit(main())
