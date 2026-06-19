from __future__ import annotations

import argparse
from dataclasses import dataclass

from difficulty1_multiturn_search import SearchVariant, exact_multiturn_result, unique_decks


@dataclass
class Candidate:
    score_key: tuple[float, float, float, float, int]
    total_cards: int
    counts: dict[str, int]
    case_count: int
    best2: tuple[tuple[str, ...], tuple[float, ...]]
    best3: tuple[tuple[str, ...], tuple[float, ...]]
    best4: tuple[tuple[str, ...], tuple[float, ...]]


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


def card_pool_from_counts(counts: dict[str, int]) -> tuple[str, ...]:
    pool: list[str] = []
    for card in ("Anger", "Battle Trance", "Bloodletting", "Body Slam", "Clash", "Rage", "Strike", "Defend", "Perfected Strike", "Bash", "Thunderclap", "Uppercut", "Headbutt", "Twin Strike", "Pommel Strike", "Shrug It Off", "Sword Boomerang", "Bludgeon", "Hemokinesis"):
        pool.extend([card] * counts[card])
    return tuple(pool)


def parse_damage_sequence(text: str) -> tuple[int, ...]:
    return tuple(int(part.strip()) for part in text.split(","))


def scan_upper_bound(
    totals: tuple[int, ...],
    max_specials: int,
    max_anger: int,
    max_perfected: int,
    max_auxiliary: int,
    max_bash: int,
    max_uppercut: int,
    max_headbutt: int,
    max_battle_trance: int,
    max_bloodletting: int,
    max_body_slam: int,
    max_rage: int,
    max_thunderclap: int,
    max_twin_strike: int,
    max_pommel_strike: int,
    max_shrug_it_off: int,
    max_sword_boomerang: int,
    max_bludgeon: int,
    max_hemokinesis: int,
    max_clash: int,
    enemy_hp: int,
    player_hp: int,
    damage_sequence: tuple[int, ...],
) -> tuple[list[Candidate], dict[str, int]]:
    candidates: list[Candidate] = []
    stats = {
        "pool_vectors": 0,
        "stable_rejected": 0,
        "missing_gradient": 0,
        "accepted": 0,
    }

    for total_cards in totals:
        for strike in range(3, total_cards + 1):
            for defend in range(1, total_cards - strike + 1):
                for perfected in range(min(max_perfected, total_cards - strike - defend) + 1):
                    for anger in range(total_cards - strike - defend - perfected + 1):
                        for clash in range(total_cards - strike - defend - perfected - anger + 1):
                            for body_slam in range(total_cards - strike - defend - perfected - anger - clash + 1):
                                for rage in range(total_cards - strike - defend - perfected - anger - clash - body_slam + 1):
                                    for bash in range(total_cards - strike - defend - perfected - anger - clash - body_slam - rage + 1):
                                        for thunderclap in range(total_cards - strike - defend - perfected - anger - clash - body_slam - rage - bash + 1):
                                            for uppercut in range(total_cards - strike - defend - perfected - anger - clash - body_slam - rage - bash - thunderclap + 1):
                                                for headbutt in range(total_cards - strike - defend - perfected - anger - clash - body_slam - rage - bash - thunderclap - uppercut + 1):
                                                    for battle_trance in range(total_cards - strike - defend - perfected - anger - clash - body_slam - rage - bash - thunderclap - uppercut - headbutt + 1):
                                                        for bloodletting in range(total_cards - strike - defend - perfected - anger - clash - body_slam - rage - bash - thunderclap - uppercut - headbutt - battle_trance + 1):
                                                            for twin_strike in range(total_cards - strike - defend - perfected - anger - clash - body_slam - rage - bash - thunderclap - uppercut - headbutt - battle_trance - bloodletting + 1):
                                                                for pommel_strike in range(total_cards - strike - defend - perfected - anger - clash - body_slam - rage - bash - thunderclap - uppercut - headbutt - battle_trance - bloodletting - twin_strike + 1):
                                                                    for shrug_it_off in range(total_cards - strike - defend - perfected - anger - clash - body_slam - rage - bash - thunderclap - uppercut - headbutt - battle_trance - bloodletting - twin_strike - pommel_strike + 1):
                                                                        for sword_boomerang in range(total_cards - strike - defend - perfected - anger - clash - body_slam - rage - bash - thunderclap - uppercut - headbutt - battle_trance - bloodletting - twin_strike - pommel_strike - shrug_it_off + 1):
                                                                            for hemokinesis in range(total_cards - strike - defend - perfected - anger - clash - body_slam - rage - bash - thunderclap - uppercut - headbutt - battle_trance - bloodletting - twin_strike - pommel_strike - shrug_it_off - sword_boomerang + 1):
                                                                                bludgeon = (
                                                                                    total_cards
                                                                                    - strike
                                                                                    - defend
                                                                                    - perfected
                                                                                    - anger
                                                                                    - clash
                                                                                    - body_slam
                                                                                    - rage
                                                                                    - bash
                                                                                    - thunderclap
                                                                                    - uppercut
                                                                                    - headbutt
                                                                                    - battle_trance
                                                                                    - bloodletting
                                                                                    - twin_strike
                                                                                    - pommel_strike
                                                                                    - shrug_it_off
                                                                                    - sword_boomerang
                                                                                    - hemokinesis
                                                                                )
                                                                                if bludgeon < 0:
                                                                                    continue
                                                                                counts = {
                                                                                    "Clash": clash,
                                                                                    "Anger": anger,
                                                                            "Body Slam": body_slam,
                                                                            "Rage": rage,
                                                                            "Strike": strike,
                                                                            "Defend": defend,
                                                                            "Perfected Strike": perfected,
                                                                            "Bash": bash,
                                                                            "Thunderclap": thunderclap,
                                                                            "Uppercut": uppercut,
                                                                            "Headbutt": headbutt,
                                                                            "Battle Trance": battle_trance,
                                                                            "Bloodletting": bloodletting,
                                                                            "Twin Strike": twin_strike,
                                                                            "Pommel Strike": pommel_strike,
                                                                            "Shrug It Off": shrug_it_off,
                                                                            "Sword Boomerang": sword_boomerang,
                                                                            "Bludgeon": bludgeon,
                                                                            "Hemokinesis": hemokinesis,
                                                                        }
                                                                        special_total = anger + clash + body_slam + rage + perfected + bash + thunderclap + uppercut + headbutt + battle_trance + bloodletting + twin_strike + pommel_strike + shrug_it_off + sword_boomerang + bludgeon + hemokinesis
                                                                        auxiliary_total = anger + clash + body_slam + rage + bash + thunderclap + uppercut + headbutt + battle_trance + bloodletting + twin_strike + pommel_strike + shrug_it_off + sword_boomerang + bludgeon + hemokinesis
                                                                        if anger > max_anger:
                                                                            continue
                                                                        if special_total > max_specials:
                                                                            continue
                                                                        if auxiliary_total > max_auxiliary:
                                                                            continue
                                                                        if bash > max_bash:
                                                                            continue
                                                                        if uppercut > max_uppercut:
                                                                            continue
                                                                        if headbutt > max_headbutt:
                                                                            continue
                                                                        if battle_trance > max_battle_trance:
                                                                            continue
                                                                        if bloodletting > max_bloodletting:
                                                                            continue
                                                                        if body_slam > max_body_slam:
                                                                            continue
                                                                        if rage > max_rage:
                                                                            continue
                                                                        if thunderclap > max_thunderclap:
                                                                            continue
                                                                        if twin_strike > max_twin_strike:
                                                                            continue
                                                                        if pommel_strike > max_pommel_strike:
                                                                            continue
                                                                        if shrug_it_off > max_shrug_it_off:
                                                                            continue
                                                                        if sword_boomerang > max_sword_boomerang:
                                                                            continue
                                                                        if bludgeon > max_bludgeon:
                                                                            continue
                                                                        if hemokinesis > max_hemokinesis:
                                                                            continue
                                                                        if clash > max_clash:
                                                                            continue
                                                                        stats["pool_vectors"] += 1

                                                                        variant = SearchVariant(
                                                                            name="basic_red_upper_bound",
                                                                            card_pool=card_pool_from_counts(counts),
                                                                            enemy_hp=enemy_hp,
                                                                            player_hp=player_hp,
                                                                            enemy_damage_by_turn=damage_sequence,
                                                                        )
                                                                        decks = unique_decks(variant.card_pool, variant.min_deck_size, variant.max_deck_size)
                                                                        rows: list[tuple[tuple[str, ...], tuple[float, ...]]] = []
                                                                        stable = False
                                                                        for deck in decks:
                                                                            result = exact_multiturn_result(deck, variant)
                                                                            rows.append((deck, result))
                                                                            if result[-1] < 1e-12:
                                                                                stable = True
                                                                        if stable:
                                                                            stats["stable_rejected"] += 1
                                                                            continue

                                                                        best2 = choose_best(rows, 2)
                                                                        best3 = choose_best(rows, 3)
                                                                        best4 = choose_best(rows, 4)
                                                                        if best2 is None or best3 is None or best4 is None:
                                                                            stats["missing_gradient"] += 1
                                                                            continue

                                                                        score_key = (
                                                                            best4[1][3],
                                                                            -best4[1][-1],
                                                                            best3[1][2],
                                                                            -abs(best2[1][1] - 0.4),
                                                                            -len(decks),
                                                                        )
                                                                        candidates.append(
                                                                            Candidate(
                                                                                score_key=score_key,
                                                                                total_cards=total_cards,
                                                                                counts=counts,
                                                                                case_count=len(decks),
                                                                                best2=best2,
                                                                                best3=best3,
                                                                                best4=best4,
                                                                            )
                                                                        )
                                                                        stats["accepted"] += 1

    candidates.sort(key=lambda item: item.score_key, reverse=True)
    return candidates, stats


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Upper-bound scan for low-noise basic red card families.")
    parser.add_argument("--totals", nargs="+", type=int, default=[9, 10, 11])
    parser.add_argument("--max-specials", type=int, default=4)
    parser.add_argument("--max-anger", type=int, default=99)
    parser.add_argument("--max-perfected", type=int, default=2)
    parser.add_argument("--max-auxiliary", type=int, default=1, help="Max count among Anger/Body Slam/Rage/Bash/Thunderclap/Uppercut/Headbutt/Battle Trance/Bloodletting/Twin/Pommel/Shrug It Off/Sword Boomerang/Bludgeon/Hemokinesis combined.")
    parser.add_argument("--max-bash", type=int, default=99)
    parser.add_argument("--max-thunderclap", type=int, default=99)
    parser.add_argument("--max-uppercut", type=int, default=99)
    parser.add_argument("--max-headbutt", type=int, default=99)
    parser.add_argument("--max-battle-trance", type=int, default=99)
    parser.add_argument("--max-bloodletting", type=int, default=99)
    parser.add_argument("--max-body-slam", type=int, default=99)
    parser.add_argument("--max-rage", type=int, default=99)
    parser.add_argument("--max-twin-strike", type=int, default=99)
    parser.add_argument("--max-pommel-strike", type=int, default=99)
    parser.add_argument("--max-shrug-it-off", type=int, default=99)
    parser.add_argument("--max-sword-boomerang", type=int, default=99)
    parser.add_argument("--max-bludgeon", type=int, default=99)
    parser.add_argument("--max-hemokinesis", type=int, default=99)
    parser.add_argument("--max-clash", type=int, default=99)
    parser.add_argument("--enemy-hp", type=int, default=50)
    parser.add_argument("--player-hp", type=int, default=8)
    parser.add_argument("--damage-seq", default="0,9,11,13")
    parser.add_argument("--top", type=int, default=20)
    return parser


def main() -> None:
    args = build_parser().parse_args()
    candidates, stats = scan_upper_bound(
        totals=tuple(args.totals),
        max_specials=args.max_specials,
        max_anger=args.max_anger,
        max_perfected=args.max_perfected,
        max_auxiliary=args.max_auxiliary,
        max_bash=args.max_bash,
        max_uppercut=args.max_uppercut,
        max_headbutt=args.max_headbutt,
        max_battle_trance=args.max_battle_trance,
        max_bloodletting=args.max_bloodletting,
        max_body_slam=args.max_body_slam,
        max_rage=args.max_rage,
        max_thunderclap=args.max_thunderclap,
        max_twin_strike=args.max_twin_strike,
        max_pommel_strike=args.max_pommel_strike,
        max_shrug_it_off=args.max_shrug_it_off,
        max_sword_boomerang=args.max_sword_boomerang,
        max_bludgeon=args.max_bludgeon,
        max_hemokinesis=args.max_hemokinesis,
        max_clash=args.max_clash,
        enemy_hp=args.enemy_hp,
        player_hp=args.player_hp,
        damage_sequence=parse_damage_sequence(args.damage_seq),
    )
    print("stats", stats)
    for item in candidates[: args.top]:
        print(
            {
                "score_key": tuple(round(x, 6) for x in item.score_key),
                "total_cards": item.total_cards,
                "counts": item.counts,
                "case_count": item.case_count,
                "best2": item.best2,
                "best3": item.best3,
                "best4": item.best4,
            }
        )
        print()


if __name__ == "__main__":
    main()
