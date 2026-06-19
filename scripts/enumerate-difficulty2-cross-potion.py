#!/usr/bin/env python3
"""
OBSOLETE prototype enumerator for the abandoned difficulty 2 draft 毒刃过载.

Do not use this file for current puzzle 02. The active difficulty 2 audit
source is scripts/enumerate-difficulty2-armor-threshold.py.

This puzzle intentionally mixes Ironclad, Silent, and Defect cards, then adds
one required potion choice. It enumerates all legal card/potion resource
combinations and all random draw states with optimal play.
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
    "Bash",
    "IronWave",
    "BodySlam",
    "TwinStrike",
    "Slice",
    "BeamCell",
    "PoisonedStab",
)
POTIONS = (
    "FirePotion",
    "EnergyPotion",
    "PoisonPotion",
    "VulnerablePotion",
)
CARD_INDEX = {card: index for index, card in enumerate(CARDS)}
CARD_NAMES_ZH = {
    "Strike": "打击",
    "Bash": "痛击",
    "IronWave": "铁斩波",
    "BodySlam": "全身撞击",
    "TwinStrike": "双重打击",
    "Slice": "切割",
    "BeamCell": "光束射线",
    "PoisonedStab": "带毒刺击",
}
POTION_NAMES_ZH = {
    "FirePotion": "火焰药水",
    "EnergyPotion": "能量药水",
    "PoisonPotion": "毒药水",
    "VulnerablePotion": "易伤药水",
}
RESOURCE_IDS = {
    "Strike": "StrikeIronclad",
    "Bash": "Bash",
    "IronWave": "IronWave",
    "BodySlam": "BodySlam",
    "TwinStrike": "TwinStrike",
    "Slice": "Slice",
    "BeamCell": "BeamCell",
    "PoisonedStab": "PoisonedStab",
    "FirePotion": "FirePotion",
    "EnergyPotion": "EnergyPotion",
    "PoisonPotion": "PoisonPotion",
    "VulnerablePotion": "VulnerablePotion",
}
CARD_COST = {
    "Strike": 1,
    "Bash": 2,
    "IronWave": 1,
    "BodySlam": 1,
    "TwinStrike": 1,
    "Slice": 0,
    "BeamCell": 0,
    "PoisonedStab": 1,
}
POOL_LIMITS = {
    "Strike": 3,
    "Bash": 1,
    "IronWave": 1,
    "BodySlam": 1,
    "TwinStrike": 1,
    "Slice": 2,
    "BeamCell": 2,
    "PoisonedStab": 1,
}

PLAYER_HP = 12
ENERGY_PER_TURN = 3
DRAW_PER_TURN = 5
ENEMY_HP = 146
ENEMY_DAMAGE = (0, 14, 20)
MAX_TURNS = 3
SELECTED_CARDS = 6

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")


@dataclass(frozen=True)
class Result:
    cards: tuple[int, ...]
    potion: str
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
    checks = [(resource_id.encode("utf-8"), f"dll/pck: {resource_id}") for resource_id in RESOURCE_IDS.values()]
    return missing_needles(DLL_PATH, checks) + missing_needles(PCK_PATH, checks, chunk_size=8 * 1024 * 1024)


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


def solve(deck: tuple[int, ...], potion: str) -> tuple[float, float, float, float, float]:
    zero = (0.0, 0.0, 0.0, 0.0, 1.0)

    @lru_cache(maxsize=None)
    def start_turn(
        turn: int,
        draw_pile: tuple[int, ...],
        discard_pile: tuple[int, ...],
        player_hp: int,
        enemy_hp: int,
        vulnerable: int,
        poison: int,
        potion_used: bool,
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
            branch = best_action(
                turn,
                hand,
                new_draw,
                new_discard,
                player_hp,
                enemy_hp,
                vulnerable,
                poison,
                0,
                ENERGY_PER_TURN,
                potion_used,
            )
            accumulated = add_distribution(accumulated, branch, probability)
        return accumulated

    @lru_cache(maxsize=None)
    def best_action(
        turn: int,
        hand: tuple[int, ...],
        draw_pile: tuple[int, ...],
        discard_pile: tuple[int, ...],
        player_hp: int,
        enemy_hp: int,
        vulnerable: int,
        poison: int,
        block: int,
        energy: int,
        potion_used: bool,
    ) -> tuple[float, float, float, float, float]:
        if enemy_hp <= 0:
            output = [0.0] * 5
            output[turn - 1] = 1.0
            return tuple(output)  # type: ignore[return-value]

        poison_enemy_hp = enemy_hp - poison
        next_poison = max(0, poison - 1) if poison > 0 else 0
        if poison_enemy_hp <= 0:
            output = [0.0] * 5
            output[turn - 1] = 1.0
            best = tuple(output)  # type: ignore[assignment]
        else:
            player_hp_after_attack = player_hp - max(0, ENEMY_DAMAGE[turn - 1] - block)
            if player_hp_after_attack <= 0:
                best = zero
            else:
                end_turn_discard = vector_add(discard_pile, hand)
                best = start_turn(
                    turn + 1,
                    draw_pile,
                    end_turn_discard,
                    player_hp_after_attack,
                    poison_enemy_hp,
                    max(0, vulnerable - 1),
                    next_poison,
                    potion_used,
                )

        if not potion_used:
            if potion == "FirePotion":
                branch = best_action(
                    turn, hand, draw_pile, discard_pile, player_hp,
                    enemy_hp - 20, vulnerable, poison, block, energy, True
                )
                best = better_distribution(best, branch)
            elif potion == "EnergyPotion":
                branch = best_action(
                    turn, hand, draw_pile, discard_pile, player_hp,
                    enemy_hp, vulnerable, poison, block, energy + 2, True
                )
                best = better_distribution(best, branch)
            elif potion == "PoisonPotion":
                branch = best_action(
                    turn, hand, draw_pile, discard_pile, player_hp,
                    enemy_hp, vulnerable, poison + 12, block, energy, True
                )
                best = better_distribution(best, branch)
            elif potion == "VulnerablePotion":
                branch = best_action(
                    turn, hand, draw_pile, discard_pile, player_hp,
                    enemy_hp, vulnerable + 3, poison, block, energy, True
                )
                best = better_distribution(best, branch)

        for index, card in enumerate(CARDS):
            if hand[index] <= 0 or energy < CARD_COST[card]:
                continue

            new_hand = list(hand)
            new_hand[index] -= 1
            new_hand_tuple = tuple(new_hand)
            new_discard = list(discard_pile)
            new_discard[index] += 1
            new_discard_tuple = tuple(new_discard)
            new_enemy_hp = enemy_hp
            new_vulnerable = vulnerable
            new_poison = poison
            new_block = block

            if card == "Strike":
                new_enemy_hp -= damage_after_vulnerable(6, new_vulnerable)
            elif card == "Bash":
                new_enemy_hp -= damage_after_vulnerable(8, new_vulnerable)
                new_vulnerable += 2
            elif card == "IronWave":
                new_block += 5
                new_enemy_hp -= damage_after_vulnerable(5, new_vulnerable)
            elif card == "BodySlam":
                new_enemy_hp -= damage_after_vulnerable(new_block, new_vulnerable)
            elif card == "TwinStrike":
                new_enemy_hp -= damage_after_vulnerable(5, new_vulnerable)
                new_enemy_hp -= damage_after_vulnerable(5, new_vulnerable)
            elif card == "Slice":
                new_enemy_hp -= damage_after_vulnerable(6, new_vulnerable)
            elif card == "BeamCell":
                new_enemy_hp -= damage_after_vulnerable(3, new_vulnerable)
                new_vulnerable += 1
            elif card == "PoisonedStab":
                new_enemy_hp -= damage_after_vulnerable(6, new_vulnerable)
                new_poison += 3

            branch = best_action(
                turn,
                new_hand_tuple,
                draw_pile,
                new_discard_tuple,
                player_hp,
                new_enemy_hp,
                new_vulnerable,
                new_poison,
                new_block,
                energy - CARD_COST[card],
                potion_used,
            )
            best = better_distribution(best, branch)

        return best

    return start_turn(1, deck, (0,) * len(CARDS), PLAYER_HP, ENEMY_HP, 0, 0, False)


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


def classify_family(counts: tuple[int, ...], potion: str) -> str:
    if potion == "VulnerablePotion":
        return "易伤线：易伤药水 + 多段攻击"
    if potion == "EnergyPotion":
        return "能量线：能量药水 + 同回合爆发"
    if potion == "PoisonPotion":
        return "毒线：毒药水 + 持续压血"
    if potion == "FirePotion":
        return "火焰线：火焰药水 + 物理补刀"
    return "其他可通组合"


def solve_case(case: tuple[tuple[int, ...], str]) -> Result:
    deck, potion = case
    return Result(cards=deck, potion=potion, distribution=solve(deck, potion))


def run(output_json: Path | None, summary_only: bool = False, skip_resource_check: bool = False) -> int:
    if not skip_resource_check:
        missing = verify_local_resources()
        if missing:
            print("资源校验失败：")
            for item in missing:
                print(f"- {item}")
            return 2

    cases = [(deck, potion) for deck in enumerate_decks() for potion in POTIONS]
    jobs = max(1, min(os.cpu_count() or 1, 8))
    if jobs == 1:
        results = [solve_case(case) for case in cases]
    else:
        with concurrent.futures.ProcessPoolExecutor(max_workers=jobs) as executor:
            results = list(executor.map(solve_case, cases))

    results.sort(key=lambda item: (item.first_kill_turn or 99, -item.success_rate, item.fail_rate, item.potion, item.cards))
    viable = [item for item in results if item.success_rate > 1e-12]
    stable = [item for item in results if item.success_rate >= 1.0 - 1e-12]
    family_summary: dict[str, int] = {}
    for item in viable:
        family = classify_family(item.cards, item.potion)
        family_summary[family] = family_summary.get(family, 0) + 1

    print("资源校验：SKIPPED" if skip_resource_check else "资源校验：OK")
    print("第二题参数：")
    print(f"- player_hp = {PLAYER_HP}")
    print(f"- enemy_hp = {ENEMY_HP}")
    print(f"- enemy_damage = {ENEMY_DAMAGE}")
    print(f"- selected_cards = {SELECTED_CARDS}")
    print(f"- selected_potions = 1")
    print(f"case_count = {len(results)}")
    print(f"viable_count = {len(viable)}")
    print(f"stable_count = {len(stable)}")
    print("解法族计数：")
    for family, count in sorted(family_summary.items(), key=lambda item: (-item[1], item[0])):
        print(f"- {family}: {count}")

    if not summary_only:
        print()
        print("所有可通组合：")
        for index, item in enumerate(viable, start=1):
            d = item.distribution
            print(
                f"{index:03d}. {format_cards(item.cards)} + {item.potion} | "
                f"中文：{format_cards(item.cards, zh=True)} + {POTION_NAMES_ZH[item.potion]} | "
                f"解法族：{classify_family(item.cards, item.potion)} | "
                f"分布：T1 {pct(d[0])} / T2 {pct(d[1])} / T3 {pct(d[2])} / "
                f"T4 {pct(d[3])} / 失败 {pct(d[4])} | 总成功 {pct(item.success_rate)}"
            )

    if output_json is not None:
        output_json.parent.mkdir(parents=True, exist_ok=True)
        payload = {
            "puzzleDoc": "docs/difficulty2_iron_wave_echo.md",
            "caseCount": len(results),
            "viableCount": len(viable),
            "stableCount": len(stable),
            "config": {
                "playerHp": PLAYER_HP,
                "enemyHp": ENEMY_HP,
                "enemyDamage": list(ENEMY_DAMAGE),
                "selectedCards": SELECTED_CARDS,
                "selectedPotions": 1,
                "maxTurns": MAX_TURNS,
                "poolLimits": POOL_LIMITS,
                "potions": POTIONS,
            },
            "familySummary": family_summary,
            "results": [
                {
                    "cards": dict(zip(CARDS, item.cards)),
                    "potion": item.potion,
                    "cardsText": format_cards(item.cards),
                    "cardsTextZh": format_cards(item.cards, zh=True),
                    "potionTextZh": POTION_NAMES_ZH[item.potion],
                    "family": classify_family(item.cards, item.potion),
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
    parser.add_argument("--summary-only", action="store_true", help="Print only aggregate counts.")
    parser.add_argument("--skip-resource-check", action="store_true", help="Skip local STS2 resource verification.")
    args = parser.parse_args()
    return run(args.json, summary_only=args.summary_only, skip_resource_check=args.skip_resource_check)


if __name__ == "__main__":
    raise SystemExit(main())
