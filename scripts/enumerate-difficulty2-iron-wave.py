#!/usr/bin/env python3
"""
Exact state enumerator for difficulty 2: 铁浪回声.

The script reuses the difficulty 1 exact combat engine, but owns the difficulty
2 card pool, player/enemy parameters, and report format. It enumerates real
random draw states rather than assuming fixed opening hands.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import importlib.util
import itertools
import json
import os
import sys
from functools import lru_cache
from pathlib import Path
from typing import Iterable


ROOT = Path(__file__).resolve().parents[1]
BASE_SCRIPT = ROOT / "scripts" / "enumerate-difficulty1-cultist.py"

spec = importlib.util.spec_from_file_location("gongdou_difficulty1_engine", BASE_SCRIPT)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load base enumerator: {BASE_SCRIPT}")
BASE = importlib.util.module_from_spec(spec)
sys.modules["gongdou_difficulty1_engine"] = BASE
spec.loader.exec_module(BASE)

CARDS = BASE.CARDS
DEFAULT_POOL_LIMITS = {
    "Strike": 4,
    "PerfectedStrike": 2,
    "Bash": 1,
    "IronWave": 2,
    "ShrugItOff": 1,
    "Defend": 0,
    "BodySlam": 1,
    "Uppercut": 1,
}
CARD_NAMES_ZH = {
    "Strike": "打击",
    "PerfectedStrike": "完美打击",
    "Bash": "痛击",
    "IronWave": "铁斩波",
    "ShrugItOff": "耸肩无视",
    "Defend": "防御",
    "BodySlam": "全身撞击",
    "Uppercut": "上勾拳",
}
DEFAULT_PLAYER_HP = 12
DEFAULT_ENEMY_HP = 80
DEFAULT_ENEMY_DAMAGE = (0, 10, 16, 18)
DEFAULT_SELECTED_CARDS = 8
DEFAULT_MAX_TURNS = 4

CONFIG = {
    "player_hp": DEFAULT_PLAYER_HP,
    "enemy_hp": DEFAULT_ENEMY_HP,
    "enemy_damage": DEFAULT_ENEMY_DAMAGE,
    "selected_cards": DEFAULT_SELECTED_CARDS,
    "max_turns": DEFAULT_MAX_TURNS,
    "pool_limits": DEFAULT_POOL_LIMITS,
}

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")


def configure(config: dict) -> None:
    CONFIG.update(config)
    BASE.POOL_LIMITS.clear()
    BASE.POOL_LIMITS.update(CONFIG["pool_limits"])
    BASE.SELECTED_CARDS = int(CONFIG["selected_cards"])
    BASE.PLAYER_HP = int(CONFIG["player_hp"])
    BASE.ENEMY_HP = int(CONFIG["enemy_hp"])
    BASE.ENEMY_DAMAGE = tuple(CONFIG["enemy_damage"])
    BASE.MAX_TURNS = int(CONFIG["max_turns"])
    BASE.CARD_NAMES_ZH.update(CARD_NAMES_ZH)


def init_worker(config: dict) -> None:
    configure(config)


def enumerate_decks() -> Iterable[tuple[int, ...]]:
    ranges = [range(CONFIG["pool_limits"][card] + 1) for card in CARDS]
    for counts in itertools.product(*ranges):
        if sum(counts) == CONFIG["selected_cards"]:
            yield tuple(counts)


def damage_after_weak(base_damage: int, weak: int) -> int:
    return int(base_damage * 0.75) if weak > 0 else base_damage


def solve(deck: tuple[int, ...]) -> tuple[float, float, float, float, float]:
    strike_like_count = deck[BASE.CARD_INDEX["Strike"]] + deck[BASE.CARD_INDEX["PerfectedStrike"]]
    perfected_strike_damage = 6 + 2 * strike_like_count
    zero = (0.0, 0.0, 0.0, 0.0, 1.0)
    enemy_damage = tuple(CONFIG["enemy_damage"])
    max_turns = int(CONFIG["max_turns"])

    @lru_cache(maxsize=None)
    def start_turn(
        turn: int,
        draw_pile: tuple[int, ...],
        discard_pile: tuple[int, ...],
        player_hp: int,
        enemy_hp: int,
        vulnerable: int,
        weak: int,
    ) -> tuple[float, float, float, float, float]:
        if enemy_hp <= 0:
            output = [0.0] * 5
            output[min(turn - 1, max_turns) - 1] = 1.0
            return tuple(output)  # type: ignore[return-value]
        if turn > max_turns or player_hp <= 0:
            return zero

        accumulated = (0.0, 0.0, 0.0, 0.0, 0.0)
        for probability, hand, new_draw, new_discard in BASE.draw_cards(
            (0,) * len(CARDS),
            draw_pile,
            discard_pile,
            BASE.DRAW_PER_TURN,
        ):
            branch = best_action(
                turn,
                hand,
                new_draw,
                new_discard,
                (0,) * len(CARDS),
                player_hp,
                enemy_hp,
                vulnerable,
                weak,
                0,
            )
            accumulated = BASE.add_distribution(accumulated, branch, probability)
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
        weak: int,
        block: int,
    ) -> tuple[float, float, float, float, float]:
        if enemy_hp <= 0:
            output = [0.0] * 5
            output[turn - 1] = 1.0
            return tuple(output)  # type: ignore[return-value]

        damage = damage_after_weak(enemy_damage[turn - 1], weak)
        player_hp_after_attack = player_hp - max(0, damage - block)
        if player_hp_after_attack <= 0:
            best = zero
        else:
            end_turn_discard = BASE.vector_add(discard_pile, BASE.vector_add(hand, played_this_turn))
            best = start_turn(
                turn + 1,
                draw_pile,
                end_turn_discard,
                player_hp_after_attack,
                enemy_hp,
                max(0, vulnerable - 1),
                max(0, weak - 1),
            )

        spent_energy = sum(BASE.CARD_COST[card] * count for card, count in zip(CARDS, played_this_turn))
        available_energy = BASE.ENERGY_PER_TURN - spent_energy

        for index, card in enumerate(CARDS):
            if hand[index] <= 0 or available_energy < BASE.CARD_COST[card]:
                continue

            new_hand = list(hand)
            new_hand[index] -= 1
            new_hand_tuple = tuple(new_hand)
            new_played = list(played_this_turn)
            new_played[index] += 1
            new_played_tuple = tuple(new_played)
            new_enemy_hp = enemy_hp
            new_vulnerable = vulnerable
            new_weak = weak
            new_block = block

            if card == "Strike":
                new_enemy_hp -= BASE.damage_after_vulnerable(6, new_vulnerable)
            elif card == "PerfectedStrike":
                new_enemy_hp -= BASE.damage_after_vulnerable(perfected_strike_damage, new_vulnerable)
            elif card == "Bash":
                new_enemy_hp -= BASE.damage_after_vulnerable(8, new_vulnerable)
                new_vulnerable += 2
            elif card == "IronWave":
                new_block += 5
                new_enemy_hp -= BASE.damage_after_vulnerable(5, new_vulnerable)
            elif card == "ShrugItOff":
                new_block += 8
                accumulated = (0.0, 0.0, 0.0, 0.0, 0.0)
                for probability, drawn_hand, new_draw, new_discard in BASE.draw_cards(
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
                        new_weak,
                        new_block,
                    )
                    accumulated = BASE.add_distribution(accumulated, branch, probability)
                best = BASE.better_distribution(best, accumulated)
                continue
            elif card == "Defend":
                new_block += 5
            elif card == "BodySlam":
                new_enemy_hp -= BASE.damage_after_vulnerable(new_block, new_vulnerable)
            elif card == "Uppercut":
                new_enemy_hp -= BASE.damage_after_vulnerable(13, new_vulnerable)
                new_weak += 1
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
                new_weak,
                new_block,
            )
            best = BASE.better_distribution(best, branch)

        return best

    return start_turn(1, deck, (0,) * len(CARDS), int(CONFIG["player_hp"]), int(CONFIG["enemy_hp"]), 0, 0)


def solve_deck(deck: tuple[int, ...]):
    configure(CONFIG)
    return BASE.Result(cards=deck, distribution=solve(deck))


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


def classify_family(counts: tuple[int, ...]) -> str:
    cards = dict(zip(CARDS, counts))
    if cards["PerfectedStrike"] >= 2 and cards["Bash"] >= 1 and cards["Uppercut"] >= 1 and cards["Strike"] >= 4:
        return "快线：痛击 + 上勾拳 + 双完美打击"
    if cards["IronWave"] >= 2 and cards["BodySlam"] >= 1 and cards["Bash"] >= 1:
        return "主线：铁斩波蓄格挡 + 全身撞击"
    if cards["PerfectedStrike"] >= 2 and cards["IronWave"] >= 1 and cards["ShrugItOff"] >= 1 and cards["Strike"] >= 3:
        return "厚打击线：完美打击 + 铁斩波 + 耸肩无视"
    if cards["PerfectedStrike"] >= 1 and cards["IronWave"] >= 1 and cards["Uppercut"] >= 1:
        return "混合线：完美打击 + 铁斩波 + 上勾拳"
    return "其他可通组合"


def parse_damage(value: str) -> tuple[int, ...]:
    parts = [part.strip() for part in value.split(",") if part.strip()]
    if len(parts) != 4:
        raise argparse.ArgumentTypeError("enemy damage must contain 4 comma-separated integers")
    return tuple(int(part) for part in parts)


def build_config(args: argparse.Namespace) -> dict:
    return {
        "player_hp": args.player_hp,
        "enemy_hp": args.enemy_hp,
        "enemy_damage": args.enemy_damage,
        "selected_cards": args.selected_cards,
        "max_turns": 4,
        "pool_limits": DEFAULT_POOL_LIMITS.copy(),
    }


def run(args: argparse.Namespace) -> int:
    config = build_config(args)
    configure(config)

    if not args.skip_resource_check:
        missing = BASE.verify_local_resources()
        if missing:
            print("资源校验失败：")
            for item in missing:
                print(f"- {item}")
            return 2

    decks = list(enumerate_decks())
    jobs = max(1, min(args.jobs or (os.cpu_count() or 1), 8))
    if jobs == 1:
        results = [solve_deck(deck) for deck in decks]
    else:
        with concurrent.futures.ProcessPoolExecutor(
            max_workers=jobs,
            initializer=init_worker,
            initargs=(config,),
        ) as executor:
            results = list(executor.map(solve_deck, decks))

    results.sort(key=lambda item: (item.first_kill_turn or 99, -item.success_rate, item.fail_rate, item.cards))
    viable = [item for item in results if item.success_rate > 1e-12]
    stable = [item for item in results if item.success_rate >= 1.0 - 1e-12]
    family_summary: dict[str, int] = {}
    for item in viable:
        family = classify_family(item.cards)
        family_summary[family] = family_summary.get(family, 0) + 1

    print("资源校验：SKIPPED" if args.skip_resource_check else "资源校验：OK")
    print("第二题参数：")
    print(f"- player_hp = {config['player_hp']}")
    print(f"- enemy_hp = {config['enemy_hp']}")
    print(f"- enemy_damage = {config['enemy_damage']}")
    print(f"- selected_cards = {config['selected_cards']}")
    print(f"case_count = {len(results)}")
    print(f"viable_count = {len(viable)}")
    print(f"stable_count = {len(stable)}")
    print("解法族计数：")
    for family, count in sorted(family_summary.items(), key=lambda item: (-item[1], item[0])):
        print(f"- {family}: {count}")

    if not args.summary_only:
        print()
        print("所有可通组合：")
        for index, item in enumerate(viable, start=1):
            d = item.distribution
            print(
                f"{index:03d}. {format_cards(item.cards)} | "
                f"中文：{format_cards(item.cards, zh=True)} | "
                f"解法族：{classify_family(item.cards)} | "
                f"分布：T1 {pct(d[0])} / T2 {pct(d[1])} / T3 {pct(d[2])} / "
                f"T4 {pct(d[3])} / 失败 {pct(d[4])} | 总成功 {pct(item.success_rate)}"
            )

    if args.json is not None:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        payload = {
            "puzzleDoc": "docs/difficulty2_iron_wave_echo.md",
            "caseCount": len(results),
            "viableCount": len(viable),
            "stableCount": len(stable),
            "config": {
                "playerHp": config["player_hp"],
                "enemyHp": config["enemy_hp"],
                "enemyDamage": list(config["enemy_damage"]),
                "selectedCards": config["selected_cards"],
                "maxTurns": config["max_turns"],
                "poolLimits": config["pool_limits"],
            },
            "familySummary": family_summary,
            "results": [
                {
                    "cards": dict(zip(CARDS, item.cards)),
                    "cardsText": format_cards(item.cards),
                    "cardsTextZh": format_cards(item.cards, zh=True),
                    "family": classify_family(item.cards),
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
        args.json.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
        print()
        print(f"JSON written: {args.json}")

    return 0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--json", type=Path, help="Optional path for full JSON output.")
    parser.add_argument("--summary-only", action="store_true", help="Print only aggregate counts.")
    parser.add_argument("--skip-resource-check", action="store_true", help="Skip local STS2 resource verification.")
    parser.add_argument("--jobs", type=int, help="Parallel worker count.")
    parser.add_argument("--player-hp", type=int, default=DEFAULT_PLAYER_HP)
    parser.add_argument("--enemy-hp", type=int, default=DEFAULT_ENEMY_HP)
    parser.add_argument("--enemy-damage", type=parse_damage, default=DEFAULT_ENEMY_DAMAGE)
    parser.add_argument("--selected-cards", type=int, default=DEFAULT_SELECTED_CARDS)
    args = parser.parse_args()
    return run(args)


if __name__ == "__main__":
    raise SystemExit(main())
