from __future__ import annotations

import argparse
from dataclasses import dataclass
from itertools import product

from difficulty1_multiturn_search import SearchVariant, unique_decks, exact_multiturn_result


CARD_POOLS: dict[str, tuple[str, ...]] = {
    "six_strike_def3_perfected": (
        "Strike",
        "Strike",
        "Strike",
        "Strike",
        "Strike",
        "Strike",
        "Defend",
        "Defend",
        "Defend",
        "Perfected Strike",
    ),
    "five_strike_def3_bash": (
        "Strike",
        "Strike",
        "Strike",
        "Strike",
        "Strike",
        "Perfected Strike",
        "Bash",
        "Defend",
        "Defend",
        "Defend",
    ),
    "four_strike_bash": (
        "Strike",
        "Strike",
        "Strike",
        "Strike",
        "Perfected Strike",
        "Bash",
        "Defend",
        "Defend",
        "Defend",
        "Defend",
    ),
    "four_strike_upper": (
        "Strike",
        "Strike",
        "Strike",
        "Strike",
        "Perfected Strike",
        "Uppercut",
        "Defend",
        "Defend",
        "Defend",
        "Defend",
    ),
    "six_strike_def2_perfected_clothesline": (
        "Strike",
        "Strike",
        "Strike",
        "Strike",
        "Strike",
        "Strike",
        "Defend",
        "Defend",
        "Perfected Strike",
        "Clothesline",
    ),
    "five_strike_def2_perfected_upper_twin": (
        "Strike",
        "Strike",
        "Strike",
        "Strike",
        "Strike",
        "Defend",
        "Defend",
        "Perfected Strike",
        "Uppercut",
        "Twin Strike",
    ),
    "mixed": (
        "Strike",
        "Strike",
        "Strike",
        "Strike",
        "Perfected Strike",
        "Bash",
        "Uppercut",
        "Defend",
        "Defend",
        "Defend",
    ),
    "mixed_ironwave": (
        "Strike",
        "Strike",
        "Strike",
        "Perfected Strike",
        "Bash",
        "Uppercut",
        "Iron Wave",
        "Defend",
        "Defend",
        "Defend",
    ),
    "four_strike_bash_ironwave": (
        "Strike",
        "Strike",
        "Strike",
        "Strike",
        "Perfected Strike",
        "Bash",
        "Iron Wave",
        "Defend",
        "Defend",
        "Defend",
    ),
}


@dataclass
class RankedCandidate:
    score: float
    variant: SearchVariant
    stable_count: int
    best2: tuple[str, ...] | None
    best2_result: tuple[float, ...] | None
    best3: tuple[str, ...] | None
    best3_result: tuple[float, ...] | None
    best4: tuple[str, ...] | None
    best4_result: tuple[float, ...] | None


def first_success_turn(result: tuple[float, ...]) -> int | None:
    for idx, prob in enumerate(result[:-1], start=1):
        if prob > 1e-9:
            return idx
    return None


def choose_best(rows: list[tuple[tuple[str, ...], tuple[float, ...]]], target_turn: int) -> tuple[tuple[str, ...] | None, tuple[float, ...] | None]:
    subset = [(deck, result) for deck, result in rows if first_success_turn(result) == target_turn]
    if not subset:
        return None, None
    subset.sort(key=lambda item: item[1][target_turn - 1], reverse=True)
    return subset[0]


def candidate_score(
    stable_count: int,
    best2_result: tuple[float, ...] | None,
    best3_result: tuple[float, ...] | None,
    best4_result: tuple[float, ...] | None,
) -> float:
    if stable_count > 0:
        return -1000.0 - stable_count * 50
    if best2_result is None or best3_result is None or best4_result is None:
        return -500.0

    t2 = best2_result[1]
    t3 = best3_result[2]
    t4 = best4_result[3]
    fail2 = best2_result[-1]
    fail3 = best3_result[-1]
    fail4 = best4_result[-1]

    score = 0.0
    score += 100.0
    score -= abs(t2 - 0.35) * 120
    score -= abs(t3 - 0.80) * 100
    score -= abs(t4 - 0.80) * 100
    score -= max(0.0, 0.02 - fail2) * 200
    score -= max(0.0, 0.02 - fail3) * 200
    score -= max(0.0, 0.02 - fail4) * 200
    return score


def parse_damage_sequence(text: str) -> tuple[int, ...]:
    return tuple(int(part) for part in text.split(","))


def rank_candidates(
    pool_names: list[str] | None = None,
    enemy_hps: list[int] | None = None,
    player_hps: list[int] | None = None,
    damage_sequences: list[tuple[int, ...]] | None = None,
) -> list[RankedCandidate]:
    selected_pool_names = pool_names or list(CARD_POOLS.keys())
    selected_enemy_hps = enemy_hps or [48, 49, 50, 51, 52, 53, 54, 55, 56]
    selected_player_hps = player_hps or [8, 9, 10]
    selected_damage_sequences = damage_sequences or [(0, 9, 11, 13), (0, 8, 10, 12), (0, 8, 9, 11)]

    variants: list[SearchVariant] = []
    for pool_name in selected_pool_names:
        pool = CARD_POOLS[pool_name]
        for enemy_hp, player_hp, dmg_seq in product(
            selected_enemy_hps,
            selected_player_hps,
            selected_damage_sequences,
        ):
            variants.append(
                SearchVariant(
                    name=f"{pool_name}_hp{enemy_hp}_php{player_hp}_dmg{'-'.join(str(x) for x in dmg_seq)}",
                    card_pool=pool,
                    enemy_hp=enemy_hp,
                    player_hp=player_hp,
                    enemy_damage_by_turn=dmg_seq,
                )
            )

    ranked: list[RankedCandidate] = []
    for variant in variants:
        rows: list[tuple[tuple[str, ...], tuple[float, ...]]] = []
        stable_count = 0
        for deck in unique_decks(variant.card_pool, variant.min_deck_size, variant.max_deck_size):
            result = exact_multiturn_result(deck, variant)
            rows.append((deck, result))
            if result[-1] < 1e-12:
                stable_count += 1

        best2, best2_result = choose_best(rows, 2)
        best3, best3_result = choose_best(rows, 3)
        best4, best4_result = choose_best(rows, 4)
        score = candidate_score(stable_count, best2_result, best3_result, best4_result)

        ranked.append(
            RankedCandidate(
                score=score,
                variant=variant,
                stable_count=stable_count,
                best2=best2,
                best2_result=best2_result,
                best3=best3,
                best3_result=best3_result,
                best4=best4,
                best4_result=best4_result,
            )
        )

    ranked.sort(key=lambda item: item.score, reverse=True)
    return ranked


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Rank puzzle candidates with exact multi-turn search.")
    parser.add_argument("--pool", action="append", choices=sorted(CARD_POOLS.keys()), help="Restrict search to one or more named card pools.")
    parser.add_argument("--enemy-hp", action="append", type=int, dest="enemy_hps", help="Restrict search to one or more enemy HP values.")
    parser.add_argument("--player-hp", action="append", type=int, dest="player_hps", help="Restrict search to one or more player HP values.")
    parser.add_argument(
        "--damage-seq",
        action="append",
        dest="damage_sequences",
        help="Restrict search to one or more comma-separated enemy damage sequences, for example 0,9,11,13.",
    )
    parser.add_argument("--top", type=int, default=20, help="How many ranked candidates to print.")
    return parser


def main() -> None:
    args = build_parser().parse_args()
    damage_sequences = None if args.damage_sequences is None else [parse_damage_sequence(text) for text in args.damage_sequences]
    ranked = rank_candidates(
        pool_names=args.pool,
        enemy_hps=args.enemy_hps,
        player_hps=args.player_hps,
        damage_sequences=damage_sequences,
    )
    for item in ranked[: args.top]:
        print(item.score)
        print(item.variant)
        print("stable", item.stable_count)
        print("best2", item.best2, item.best2_result)
        print("best3", item.best3, item.best3_result)
        print("best4", item.best4, item.best4_result)
        print()


if __name__ == "__main__":
    main()
