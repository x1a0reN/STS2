#!/usr/bin/env python3
"""
Exact state enumerator for difficulty 2: 药水池与护甲门槛.

This script is the audit source for docs/difficulty2_final_puzzle.md. It models
the puzzle text directly instead of relying on any live STS2 implementation
quirks.
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
    "StrikeIronclad",
    "DefendIronclad",
    "Bash",
    "Neutralize",
    "BallLightning",
    "Survivor",
    "QuickSlash",
    "DaggerThrow",
    "Clothesline",
)
POTIONS = ("FirePotion", "VulnerablePotion", "WeakPotion")
CARD_NAMES_ZH = {
    "StrikeIronclad": "打击",
    "DefendIronclad": "防御",
    "Bash": "重击",
    "Neutralize": "中和",
    "BallLightning": "球状闪电（改）",
    "Survivor": "生存者",
    "QuickSlash": "切割（改）",
    "DaggerThrow": "投掷匕首（改）",
    "Clothesline": "上勾拳（改）",
}
POTION_NAMES_ZH = {
    "FirePotion": "火焰药水",
    "VulnerablePotion": "易伤药水",
    "WeakPotion": "虚弱药水",
}
RESOURCE_IDS = {
    **{card: card for card in CARDS},
    **{potion: potion for potion in POTIONS},
}
CARD_COST = {
    "StrikeIronclad": 1,
    "DefendIronclad": 1,
    "Bash": 2,
    "Neutralize": 0,
    "BallLightning": 1,
    "Survivor": 1,
    "QuickSlash": 1,
    "DaggerThrow": 1,
    "Clothesline": 2,
}
POOL_LIMITS = {
    "StrikeIronclad": 3,
    "DefendIronclad": 2,
    "Bash": 1,
    "Neutralize": 1,
    "BallLightning": 2,
    "Survivor": 1,
    "QuickSlash": 2,
    "DaggerThrow": 1,
    "Clothesline": 1,
}

PLAYER_HP = 14
ENERGY_PER_TURN = 3
DRAW_PER_TURN = 5
ENEMY_HP = 99
ENEMY_STARTING_ARMOR = 0
ENEMY_END_TURN_1_ARMOR_GAIN = 14
ENEMY_DAMAGE = (0, 10, 18, 24)
MAX_TURNS = 4
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
    checks = [(resource_id.encode("utf-8"), resource_id) for resource_id in sorted(set(RESOURCE_IDS.values()))]
    missing = missing_needles(DLL_PATH, checks)
    if missing:
        missing = missing_needles(PCK_PATH, [(item.encode("utf-8"), item) for item in missing], 8 * 1024 * 1024)
    return missing


def vector_add(left: tuple[int, ...], right: tuple[int, ...]) -> tuple[int, ...]:
    return tuple(a + b for a, b in zip(left, right))


def vector_sub(left: tuple[int, ...], right: tuple[int, ...]) -> tuple[int, ...]:
    return tuple(a - b for a, b in zip(left, right))


def total(cards: tuple[int, ...]) -> int:
    return sum(cards)


def choose_counts(cards: tuple[int, ...], amount: int) -> list[tuple[tuple[int, ...], float]]:
    if amount < 0 or amount > total(cards):
        return []
    if amount == 0:
        return [((0,) * len(cards), 1.0)]

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


def attack_damage(base_damage: int, vulnerable: int) -> int:
    return int(base_damage * 1.5) if vulnerable > 0 else base_damage


def deal_damage(enemy_hp: int, armor: int, amount: int) -> tuple[int, int]:
    absorbed = min(armor, amount)
    armor -= absorbed
    enemy_hp -= amount - absorbed
    return enemy_hp, armor


def monster_damage(turn: int, weak: int, block: int) -> int:
    damage = ENEMY_DAMAGE[turn - 1]
    if weak > 0:
        damage = int(damage * 0.75)
    return max(0, damage - block)


def solve(deck: tuple[int, ...], potion: str) -> tuple[float, float, float, float, float]:
    fail = (0.0, 0.0, 0.0, 0.0, 1.0)

    @lru_cache(maxsize=None)
    def start_turn(
        turn: int,
        draw_pile: tuple[int, ...],
        discard_pile: tuple[int, ...],
        player_hp: int,
        enemy_hp: int,
        armor: int,
        vulnerable: int,
        weak: int,
        block: int,
        potion_used: bool,
    ) -> tuple[float, float, float, float, float]:
        _ = block
        if enemy_hp <= 0:
            output = [0.0] * 5
            output[min(turn - 1, MAX_TURNS) - 1] = 1.0
            return tuple(output)  # type: ignore[return-value]
        if turn > MAX_TURNS or player_hp <= 0:
            return fail

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
                armor,
                vulnerable,
                weak,
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
        armor: int,
        vulnerable: int,
        weak: int,
        block: int,
        energy: int,
        potion_used: bool,
    ) -> tuple[float, float, float, float, float]:
        if enemy_hp <= 0:
            output = [0.0] * 5
            output[turn - 1] = 1.0
            return tuple(output)  # type: ignore[return-value]

        turn_end_armor = armor + (ENEMY_END_TURN_1_ARMOR_GAIN if turn == 1 else 0)
        damage_taken = monster_damage(turn, weak, block)
        hp_after_attack = player_hp - damage_taken
        if hp_after_attack <= 0:
            best = fail
        elif turn >= MAX_TURNS:
            best = fail
        else:
            best = start_turn(
                turn + 1,
                draw_pile,
                vector_add(discard_pile, hand),
                hp_after_attack,
                enemy_hp,
                turn_end_armor,
                max(0, vulnerable - 1),
                max(0, weak - 1),
                0,
                potion_used,
            )

        if not potion_used:
            if potion == "FirePotion":
                new_hp, new_armor = deal_damage(enemy_hp, armor, 20)
                branch = best_action(
                    turn, hand, draw_pile, discard_pile, player_hp,
                    new_hp, new_armor, vulnerable, weak, block, energy, True
                )
                best = better_distribution(best, branch)
            elif potion == "VulnerablePotion":
                branch = best_action(
                    turn, hand, draw_pile, discard_pile, player_hp,
                    enemy_hp, armor, vulnerable + 2, weak, block, energy, True
                )
                best = better_distribution(best, branch)
            elif potion == "WeakPotion":
                branch = best_action(
                    turn, hand, draw_pile, discard_pile, player_hp,
                    enemy_hp, armor, vulnerable, weak + 2, block, energy, True
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
            new_armor = armor
            new_vulnerable = vulnerable
            new_weak = weak
            new_block = block

            if card == "StrikeIronclad":
                new_enemy_hp, new_armor = deal_damage(new_enemy_hp, new_armor, attack_damage(6, new_vulnerable))
            elif card == "DefendIronclad":
                new_block += 5
            elif card == "Bash":
                new_enemy_hp, new_armor = deal_damage(new_enemy_hp, new_armor, attack_damage(8, new_vulnerable))
                new_vulnerable += 2
            elif card == "Neutralize":
                new_enemy_hp, new_armor = deal_damage(new_enemy_hp, new_armor, attack_damage(3, new_vulnerable))
                new_weak += 1
            elif card == "BallLightning":
                damage = 11 if new_armor > 0 else 5
                new_enemy_hp, new_armor = deal_damage(new_enemy_hp, new_armor, attack_damage(damage, new_vulnerable))
            elif card == "Survivor":
                new_block += 8
            elif card == "QuickSlash":
                damage = 11 if new_vulnerable > 0 else 7
                new_enemy_hp, new_armor = deal_damage(new_enemy_hp, new_armor, attack_damage(damage, new_vulnerable))
            elif card == "DaggerThrow":
                new_enemy_hp, new_armor = deal_damage(new_enemy_hp, new_armor, attack_damage(9, new_vulnerable))
            elif card == "Clothesline":
                new_enemy_hp, new_armor = deal_damage(new_enemy_hp, new_armor, attack_damage(12, new_vulnerable))
                new_weak += 2

            branch = best_action(
                turn,
                new_hand_tuple,
                draw_pile,
                new_discard_tuple,
                player_hp,
                new_enemy_hp,
                new_armor,
                new_vulnerable,
                new_weak,
                new_block,
                energy - CARD_COST[card],
                potion_used,
            )
            best = better_distribution(best, branch)

        return best

    return start_turn(
        1,
        deck,
        (0,) * len(CARDS),
        PLAYER_HP,
        ENEMY_HP,
        ENEMY_STARTING_ARMOR,
        0,
        0,
        0,
        False,
    )


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
    cards = dict(zip(CARDS, counts))
    if potion == "FirePotion" and cards.get("Survivor", 0) and cards.get("Bash", 0) and cards.get("Clothesline", 0):
        return "稳定护甲线"
    if potion == "FirePotion" and cards.get("QuickSlash", 0) == 2 and cards.get("Bash", 0):
        return "抢三转四线"
    if potion == "VulnerablePotion":
        return "破甲压血线"
    if potion == "WeakPotion":
        return "虚弱诱导线"
    if potion == "FirePotion":
        return "火焰补伤变体"
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
    turn_summary = [0, 0, 0, 0]
    for item in viable:
        family_summary[classify_family(item.cards, item.potion)] = family_summary.get(classify_family(item.cards, item.potion), 0) + 1
        for index, value in enumerate(item.distribution[:-1]):
            if value > 1e-12:
                turn_summary[index] += 1

    print("资源校验：SKIPPED" if skip_resource_check else "资源校验：OK")
    print("第二题参数：")
    print(f"- player_hp = {PLAYER_HP}")
    print(f"- enemy_hp = {ENEMY_HP}")
    print(f"- enemy_damage = {ENEMY_DAMAGE}")
    print(f"- selected_cards = {SELECTED_CARDS}")
    print("- selected_potions = 1")
    print(f"case_count = {len(results)}")
    print(f"viable_count = {len(viable)}")
    print(f"stable_count = {len(stable)}")
    print("击杀回合覆盖：")
    for index, count in enumerate(turn_summary, start=1):
        print(f"- T{index}: {count}")
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
            "puzzleDoc": "docs/difficulty2_final_puzzle.md",
            "caseCount": len(results),
            "viableCount": len(viable),
            "stableCount": len(stable),
            "config": {
                "playerHp": PLAYER_HP,
                "enemyHp": ENEMY_HP,
                "enemyStartingArmor": ENEMY_STARTING_ARMOR,
                "enemyEndTurn1ArmorGain": ENEMY_END_TURN_1_ARMOR_GAIN,
                "enemyDamage": list(ENEMY_DAMAGE),
                "selectedCards": SELECTED_CARDS,
                "selectedPotions": 1,
                "maxTurns": MAX_TURNS,
                "poolLimits": POOL_LIMITS,
                "potions": POTIONS,
            },
            "turnSummary": turn_summary,
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
