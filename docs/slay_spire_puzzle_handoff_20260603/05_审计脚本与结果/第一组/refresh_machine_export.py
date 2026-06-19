from __future__ import annotations

import argparse
import json
from pathlib import Path

from difficulty1_formal_draft_export import RANDOM_BRANCH_BASE_CARDS, solution_summary, top_non_solution_rows, turn_rows_precision
from difficulty1_multiturn_search import SearchVariant, audit_variant, card_base_name, deck_turn_report_precise


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Refresh an existing machine export JSON with the current solver.")
    parser.add_argument("paths", nargs="*", help="Existing export JSON files to refresh in place")
    parser.add_argument(
        "--scan-cwd-random",
        action="store_true",
        help="Scan current directory for export JSON files whose card pool or example deck contains random-branch cards, and refresh them in place.",
    )
    return parser


def load_variant(payload: dict[str, object]) -> SearchVariant:
    variant = payload["variant"]  # type: ignore[index]
    if not isinstance(variant, dict) or "name" not in variant:
        raise KeyError("variant.name")
    return SearchVariant(
        name=variant["name"],  # type: ignore[index]
        card_pool=tuple(variant["card_pool"]),  # type: ignore[index]
        enemy_hp=variant["enemy_hp"],  # type: ignore[index]
        player_hp=variant["player_hp"],  # type: ignore[index]
        enemy_damage_by_turn=tuple(variant["enemy_damage_by_turn"]),  # type: ignore[index]
        min_deck_size=variant.get("min_deck_size", 7),  # type: ignore[union-attr]
        max_deck_size=variant.get("max_deck_size", 9),  # type: ignore[union-attr]
        draw_per_turn=variant.get("draw_per_turn", 5),  # type: ignore[union-attr]
    )


def load_solutions(payload: dict[str, object]) -> list[tuple[str, tuple[str, ...]]]:
    solutions = []
    if "solutions" in payload:
        for row in payload["solutions"]:  # type: ignore[index]
            solutions.append((row["label"], tuple(row["deck"])))  # type: ignore[index]
    elif "stable_example" in payload:
        row = payload["stable_example"]  # type: ignore[index]
        solutions.append(("stable_example", tuple(row["deck"])))  # type: ignore[index]
    return solutions


def load_cards_for_precision(payload: dict[str, object]) -> set[str]:
    cards = set()
    variant = payload.get("variant", {})
    if not isinstance(variant, dict):
        return cards
    for card in variant.get("card_pool", []):  # type: ignore[union-attr]
        cards.add(card_base_name(card))
    if "solutions" in payload:
        for row in payload["solutions"]:  # type: ignore[index]
            for card in row["deck"]:  # type: ignore[index]
                cards.add(card_base_name(card))
    elif "stable_example" in payload:
        for card in payload["stable_example"]["deck"]:  # type: ignore[index]
            cards.add(card_base_name(card))
    return cards


def should_refresh_random_branch(path: Path) -> bool:
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return False
    if "variant" not in payload:
        return False
    if "solutions" not in payload and "stable_example" not in payload:
        return False
    cards = load_cards_for_precision(payload)
    return bool(cards & RANDOM_BRANCH_BASE_CARDS)


def scan_random_branch_exports(cwd: Path) -> list[Path]:
    matches = []
    for path in sorted(cwd.glob("*.json")):
        if should_refresh_random_branch(path):
            matches.append(path)
    return matches


def refresh_payload(path: Path) -> None:
    payload = json.loads(path.read_text(encoding="utf-8"))
    variant = load_variant(payload)
    solutions = load_solutions(payload)
    audit = audit_variant(variant)

    solution_reports = []
    solution_signatures: set[tuple[str, ...]] = set()
    for label, deck in solutions:
        sorted_deck = tuple(sorted(deck))
        solution_signatures.add(sorted_deck)
        report = deck_turn_report_precise(sorted_deck, variant)
        solution_reports.append(solution_summary(label, report))

    refreshed = {"variant": payload["variant"]}
    if "solutions" in payload:
        refreshed["solutions"] = solution_reports
        refreshed["audit_summary"] = {
            "case_count": audit["case_count"],
            "stable_count": audit["stable_count"],
            "top_rows": [
                {
                    "deck": list(row["deck"]),  # type: ignore[arg-type]
                    "deck_size": row["deck_size"],
                    "result": list(row["result"]),  # type: ignore[arg-type]
                    "fail": row["fail"],
                }
                for row in audit["top_rows"]  # type: ignore[index]
            ],
            "top_non_solution_rows": top_non_solution_rows(audit["top_rows"], solution_signatures),  # type: ignore[arg-type]
        }
    elif "stable_example" in payload:
        stable = solution_reports[0].copy()
        stable.pop("label", None)
        refreshed["stable_example"] = stable
    else:
        raise SystemExit(f"Unsupported export shape: {path}")

    refreshed["export_meta"] = {
        **payload.get("export_meta", {}),
        "script": str(Path(__file__).name),
        "turn_rows_precision": turn_rows_precision(variant.card_pool, solutions),
        "refreshed_from": path.name,
    }
    path.write_text(json.dumps(refreshed, ensure_ascii=False, indent=2), encoding="utf-8")
    print(str(path.resolve()))


def main() -> None:
    args = build_parser().parse_args()
    targets: list[Path] = []
    if args.scan_cwd_random:
        targets.extend(scan_random_branch_exports(Path.cwd()))
    targets.extend(Path(raw_path) for raw_path in args.paths)

    # Preserve order while deduplicating.
    deduped: list[Path] = []
    seen: set[Path] = set()
    for path in targets:
        resolved = path.resolve()
        if resolved in seen:
            continue
        seen.add(resolved)
        deduped.append(path)

    if not deduped:
        raise SystemExit("No export files selected")

    for path in deduped:
        refresh_payload(path)


if __name__ == "__main__":
    main()
