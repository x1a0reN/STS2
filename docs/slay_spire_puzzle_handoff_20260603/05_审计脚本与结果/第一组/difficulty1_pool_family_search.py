from __future__ import annotations

import argparse
from dataclasses import dataclass
from itertools import product

from difficulty1_candidate_ranker import candidate_score, first_success_turn
from difficulty1_multiturn_search import SearchVariant, exact_multiturn_result, unique_decks


@dataclass
class FamilyCandidate:
    score: float
    variant: SearchVariant
    stable_count: int
    best2_deck: tuple[str, ...] | None
    best2_result: tuple[float, ...] | None
    best3_deck: tuple[str, ...] | None
    best3_result: tuple[float, ...] | None
    best4_deck: tuple[str, ...] | None
    best4_result: tuple[float, ...] | None


def choose_best(
    rows: list[tuple[tuple[str, ...], tuple[float, ...]]],
    target_turn: int,
) -> tuple[tuple[str, ...] | None, tuple[float, ...] | None]:
    subset = [(deck, result) for deck, result in rows if first_success_turn(result) == target_turn]
    if not subset:
        return None, None
    subset.sort(key=lambda item: item[1][target_turn - 1], reverse=True)
    return subset[0]


def build_pool_from_counts(counts: dict[str, int]) -> tuple[str, ...]:
    cards: list[str] = []
    for card in ("Strike", "Twin Strike", "Pommel Strike", "Defend", "Perfected Strike", "Bash", "Uppercut", "Clothesline", "Iron Wave"):
        cards.extend([card] * counts.get(card, 0))
    return tuple(cards)


def iter_count_vectors(
    total_cards: int,
    include_iron_wave: bool,
    include_clothesline: bool,
    include_twin_strike: bool,
    include_pommel_strike: bool,
) -> list[dict[str, int]]:
    vectors: list[dict[str, int]] = []
    upper_wave = total_cards if include_iron_wave else 0
    upper_clothesline = total_cards if include_clothesline else 0
    upper_twin = total_cards if include_twin_strike else 0
    upper_pommel = total_cards if include_pommel_strike else 0
    for strike in range(total_cards + 1):
        for twin_strike in range(total_cards - strike + 1):
            for pommel_strike in range(total_cards - strike - twin_strike + 1):
                for defend in range(total_cards - strike - twin_strike - pommel_strike + 1):
                    for perfected in range(total_cards - strike - twin_strike - pommel_strike - defend + 1):
                        for bash in range(total_cards - strike - twin_strike - pommel_strike - defend - perfected + 1):
                            for uppercut in range(total_cards - strike - twin_strike - pommel_strike - defend - perfected - bash + 1):
                                for clothesline in range(total_cards - strike - twin_strike - pommel_strike - defend - perfected - bash - uppercut + 1):
                                    iron_wave = total_cards - strike - twin_strike - pommel_strike - defend - perfected - bash - uppercut - clothesline
                                    if twin_strike < 0 or twin_strike > upper_twin:
                                        continue
                                    if pommel_strike < 0 or pommel_strike > upper_pommel:
                                        continue
                                    if clothesline < 0 or clothesline > upper_clothesline:
                                        continue
                                    if iron_wave < 0 or iron_wave > upper_wave:
                                        continue
                                    vectors.append(
                                        {
                                            "Strike": strike,
                                            "Twin Strike": twin_strike,
                                            "Pommel Strike": pommel_strike,
                                            "Defend": defend,
                                            "Perfected Strike": perfected,
                                            "Bash": bash,
                                            "Uppercut": uppercut,
                                            "Clothesline": clothesline,
                                            "Iron Wave": iron_wave,
                                        }
                                    )
    return vectors


def passes_filters(
    counts: dict[str, int],
    min_strikes: int,
    min_defends: int,
    max_specials: int,
) -> bool:
    if counts["Strike"] < min_strikes:
        return False
    if counts["Defend"] < min_defends:
        return False
    special_total = (
        counts["Twin Strike"]
        + counts["Pommel Strike"]
        + counts["Perfected Strike"]
        + counts["Bash"]
        + counts["Uppercut"]
        + counts["Clothesline"]
        + counts["Iron Wave"]
    )
    if special_total > max_specials:
        return False
    return True


def family_name(counts: dict[str, int]) -> str:
    parts: list[str] = []
    for card in ("Strike", "Twin Strike", "Pommel Strike", "Defend", "Perfected Strike", "Bash", "Uppercut", "Clothesline", "Iron Wave"):
        count = counts[card]
        if count:
            parts.append(f"{card}x{count}")
    return "_".join(parts)


def search_family(
    total_cards: int,
    include_iron_wave: bool,
    include_clothesline: bool,
    include_twin_strike: bool,
    include_pommel_strike: bool,
    min_strikes: int,
    min_defends: int,
    max_specials: int,
    enemy_hps: list[int],
    player_hps: list[int],
    damage_sequences: list[tuple[int, ...]],
) -> list[FamilyCandidate]:
    candidates: list[FamilyCandidate] = []
    count_vectors = iter_count_vectors(
        total_cards,
        include_iron_wave,
        include_clothesline,
        include_twin_strike,
        include_pommel_strike,
    )

    for counts in count_vectors:
        if not passes_filters(counts, min_strikes, min_defends, max_specials):
            continue

        pool = build_pool_from_counts(counts)
        pool_label = family_name(counts)

        for enemy_hp, player_hp, damage_sequence in product(enemy_hps, player_hps, damage_sequences):
            variant = SearchVariant(
                name=f"{pool_label}_hp{enemy_hp}_php{player_hp}_dmg{'-'.join(str(x) for x in damage_sequence)}",
                card_pool=pool,
                enemy_hp=enemy_hp,
                player_hp=player_hp,
                enemy_damage_by_turn=damage_sequence,
            )

            rows: list[tuple[tuple[str, ...], tuple[float, ...]]] = []
            stable_count = 0
            for deck in unique_decks(variant.card_pool, variant.min_deck_size, variant.max_deck_size):
                result = exact_multiturn_result(deck, variant)
                rows.append((deck, result))
                if result[-1] < 1e-12:
                    stable_count += 1

            best2_deck, best2_result = choose_best(rows, 2)
            best3_deck, best3_result = choose_best(rows, 3)
            best4_deck, best4_result = choose_best(rows, 4)
            score = candidate_score(stable_count, best2_result, best3_result, best4_result)

            candidates.append(
                FamilyCandidate(
                    score=score,
                    variant=variant,
                    stable_count=stable_count,
                    best2_deck=best2_deck,
                    best2_result=best2_result,
                    best3_deck=best3_deck,
                    best3_result=best3_result,
                    best4_deck=best4_deck,
                    best4_result=best4_result,
                )
            )

    candidates.sort(key=lambda item: item.score, reverse=True)
    return candidates


def parse_damage_sequence(text: str) -> tuple[int, ...]:
    return tuple(int(part) for part in text.split(","))


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Search over card-pool families with exact multi-turn evaluation.")
    parser.add_argument("--total-cards", type=int, default=10)
    parser.add_argument("--include-iron-wave", action="store_true")
    parser.add_argument("--include-clothesline", action="store_true")
    parser.add_argument("--include-twin-strike", action="store_true")
    parser.add_argument("--include-pommel-strike", action="store_true")
    parser.add_argument("--min-strikes", type=int, default=3)
    parser.add_argument("--min-defends", type=int, default=1)
    parser.add_argument("--max-specials", type=int, default=4)
    parser.add_argument("--enemy-hp", action="append", type=int, dest="enemy_hps")
    parser.add_argument("--player-hp", action="append", type=int, dest="player_hps")
    parser.add_argument("--damage-seq", action="append", dest="damage_sequences")
    parser.add_argument("--top", type=int, default=20)
    return parser


def main() -> None:
    args = build_parser().parse_args()
    candidates = search_family(
        total_cards=args.total_cards,
        include_iron_wave=args.include_iron_wave,
        include_clothesline=args.include_clothesline,
        include_twin_strike=args.include_twin_strike,
        include_pommel_strike=args.include_pommel_strike,
        min_strikes=args.min_strikes,
        min_defends=args.min_defends,
        max_specials=args.max_specials,
        enemy_hps=args.enemy_hps or [49, 50, 51, 52],
        player_hps=args.player_hps or [8],
        damage_sequences=[parse_damage_sequence(text) for text in (args.damage_sequences or ["0,9,11,13"])],
    )

    for item in candidates[: args.top]:
        print(item.score)
        print(item.variant)
        print("stable", item.stable_count)
        print("best2", item.best2_deck, item.best2_result)
        print("best3", item.best3_deck, item.best3_result)
        print("best4", item.best4_deck, item.best4_result)
        print()


if __name__ == "__main__":
    main()
