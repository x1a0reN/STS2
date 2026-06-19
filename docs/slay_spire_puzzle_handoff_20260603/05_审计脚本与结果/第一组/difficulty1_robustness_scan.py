from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from itertools import product
from pathlib import Path

from difficulty1_multiturn_search import SearchVariant, exact_multiturn_result, unique_decks


@dataclass(frozen=True)
class Family:
    name: str
    card_pool: tuple[str, ...]


@dataclass
class RobustnessResult:
    family: Family
    valid_cases: int
    invalid_cases: int
    avg_t2: float
    avg_t3: float
    avg_t4: float
    avg_fail4: float
    min_t4: float
    max_t4: float
    sample_case: tuple[int, int, tuple[int, ...]] | None
    sample_best2: tuple[tuple[str, ...], tuple[float, ...]] | None
    sample_best3: tuple[tuple[str, ...], tuple[float, ...]] | None
    sample_best4: tuple[tuple[str, ...], tuple[float, ...]] | None


@dataclass
class CaseDetail:
    enemy_hp: int
    player_hp: int
    damage_sequence: tuple[int, ...]
    stable: bool
    best2: tuple[tuple[str, ...], tuple[float, ...]] | None
    best3: tuple[tuple[str, ...], tuple[float, ...]] | None
    best4: tuple[tuple[str, ...], tuple[float, ...]] | None


FAMILIES: tuple[Family, ...] = (
    Family(
        name="six_strike_def3_perfected",
        card_pool=("Strike", "Strike", "Strike", "Strike", "Strike", "Strike", "Defend", "Defend", "Defend", "Perfected Strike"),
    ),
    Family(
        name="six_strike_def3_double_perfected",
        card_pool=("Strike", "Strike", "Strike", "Strike", "Strike", "Strike", "Defend", "Defend", "Defend", "Perfected Strike", "Perfected Strike"),
    ),
    Family(
        name="seven_strike_def3_perfected",
        card_pool=("Strike", "Strike", "Strike", "Strike", "Strike", "Strike", "Strike", "Defend", "Defend", "Defend", "Perfected Strike"),
    ),
    Family(
        name="six_strike_def4_perfected",
        card_pool=("Strike", "Strike", "Strike", "Strike", "Strike", "Strike", "Defend", "Defend", "Defend", "Defend", "Perfected Strike"),
    ),
    Family(
        name="six_strike_def3_perfected_uppercut",
        card_pool=("Strike", "Strike", "Strike", "Strike", "Strike", "Strike", "Defend", "Defend", "Defend", "Perfected Strike", "Uppercut"),
    ),
    Family(
        name="six_strike_def3_perfected_bash",
        card_pool=("Strike", "Strike", "Strike", "Strike", "Strike", "Strike", "Defend", "Defend", "Defend", "Perfected Strike", "Bash"),
    ),
    Family(
        name="five_strike_def3_perfected_bash",
        card_pool=("Strike", "Strike", "Strike", "Strike", "Strike", "Defend", "Defend", "Defend", "Perfected Strike", "Bash"),
    ),
)


def first_success_turn(result: tuple[float, ...]) -> int | None:
    for idx, prob in enumerate(result[:-1], start=1):
        if prob > 1e-9:
            return idx
    return None


def choose_best(
    rows: list[tuple[tuple[str, ...], tuple[float, ...]]],
    target_turn: int,
) -> tuple[tuple[str, ...], tuple[float, ...]] | None:
    subset = [(deck, result) for deck, result in rows if first_success_turn(result) == target_turn]
    if not subset:
        return None
    subset.sort(key=lambda item: item[1][target_turn - 1], reverse=True)
    return subset[0]


def parse_cards(text: str) -> tuple[str, ...]:
    return tuple(card.strip() for card in text.split(",") if card.strip())


def parse_damage_sequence(text: str) -> tuple[int, ...]:
    return tuple(int(part.strip()) for part in text.split(","))  # type: ignore[return-value]


def evaluate_case(
    family: Family,
    enemy_hp: int,
    player_hp: int,
    damage_sequence: tuple[int, ...],
) -> CaseDetail:
    variant = SearchVariant(
        name=f"{family.name}_hp{enemy_hp}_php{player_hp}_dmg{'-'.join(str(x) for x in damage_sequence)}",
        card_pool=family.card_pool,
        enemy_hp=enemy_hp,
        player_hp=player_hp,
        enemy_damage_by_turn=damage_sequence,
    )
    rows: list[tuple[tuple[str, ...], tuple[float, ...]]] = []
    stable = False
    for deck in unique_decks(variant.card_pool, variant.min_deck_size, variant.max_deck_size):
        result = exact_multiturn_result(deck, variant)
        rows.append((deck, result))
        if result[-1] < 1e-12:
            stable = True
    return CaseDetail(
        enemy_hp=enemy_hp,
        player_hp=player_hp,
        damage_sequence=damage_sequence,
        stable=stable,
        best2=choose_best(rows, 2),
        best3=choose_best(rows, 3),
        best4=choose_best(rows, 4),
    )


def evaluate_family(
    family: Family,
    enemy_hps: tuple[int, ...],
    player_hps: tuple[int, ...],
    damage_sequences: tuple[tuple[int, ...], ...],
) -> RobustnessResult:
    valid_cases = 0
    invalid_cases = 0
    t2_total = 0.0
    t3_total = 0.0
    t4_total = 0.0
    fail4_total = 0.0
    min_t4 = 1.0
    max_t4 = 0.0
    sample_case = None
    sample_best2 = None
    sample_best3 = None
    sample_best4 = None

    for enemy_hp, player_hp, damage_sequence in product(enemy_hps, player_hps, damage_sequences):
        case = evaluate_case(family, enemy_hp, player_hp, damage_sequence)
        best2 = case.best2
        best3 = case.best3
        best4 = case.best4
        if case.stable or best2 is None or best3 is None or best4 is None:
            invalid_cases += 1
            continue

        valid_cases += 1
        t2_total += best2[1][1]
        t3_total += best3[1][2]
        t4_total += best4[1][3]
        fail4_total += best4[1][-1]
        min_t4 = min(min_t4, best4[1][3])
        max_t4 = max(max_t4, best4[1][3])

        if sample_case is None:
            sample_case = (enemy_hp, player_hp, damage_sequence)
            sample_best2 = best2
            sample_best3 = best3
            sample_best4 = best4

    if valid_cases == 0:
        return RobustnessResult(
            family=family,
            valid_cases=0,
            invalid_cases=invalid_cases,
            avg_t2=0.0,
            avg_t3=0.0,
            avg_t4=0.0,
            avg_fail4=1.0,
            min_t4=0.0,
            max_t4=0.0,
            sample_case=None,
            sample_best2=None,
            sample_best3=None,
            sample_best4=None,
        )

    return RobustnessResult(
        family=family,
        valid_cases=valid_cases,
        invalid_cases=invalid_cases,
        avg_t2=t2_total / valid_cases,
        avg_t3=t3_total / valid_cases,
        avg_t4=t4_total / valid_cases,
        avg_fail4=fail4_total / valid_cases,
        min_t4=min_t4,
        max_t4=max_t4,
        sample_case=sample_case,
        sample_best2=sample_best2,
        sample_best3=sample_best3,
        sample_best4=sample_best4,
    )


def detailed_grid(
    family: Family,
    enemy_hps: tuple[int, ...],
    player_hps: tuple[int, ...],
    damage_sequences: tuple[tuple[int, ...], ...],
) -> dict[str, object]:
    details: list[dict[str, object]] = []
    for enemy_hp, player_hp, damage_sequence in product(enemy_hps, player_hps, damage_sequences):
        case = evaluate_case(family, enemy_hp, player_hp, damage_sequence)
        details.append(
            {
                "enemy_hp": enemy_hp,
                "player_hp": player_hp,
                "damage_sequence": list(damage_sequence),
                "stable": case.stable,
                "has_234_gradient": case.best2 is not None and case.best3 is not None and case.best4 is not None,
                "best2": None if case.best2 is None else {"deck": list(case.best2[0]), "result": list(case.best2[1])},
                "best3": None if case.best3 is None else {"deck": list(case.best3[0]), "result": list(case.best3[1])},
                "best4": None if case.best4 is None else {"deck": list(case.best4[0]), "result": list(case.best4[1])},
            }
        )
    return {
        "family": family.name,
        "card_pool": list(family.card_pool),
        "enemy_hps": list(enemy_hps),
        "player_hps": list(player_hps),
        "damage_sequences": [list(seq) for seq in damage_sequences],
        "details": details,
    }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Robustness scan for candidate families.")
    parser.add_argument("--family-name")
    parser.add_argument("--card-pool")
    parser.add_argument("--enemy-hps", nargs="+", type=int, default=[49, 50, 51, 52])
    parser.add_argument("--player-hps", nargs="+", type=int, default=[8, 9])
    parser.add_argument("--damage-seq", action="append", dest="damage_sequences")
    parser.add_argument("--detail-json", help="Write detailed per-case grid to JSON for a single custom family")
    return parser


def main() -> None:
    args = build_parser().parse_args()
    enemy_hps = tuple(args.enemy_hps)
    player_hps = tuple(args.player_hps)
    damage_sequence_texts = args.damage_sequences or ["0,9,11,13", "0,9,10,12", "0,8,10,12", "0,8,11,13", "0,9,12,14"]
    damage_sequences = tuple(parse_damage_sequence(text) for text in damage_sequence_texts)

    if args.card_pool:
        family = Family(
            name=args.family_name or "custom_family",
            card_pool=parse_cards(args.card_pool),
        )
        result = evaluate_family(family, enemy_hps, player_hps, damage_sequences)
        print(family.name)
        print(
            {
                "valid_cases": result.valid_cases,
                "invalid_cases": result.invalid_cases,
                "avg_t2": round(result.avg_t2, 6),
                "avg_t3": round(result.avg_t3, 6),
                "avg_t4": round(result.avg_t4, 6),
                "avg_fail4": round(result.avg_fail4, 6),
                "min_t4": round(result.min_t4, 6),
                "max_t4": round(result.max_t4, 6),
                "sample_case": result.sample_case,
                "sample_best2": result.sample_best2,
                "sample_best3": result.sample_best3,
                "sample_best4": result.sample_best4,
            }
        )
        if args.detail_json:
            payload = detailed_grid(family, enemy_hps, player_hps, damage_sequences)
            Path(args.detail_json).write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
            print(Path(args.detail_json).resolve())
        return

    results = [evaluate_family(family, enemy_hps, player_hps, damage_sequences) for family in FAMILIES]
    results.sort(
        key=lambda item: (
            item.valid_cases,
            item.avg_t4,
            -item.avg_fail4,
            item.min_t4,
            -abs(item.avg_t2 - 0.4),
        ),
        reverse=True,
    )

    for item in results:
        print(item.family.name)
        print(
            {
                "valid_cases": item.valid_cases,
                "invalid_cases": item.invalid_cases,
                "avg_t2": round(item.avg_t2, 6),
                "avg_t3": round(item.avg_t3, 6),
                "avg_t4": round(item.avg_t4, 6),
                "avg_fail4": round(item.avg_fail4, 6),
                "min_t4": round(item.min_t4, 6),
                "max_t4": round(item.max_t4, 6),
                "sample_case": item.sample_case,
                "sample_best2": item.sample_best2,
                "sample_best3": item.sample_best3,
                "sample_best4": item.sample_best4,
            }
        )
        print()


if __name__ == "__main__":
    main()
