#!/usr/bin/env python3
"""
Exact state enumerator for difficulty 1: 石化狂信徒的第一课.

This script is the source of truth for probability text in
docs/difficulty1_final_puzzle.md. It enumerates real random draw states:
opening hand, later natural draws, shuffle from discard, and Shrug It Off's
extra draw. It does not assume a fixed card order.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import itertools
import json
import os
import sys
from dataclasses import dataclass
from functools import lru_cache
from math import comb
from pathlib import Path
from typing import Iterable


GAME_DIR = Path(r"D:\Steam\steamapps\common\Slay the Spire 2")
DLL_PATH = GAME_DIR / "data_sts2_windows_x86_64" / "sts2.dll"
PCK_PATH = GAME_DIR / "SlayTheSpire2.pck"

CARDS = (
    "Strike",
    "PerfectedStrike",
    "Bash",
    "IronWave",
    "ShrugItOff",
    "Defend",
    "BodySlam",
    "Uppercut",
)
CARD_INDEX = {card: index for index, card in enumerate(CARDS)}
CARD_NAMES_ZH = {
    "Strike": "打击",
    "PerfectedStrike": "完美打击",
    "Bash": "重击",
    "IronWave": "铁斩波",
    "ShrugItOff": "耸肩无视",
    "Defend": "防御",
    "BodySlam": "全身撞击",
    "Uppercut": "上勾拳",
}
OFFICIAL_LOCALIZATION_ZH = {
    "Strike": "打击",
    "PerfectedStrike": "完美打击",
    "Bash": "痛击",
    "IronWave": "铁斩波",
    "ShrugItOff": "耸肩无视",
    "Defend": "防御",
    "BodySlam": "全身撞击",
    "Uppercut": "上勾拳",
    "CalcifiedCultist": "钙化邪教徒",
}
CARD_COST = {
    "Strike": 1,
    "PerfectedStrike": 2,
    "Bash": 2,
    "IronWave": 1,
    "ShrugItOff": 1,
    "Defend": 1,
    "BodySlam": 1,
    "Uppercut": 2,
}
POOL_LIMITS = {
    "Strike": 5,
    "PerfectedStrike": 2,
    "Bash": 1,
    "IronWave": 1,
    "ShrugItOff": 1,
    "Defend": 1,
    "BodySlam": 1,
    "Uppercut": 1,
}

PLAYER_HP = 8
ENERGY_PER_TURN = 3
DRAW_PER_TURN = 5
ENEMY_HP = 55
ENEMY_DAMAGE = (0, 9, 11, 13)
MAX_TURNS = 4
SELECTED_CARDS = 8

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")


@dataclass(frozen=True)
class Result:
    cards: tuple[int, ...]
    distribution: tuple[float, float, float, float, float]

    @property
    def success_rate(self) -> float:
        return sum(self.distribution[:-1])

    @property
    def fail_rate(self) -> float:
        return self.distribution[-1]

    @property
    def first_kill_turn(self) -> int | None:
        for index, value in enumerate(self.distribution[:-1], start=1):
            if value > 1e-12:
                return index
        return None


def file_contains(path: Path, needle: bytes, chunk_size: int = 1024 * 1024) -> bool:
    if not path.exists():
        return False
    overlap = max(0, len(needle) - 1)
    previous = b""
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(chunk_size)
            if not chunk:
                return False
            haystack = previous + chunk
            if needle in haystack:
                return True
            previous = haystack[-overlap:] if overlap else b""


def missing_needles(path: Path, checks: list[tuple[bytes, str]], chunk_size: int = 1024 * 1024) -> list[str]:
    if not path.exists():
        return [label for _, label in checks]

    pending = dict(checks)
    overlap = max((len(needle) - 1 for needle, _ in checks), default=0)
    previous = b""
    with path.open("rb") as handle:
        while pending:
            chunk = handle.read(chunk_size)
            if not chunk:
                break
            haystack = previous + chunk
            for needle in list(pending):
                if needle in haystack:
                    del pending[needle]
            previous = haystack[-overlap:] if overlap else b""
    return list(pending.values())


def verify_local_resources() -> list[str]:
    dll_checks = [
        (b"StrikeIronclad", "dll: StrikeIronclad"),
        (b"DefendIronclad", "dll: DefendIronclad"),
        (b"PerfectedStrike", "dll: PerfectedStrike"),
        (b"Bash", "dll: Bash"),
        (b"IronWave", "dll: IronWave"),
        (b"ShrugItOff", "dll: ShrugItOff"),
        (b"BodySlam", "dll: BodySlam"),
        (b"Uppercut", "dll: Uppercut"),
        (b"CalcifiedCultist", "dll: CalcifiedCultist"),
    ]
    pck_checks = [
        ('"STRIKE_IRONCLAD.title": "打击"'.encode("utf-8"), "pck: 打击"),
        ('"DEFEND_IRONCLAD.title": "防御"'.encode("utf-8"), "pck: 防御"),
        ('"PERFECTED_STRIKE.title": "完美打击"'.encode("utf-8"), "pck: 完美打击"),
        ('"BASH.title": "痛击"'.encode("utf-8"), "pck: 痛击"),
        ('"IRON_WAVE.title": "铁斩波"'.encode("utf-8"), "pck: 铁斩波"),
        ('"SHRUG_IT_OFF.title": "耸肩无视"'.encode("utf-8"), "pck: 耸肩无视"),
        ('"BODY_SLAM.title": "全身撞击"'.encode("utf-8"), "pck: 全身撞击"),
        ('"UPPERCUT.title": "上勾拳"'.encode("utf-8"), "pck: 上勾拳"),
        ('"CALCIFIED_CULTIST.name": "钙化邪教徒"'.encode("utf-8"), "pck: 钙化邪教徒"),
    ]
    return missing_needles(DLL_PATH, dll_checks) + missing_needles(PCK_PATH, pck_checks, chunk_size=8 * 1024 * 1024)


def vector_add(left: tuple[int, ...], right: tuple[int, ...]) -> tuple[int, ...]:
    return tuple(a + b for a, b in zip(left, right))


def vector_sub(left: tuple[int, ...], right: tuple[int, ...]) -> tuple[int, ...]:
    return tuple(a - b for a, b in zip(left, right))


def total(cards: tuple[int, ...]) -> int:
    return sum(cards)


def choose_counts(cards: tuple[int, ...], amount: int) -> list[tuple[tuple[int, ...], float]]:
    if amount < 0 or amount > total(cards):
        return []

    denominator = comb(total(cards), amount)
    current = [0] * len(cards)
    results: list[tuple[tuple[int, ...], float]] = []

    def walk(index: int, remaining: int) -> None:
        if index == len(cards):
            if remaining == 0:
                weight = 1
                for count, take in zip(cards, current):
                    weight *= comb(count, take)
                results.append((tuple(current), weight / denominator))
            return

        for take in range(min(cards[index], remaining) + 1):
            current[index] = take
            walk(index + 1, remaining - take)

    walk(0, amount)
    return results


def draw_cards(
    hand: tuple[int, ...],
    draw_pile: tuple[int, ...],
    discard_pile: tuple[int, ...],
    amount: int,
) -> list[tuple[float, tuple[int, ...], tuple[int, ...], tuple[int, ...]]]:
    if amount <= 0:
        return [(1.0, hand, draw_pile, discard_pile)]

    if total(draw_pile) >= amount:
        return [
            (probability, vector_add(hand, take), vector_sub(draw_pile, take), discard_pile)
            for take, probability in choose_counts(draw_pile, amount)
        ]

    hand_after_draw = vector_add(hand, draw_pile)
    needed = amount - total(draw_pile)
    empty = (0,) * len(CARDS)

    if total(discard_pile) == 0:
        return [(1.0, hand_after_draw, empty, discard_pile)]

    if total(discard_pile) <= needed:
        return [(1.0, vector_add(hand_after_draw, discard_pile), empty, empty)]

    return [
        (probability, vector_add(hand_after_draw, take), vector_sub(discard_pile, take), empty)
        for take, probability in choose_counts(discard_pile, needed)
    ]


def add_distribution(
    left: tuple[float, float, float, float, float],
    right: tuple[float, float, float, float, float],
    scale: float,
) -> tuple[float, float, float, float, float]:
    return tuple(a + scale * b for a, b in zip(left, right))  # type: ignore[return-value]


def better_distribution(
    left: tuple[float, float, float, float, float],
    right: tuple[float, float, float, float, float],
) -> tuple[float, float, float, float, float]:
    left_success = sum(left[:-1])
    right_success = sum(right[:-1])
    if abs(left_success - right_success) > 1e-12:
        return left if left_success > right_success else right
    return max(left, right)


def damage_after_vulnerable(base_damage: int, vulnerable: int) -> int:
    return int(base_damage * 1.5) if vulnerable > 0 else base_damage


def solve(deck: tuple[int, ...]) -> tuple[float, float, float, float, float]:
    strike_like_count = deck[CARD_INDEX["Strike"]] + deck[CARD_INDEX["PerfectedStrike"]]
    perfected_strike_damage = 6 + 2 * strike_like_count
    zero = (0.0, 0.0, 0.0, 0.0, 1.0)

    @lru_cache(maxsize=None)
    def start_turn(
        turn: int,
        draw_pile: tuple[int, ...],
        discard_pile: tuple[int, ...],
        player_hp: int,
        enemy_hp: int,
        vulnerable: int,
    ) -> tuple[float, float, float, float, float]:
        if enemy_hp <= 0:
            output = [0.0] * 5
            output[min(turn - 1, MAX_TURNS) - 1] = 1.0
            return tuple(output)  # type: ignore[return-value]
        if turn > MAX_TURNS or player_hp <= 0:
            return zero

        accumulated = (0.0, 0.0, 0.0, 0.0, 0.0)
        for probability, hand, new_draw, new_discard in draw_cards(
            (0,) * len(CARDS),
            draw_pile,
            discard_pile,
            DRAW_PER_TURN,
        ):
            branch = best_action(turn, hand, new_draw, new_discard, (0,) * len(CARDS), player_hp, enemy_hp, vulnerable, 0)
            accumulated = add_distribution(accumulated, branch, probability)
        return accumulated

    @lru_cache(maxsize=None)
    def best_action(
        turn: int,
        hand: tuple[int, ...],
        draw_pile: tuple[int, ...],
        discard_pile: tuple[int, ...],
        played_this_turn: tuple[int, ...],
        player_hp: int,
        enemy_hp: int,
        vulnerable: int,
        block: int,
    ) -> tuple[float, float, float, float, float]:
        if enemy_hp <= 0:
            output = [0.0] * 5
            output[turn - 1] = 1.0
            return tuple(output)  # type: ignore[return-value]

        damage = ENEMY_DAMAGE[turn - 1]
        player_hp_after_attack = player_hp - max(0, damage - block)
        if player_hp_after_attack <= 0:
            best = zero
        else:
            end_turn_discard = vector_add(discard_pile, vector_add(hand, played_this_turn))
            best = start_turn(
                turn + 1,
                draw_pile,
                end_turn_discard,
                player_hp_after_attack,
                enemy_hp,
                max(0, vulnerable - 1),
            )

        spent_energy = sum(CARD_COST[card] * count for card, count in zip(CARDS, played_this_turn))
        available_energy = ENERGY_PER_TURN - spent_energy

        for index, card in enumerate(CARDS):
            if hand[index] <= 0 or available_energy < CARD_COST[card]:
                continue

            new_hand = list(hand)
            new_hand[index] -= 1
            new_hand_tuple = tuple(new_hand)
            new_played = list(played_this_turn)
            new_played[index] += 1
            new_played_tuple = tuple(new_played)
            new_enemy_hp = enemy_hp
            new_vulnerable = vulnerable
            new_block = block

            if card == "Strike":
                new_enemy_hp -= damage_after_vulnerable(6, new_vulnerable)
            elif card == "PerfectedStrike":
                new_enemy_hp -= damage_after_vulnerable(perfected_strike_damage, new_vulnerable)
            elif card == "Bash":
                new_enemy_hp -= damage_after_vulnerable(8, new_vulnerable)
                new_vulnerable += 2
            elif card == "IronWave":
                new_block += 5
                new_enemy_hp -= damage_after_vulnerable(5, new_vulnerable)
            elif card == "ShrugItOff":
                new_block += 8
                accumulated = (0.0, 0.0, 0.0, 0.0, 0.0)
                for probability, drawn_hand, new_draw, new_discard in draw_cards(
                    new_hand_tuple,
                    draw_pile,
                    discard_pile,
                    1,
                ):
                    branch = best_action(
                        turn,
                        drawn_hand,
                        new_draw,
                        new_discard,
                        new_played_tuple,
                        player_hp,
                        new_enemy_hp,
                        new_vulnerable,
                        new_block,
                    )
                    accumulated = add_distribution(accumulated, branch, probability)
                best = better_distribution(best, accumulated)
                continue
            elif card == "Defend":
                new_block += 5
            elif card == "BodySlam":
                new_enemy_hp -= damage_after_vulnerable(new_block, new_vulnerable)
            elif card == "Uppercut":
                new_enemy_hp -= damage_after_vulnerable(13, new_vulnerable)
                new_vulnerable += 1

            branch = best_action(
                turn,
                new_hand_tuple,
                draw_pile,
                discard_pile,
                new_played_tuple,
                player_hp,
                new_enemy_hp,
                new_vulnerable,
                new_block,
            )
            best = better_distribution(best, branch)

        return best

    return start_turn(1, deck, (0,) * len(CARDS), PLAYER_HP, ENEMY_HP, 0)


def enumerate_decks() -> Iterable[tuple[int, ...]]:
    ranges = [range(POOL_LIMITS[card] + 1) for card in CARDS]
    for counts in itertools.product(*ranges):
        if total(counts) == SELECTED_CARDS:
            yield tuple(counts)


def format_cards(counts: tuple[int, ...], zh: bool = False) -> str:
    parts = []
    for card, count in zip(CARDS, counts):
        if count <= 0:
            continue
        name = CARD_NAMES_ZH[card] if zh else card
        parts.append(f"{name} x{count}" if count > 1 else name)
    return ", ".join(parts) if parts else "无"


def pct(value: float) -> str:
    return f"{value * 100:.4f}%"


def representative_line(counts: tuple[int, ...]) -> str | None:
    key = dict(zip(CARDS, counts))
    if key == {"Strike": 5, "PerfectedStrike": 2, "Bash": 1, "IronWave": 0, "ShrugItOff": 0, "Defend": 0, "BodySlam": 0, "Uppercut": 0}:
        return "快解：理想 2 回合。关键是前两回合打出重击+两张完美打击，剩余用打击补足。"
    if key == {"Strike": 3, "PerfectedStrike": 2, "Bash": 1, "IronWave": 1, "ShrugItOff": 0, "Defend": 0, "BodySlam": 1, "Uppercut": 0}:
        return "主解：理想 3 回合。铁斩波先存格挡，全身撞击把格挡转伤，重击和完美打击负责主输出。"
    if key == {"Strike": 5, "PerfectedStrike": 0, "Bash": 0, "IronWave": 1, "ShrugItOff": 1, "Defend": 1, "BodySlam": 0, "Uppercut": 0}:
        return "稳线：理想 4 回合。靠防御、铁斩波、耸肩无视活过 9/11/13，打击慢慢磨死。"
    return None


def solve_deck(deck: tuple[int, ...]) -> Result:
    return Result(cards=deck, distribution=solve(deck))


def run(output_json: Path | None, summary_only: bool = False) -> int:
    missing = verify_local_resources()
    if missing:
        print("资源校验失败：")
        for item in missing:
            print(f"- {item}")
        return 2

    decks = list(enumerate_decks())
    jobs = max(1, min(os.cpu_count() or 1, 8))
    if jobs == 1:
        results = [solve_deck(deck) for deck in decks]
    else:
        with concurrent.futures.ProcessPoolExecutor(max_workers=jobs) as executor:
            results = list(executor.map(solve_deck, decks))
    results.sort(key=lambda item: (item.first_kill_turn or 99, -item.success_rate, item.fail_rate, item.cards))
    stable = [item for item in results if item.success_rate >= 1.0 - 1e-12]
    viable = [item for item in results if item.success_rate > 1e-12]

    print("资源校验：OK")
    print("官方本地化校准：")
    for card, name in OFFICIAL_LOCALIZATION_ZH.items():
        print(f"- {card}: {name}")
    print("说明：题面显示名按 docs/difficulty1_final_puzzle.md；Bash/敌人显示名与官方中文不一致时，以题面为展示口径、以官方模型为实现口径。")
    print(f"case_count = {len(results)}")
    print(f"viable_count = {len(viable)}")
    print(f"stable_count = {len(stable)}")
    if not summary_only:
        print()
        print("所有可通组合：")
        for index, item in enumerate(viable, start=1):
            d = item.distribution
            print(
                f"{index:02d}. {format_cards(item.cards)} | "
                f"中文：{format_cards(item.cards, zh=True)} | "
                f"分布：T1 {pct(d[0])} / T2 {pct(d[1])} / T3 {pct(d[2])} / "
                f"T4 {pct(d[3])} / 失败 {pct(d[4])} | 总成功 {pct(item.success_rate)}"
            )

    if output_json is not None:
        output_json.parent.mkdir(parents=True, exist_ok=True)
        payload = {
            "puzzleDoc": "docs/difficulty1_final_puzzle.md",
            "caseCount": len(results),
            "viableCount": len(viable),
            "stableCount": len(stable),
            "officialLocalizationZh": OFFICIAL_LOCALIZATION_ZH,
            "results": [
                {
                    "cards": dict(zip(CARDS, item.cards)),
                    "cardsText": format_cards(item.cards),
                    "cardsTextZh": format_cards(item.cards, zh=True),
                    "distribution": {
                        "turn1": item.distribution[0],
                        "turn2": item.distribution[1],
                        "turn3": item.distribution[2],
                        "turn4": item.distribution[3],
                        "fail": item.distribution[4],
                    },
                    "successRate": item.success_rate,
                    "failRate": item.fail_rate,
                    "firstKillTurn": item.first_kill_turn,
                    "representativeSuccessLine": representative_line(item.cards),
                }
                for item in results
            ],
        }
        output_json.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
        print()
        print(f"JSON written: {output_json}")

    return 0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--json", type=Path, help="Optional path for full JSON output.")
    parser.add_argument("--summary-only", action="store_true", help="Print only aggregate counts; JSON still contains all results.")
    args = parser.parse_args()
    return run(args.json, summary_only=args.summary_only)


if __name__ == "__main__":
    raise SystemExit(main())
