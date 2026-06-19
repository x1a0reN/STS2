#!/usr/bin/env python3
"""
OBSOLETE prototype enumerator for an abandoned difficulty 2 draft.

Do not use this file for current puzzle 02. The active difficulty 2 audit
source is scripts/enumerate-difficulty2-armor-threshold.py.

The puzzle deliberately mixes Ironclad, Silent, and Defect cards and requires
one potion. It enumerates all legal card/potion combinations and all random
draw branches with optimal play. Card text is modeled only for the resources
listed in this file.
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
    "TwinStrike",
    "Uppercut",
    "Inflame",
    "Hemokinesis",
    "Slice",
    "PoisonedStab",
    "DeadlyPoison",
    "BouncingFlask",
    "DaggerSpray",
    "Dash",
    "BeamCell",
)
POTIONS = (
    "FirePotion",
    "EnergyPotion",
    "PoisonPotion",
    "StrengthPotion",
)
CARD_NAMES_ZH = {
    "Strike": "打击",
    "Bash": "痛击",
    "IronWave": "铁斩波",
    "TwinStrike": "双重打击",
    "Uppercut": "上勾拳",
    "Inflame": "燃烧",
    "Hemokinesis": "御血术",
    "Slice": "切割",
    "PoisonedStab": "带毒刺击",
    "DeadlyPoison": "致命毒药",
    "BouncingFlask": "弹跳药瓶",
    "DaggerSpray": "匕首喷射",
    "Dash": "冲刺",
    "BeamCell": "光束射线",
}
POTION_NAMES_ZH = {
    "FirePotion": "火焰药水",
    "EnergyPotion": "能量药水",
    "PoisonPotion": "毒药水",
    "StrengthPotion": "力量药水",
}
RESOURCE_IDS = {
    "Strike": "StrikeIronclad",
    "Bash": "Bash",
    "IronWave": "IronWave",
    "TwinStrike": "TwinStrike",
    "Uppercut": "Uppercut",
    "Inflame": "Inflame",
    "Hemokinesis": "Hemokinesis",
    "Slice": "Slice",
    "PoisonedStab": "PoisonedStab",
    "DeadlyPoison": "DeadlyPoison",
    "BouncingFlask": "BouncingFlask",
    "DaggerSpray": "DaggerSpray",
    "Dash": "Dash",
    "BeamCell": "BeamCell",
    "FirePotion": "FirePotion",
    "EnergyPotion": "EnergyPotion",
    "PoisonPotion": "PoisonPotion",
    "StrengthPotion": "StrengthPotion",
}
CARD_COST = {
    "Strike": 1,
    "Bash": 2,
    "IronWave": 1,
    "TwinStrike": 1,
    "Uppercut": 2,
    "Inflame": 1,
    "Hemokinesis": 1,
    "Slice": 0,
    "PoisonedStab": 1,
    "DeadlyPoison": 1,
    "BouncingFlask": 2,
    "DaggerSpray": 1,
    "Dash": 2,
    "BeamCell": 0,
}
POOL_LIMITS = {
    "Strike": 1,
    "Bash": 1,
    "IronWave": 1,
    "TwinStrike": 1,
    "Uppercut": 1,
    "Inflame": 1,
    "Hemokinesis": 1,
    "Slice": 2,
    "PoisonedStab": 1,
    "DeadlyPoison": 1,
    "BouncingFlask": 1,
    "DaggerSpray": 1,
    "Dash": 1,
    "BeamCell": 2,
}

PLAYER_HP = 30
ENERGY_PER_TURN = 3
DRAW_PER_TURN = 5
ENEMY_HP = 132
ENEMY_DAMAGE = (6, 17, 9, 23, 99)
MAX_TURNS = 5
SELECTED_CARDS = 8

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")


@dataclass(frozen=True)
class Result:
    cards: tuple[int, ...]
    potion: str
    distribution: tuple[float, float, float, float, float, float]

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
    left: tuple[float, float, float, float, float, float],
    right: tuple[float, float, float, float, float, float],
    scale: float,
) -> tuple[float, float, float, float, float, float]:
    return tuple(a + scale * b for a, b in zip(left, right))  # type: ignore[return-value]


def better_distribution(
    left: tuple[float, float, float, float, float, float],
    right: tuple[float, float, float, float, float, float],
) -> tuple[float, float, float, float, float, float]:
    left_success = sum(left[:-1])
    right_success = sum(right[:-1])
    if abs(left_success - right_success) > 1e-12:
        return left if left_success > right_success else right
    return max(left, right)


def damage_after_vulnerable(base_damage: int, vulnerable: int) -> int:
    return int(base_damage * 1.5) if vulnerable > 0 else base_damage


def monster_attack_damage(turn: int, weak: int, block: int) -> int:
    base = ENEMY_DAMAGE[turn - 1]
    if weak > 0:
        base = int(base * 0.75)
    return max(0, base - block)


def hit_damage(base_damage: int, strength: int, vulnerable: int) -> int:
    return damage_after_vulnerable(base_damage + strength, vulnerable)


def solve(deck: tuple[int, ...], potion: str) -> tuple[float, float, float, float, float, float]:
    fail = (0.0, 0.0, 0.0, 0.0, 0.0, 1.0)

    @lru_cache(maxsize=None)
    def start_turn(
        turn: int,
        draw_pile: tuple[int, ...],
        discard_pile: tuple[int, ...],
        player_hp: int,
        enemy_hp: int,
        vulnerable: int,
        weak: int,
        poison: int,
        strength: int,
        potion_used: bool,
    ) -> tuple[float, float, float, float, float, float]:
        if enemy_hp <= 0:
            output = [0.0] * 6
            output[min(turn - 1, MAX_TURNS) - 1] = 1.0
            return tuple(output)  # type: ignore[return-value]
        if turn > MAX_TURNS or player_hp <= 0:
            return fail

        accumulated = (0.0, 0.0, 0.0, 0.0, 0.0, 0.0)
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
                weak,
                poison,
                strength,
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
        weak: int,
        poison: int,
        strength: int,
        block: int,
        energy: int,
        potion_used: bool,
    ) -> tuple[float, float, float, float, float, float]:
        if enemy_hp <= 0:
            output = [0.0] * 6
            output[turn - 1] = 1.0
            return tuple(output)  # type: ignore[return-value]

        poison_enemy_hp = enemy_hp - poison
        next_poison = max(0, poison - 1) if poison > 0 else 0
        if poison_enemy_hp <= 0:
            output = [0.0] * 6
            output[turn - 1] = 1.0
            best = tuple(output)  # type: ignore[assignment]
        else:
            damage_taken = monster_attack_damage(turn, weak, block)
            player_hp_after_attack = player_hp - damage_taken
            if player_hp_after_attack <= 0:
                best = fail
            else:
                end_turn_discard = vector_add(discard_pile, hand)
                best = start_turn(
                    turn + 1,
                    draw_pile,
                    end_turn_discard,
                    player_hp_after_attack,
                    poison_enemy_hp,
                    max(0, vulnerable - 1),
                    max(0, weak - 1),
                    next_poison,
                    strength,
                    potion_used,
                )

        if not potion_used:
            if potion == "FirePotion":
                branch = best_action(
                    turn, hand, draw_pile, discard_pile, player_hp,
                    enemy_hp - 20, vulnerable, weak, poison, strength, block, energy, True
                )
                best = better_distribution(best, branch)
            elif potion == "EnergyPotion":
                branch = best_action(
                    turn, hand, draw_pile, discard_pile, player_hp,
                    enemy_hp, vulnerable, weak, poison, strength, block, energy + 2, True
                )
                best = better_distribution(best, branch)
            elif potion == "PoisonPotion":
                branch = best_action(
                    turn, hand, draw_pile, discard_pile, player_hp,
                    enemy_hp, vulnerable, weak, poison + 12, strength, block, energy, True
                )
                best = better_distribution(best, branch)
            elif potion == "StrengthPotion":
                branch = best_action(
                    turn, hand, draw_pile, discard_pile, player_hp,
                    enemy_hp, vulnerable, weak, poison, strength + 2, block, energy, True
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
            new_player_hp = player_hp
            new_enemy_hp = enemy_hp
            new_vulnerable = vulnerable
            new_weak = weak
            new_poison = poison
            new_strength = strength
            new_block = block

            if card == "Strike":
                new_enemy_hp -= hit_damage(6, new_strength, new_vulnerable)
            elif card == "Bash":
                new_enemy_hp -= hit_damage(8, new_strength, new_vulnerable)
                new_vulnerable += 2
            elif card == "IronWave":
                new_enemy_hp -= hit_damage(5, new_strength, new_vulnerable)
                new_block += 5
            elif card == "TwinStrike":
                new_enemy_hp -= hit_damage(5, new_strength, new_vulnerable)
                new_enemy_hp -= hit_damage(5, new_strength, new_vulnerable)
            elif card == "Uppercut":
                new_enemy_hp -= hit_damage(13, new_strength, new_vulnerable)
                new_vulnerable += 1
                new_weak += 1
            elif card == "Inflame":
                new_strength += 2
            elif card == "Hemokinesis":
                new_player_hp -= 2
                if new_player_hp <= 0:
                    continue
                new_enemy_hp -= hit_damage(15, new_strength, new_vulnerable)
            elif card == "Slice":
                new_enemy_hp -= hit_damage(6, new_strength, new_vulnerable)
            elif card == "PoisonedStab":
                new_enemy_hp -= hit_damage(6, new_strength, new_vulnerable)
                new_poison += 3
            elif card == "DeadlyPoison":
                new_poison += 5
            elif card == "BouncingFlask":
                new_poison += 9
            elif card == "DaggerSpray":
                new_enemy_hp -= hit_damage(4, new_strength, new_vulnerable)
                new_enemy_hp -= hit_damage(4, new_strength, new_vulnerable)
            elif card == "Dash":
                new_enemy_hp -= hit_damage(10, new_strength, new_vulnerable)
                new_block += 10
            elif card == "BeamCell":
                new_enemy_hp -= hit_damage(3, new_strength, new_vulnerable)
                new_vulnerable += 1

            branch = best_action(
                turn,
                new_hand_tuple,
                draw_pile,
                new_discard_tuple,
                new_player_hp,
                new_enemy_hp,
                new_vulnerable,
                new_weak,
                new_poison,
                new_strength,
                new_block,
                energy - CARD_COST[card],
                potion_used,
            )
            best = better_distribution(best, branch)

        return best

    return start_turn(1, deck, (0,) * len(CARDS), PLAYER_HP, ENEMY_HP, 0, 0, 0, 0, False)


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
    if potion == "EnergyPotion" and cards.get("Hemokinesis", 0) and (
        cards.get("Uppercut", 0) or cards.get("Bash", 0)
    ):
        return "二回合快解：能量药水 + 易伤高费爆发"
    if potion == "PoisonPotion" or cards.get("BouncingFlask", 0) or cards.get("DeadlyPoison", 0):
        return "三回合毒压线：毒药水/毒牌 + 物理补伤"
    if cards.get("Dash", 0) or cards.get("IronWave", 0) or cards.get("Uppercut", 0):
        return "四回合生存线：格挡/虚弱 + 后续补刀"
    if potion == "FirePotion":
        return "固定伤害线：火焰药水 + 低费攻击"
    if potion == "StrengthPotion" or cards.get("Inflame", 0):
        return "力量多段线：力量 + 多段攻击"
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
    turn_summary = [0, 0, 0, 0, 0]
    best_by_turn: dict[int, Result] = {}
    for item in viable:
        family = classify_family(item.cards, item.potion)
        family_summary[family] = family_summary.get(family, 0) + 1
        for turn, probability in enumerate(item.distribution[:-1], start=1):
            if probability > 1e-12:
                turn_summary[turn - 1] += 1
                current = best_by_turn.get(turn)
                if current is None or probability > current.distribution[turn - 1]:
                    best_by_turn[turn] = item

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
    print("击杀回合覆盖：")
    for turn, count in enumerate(turn_summary, start=1):
        if count:
            best = best_by_turn[turn]
            print(f"- T{turn}: {count} cases, best {pct(best.distribution[turn - 1])}, {format_cards(best.cards, True)} + {POTION_NAMES_ZH[best.potion]}")
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
                f"T4 {pct(d[3])} / T5 {pct(d[4])} / 失败 {pct(d[5])} | 总成功 {pct(item.success_rate)}"
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
                "enemyDamage": list(ENEMY_DAMAGE),
                "selectedCards": SELECTED_CARDS,
                "selectedPotions": 1,
                "maxTurnsAudited": MAX_TURNS,
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
                        "turn5": item.distribution[4],
                        "fail": item.distribution[5],
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
