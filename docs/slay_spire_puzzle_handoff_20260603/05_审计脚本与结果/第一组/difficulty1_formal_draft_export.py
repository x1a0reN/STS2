from __future__ import annotations

import argparse
import json
from pathlib import Path

from difficulty1_multiturn_search import (
    SearchVariant,
    audit_variant,
    deck_turn_report_precise,
    expected_kill_turn,
    variant_catalog,
)


DEFAULT_VARIANT_NAME = "six_strike_def3_perfected_hp50_php8_turn4"
DEFAULT_SOLUTIONS: list[tuple[str, tuple[str, ...]]] = [
    ("fast_2_turn", ("Strike", "Strike", "Strike", "Strike", "Strike", "Strike", "Perfected Strike")),
    ("main_3_turn", ("Strike", "Strike", "Strike", "Strike", "Perfected Strike", "Defend", "Defend")),
    ("slow_4_turn", ("Strike", "Strike", "Strike", "Strike", "Strike", "Strike", "Defend", "Defend", "Defend")),
]
DEFAULT_OUTPUT_JSON = Path("difficulty1_cultist_case_machine_export.json")
RANDOM_BRANCH_BASE_CARDS = frozenset({"Battle Trance", "Pommel Strike", "Shrug It Off", "Burning Pact"})


def parse_damage_seq(text: str) -> tuple[int, ...]:
    return tuple(int(part.strip()) for part in text.split(",") if part.strip())


def parse_cards(text: str) -> tuple[str, ...]:
    return tuple(card.strip() for card in text.split(",") if card.strip())


def parse_solution(text: str) -> tuple[str, tuple[str, ...]]:
    if "=" not in text:
        raise argparse.ArgumentTypeError("Solution must use label=CardA,CardB,... format")
    label, cards_text = text.split("=", 1)
    label = label.strip()
    if not label:
        raise argparse.ArgumentTypeError("Solution label cannot be empty")
    cards = parse_cards(cards_text)
    if not cards:
        raise argparse.ArgumentTypeError("Solution deck cannot be empty")
    return label, cards


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Export machine-readable reports for a candidate family or formal draft.")
    parser.add_argument("--variant-name", default=DEFAULT_VARIANT_NAME, help="Built-in variant name from difficulty1_multiturn_search.py")
    parser.add_argument("--card-pool", help="Comma-separated card pool override for custom variants")
    parser.add_argument("--enemy-hp", type=int, help="Custom variant enemy HP")
    parser.add_argument("--player-hp", type=int, help="Custom variant player HP")
    parser.add_argument("--damage-seq", help="Custom damage sequence like 0,9,11,13")
    parser.add_argument("--min-deck-size", type=int, default=7)
    parser.add_argument("--max-deck-size", type=int, default=9)
    parser.add_argument("--draw-per-turn", type=int, default=5)
    parser.add_argument(
        "--solution",
        action="append",
        type=parse_solution,
        help="Repeatable solution definition: label=CardA,CardB,...",
    )
    parser.add_argument("--output", default=str(DEFAULT_OUTPUT_JSON), help="Output JSON path")
    return parser


def choose_variant(args: argparse.Namespace) -> SearchVariant:
    if args.card_pool:
        if args.enemy_hp is None or args.player_hp is None or args.damage_seq is None:
            raise SystemExit("Custom variants require --card-pool, --enemy-hp, --player-hp, and --damage-seq")
        return SearchVariant(
            name=args.variant_name,
            card_pool=parse_cards(args.card_pool),
            enemy_hp=args.enemy_hp,
            player_hp=args.player_hp,
            enemy_damage_by_turn=parse_damage_seq(args.damage_seq),
            min_deck_size=args.min_deck_size,
            max_deck_size=args.max_deck_size,
            draw_per_turn=args.draw_per_turn,
        )

    variants = {variant.name: variant for variant in variant_catalog()}
    if args.variant_name not in variants:
        raise SystemExit(f"Unknown variant: {args.variant_name}")
    return variants[args.variant_name]


def choose_solutions(args: argparse.Namespace) -> list[tuple[str, tuple[str, ...]]]:
    return args.solution or DEFAULT_SOLUTIONS


def solution_summary(label: str, report: dict[str, object]) -> dict[str, object]:
    exact_result = tuple(report["exact_result"])  # type: ignore[arg-type]
    return {
        "label": label,
        "deck": report["deck"],
        "deck_display": report["deck_display"],
        "exact_result": report["exact_result"],
        "expected_kill_turn_if_success": expected_kill_turn(exact_result),
        "turn_rows": report["turn_rows"],
    }


def turn_rows_precision(card_pool: tuple[str, ...], solutions: list[tuple[str, tuple[str, ...]]]) -> str:
    return "authoritative"


def top_non_solution_rows(rows: list[dict[str, object]], solution_signatures: set[tuple[str, ...]]) -> list[dict[str, object]]:
    out: list[dict[str, object]] = []
    for row in rows:
        deck = tuple(row["deck"])  # type: ignore[arg-type]
        if deck in solution_signatures:
            continue
        out.append(
            {
                "deck": list(deck),
                "deck_size": row["deck_size"],
                "result": list(row["result"]),  # type: ignore[arg-type]
                "fail": row["fail"],
            }
        )
        if len(out) >= 5:
            break
    return out


def main() -> None:
    args = build_parser().parse_args()
    variant = choose_variant(args)
    solutions = choose_solutions(args)
    audit = audit_variant(variant)

    solution_reports = []
    solution_signatures: set[tuple[str, ...]] = set()
    for label, deck in solutions:
        sorted_deck = tuple(sorted(deck))
        solution_signatures.add(sorted_deck)
        report = deck_turn_report_precise(sorted_deck, variant)
        solution_reports.append(solution_summary(label, report))

    payload = {
        "variant": {
            "name": variant.name,
            "card_pool": list(variant.card_pool),
            "enemy_hp": variant.enemy_hp,
            "player_hp": variant.player_hp,
            "enemy_damage_by_turn": list(variant.enemy_damage_by_turn),
            "min_deck_size": variant.min_deck_size,
            "max_deck_size": variant.max_deck_size,
            "draw_per_turn": variant.draw_per_turn,
        },
        "solutions": solution_reports,
        "audit_summary": {
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
        },
        "export_meta": {
            "script": str(Path(__file__).name),
            "variant_name": variant.name,
            "solution_labels": [label for label, _ in solutions],
            "turn_rows_precision": turn_rows_precision(variant.card_pool, solutions),
        },
    }

    output_path = Path(args.output)
    output_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    print(str(output_path.resolve()))


if __name__ == "__main__":
    main()
