#!/usr/bin/env python3
"""
Exact enumerator for 尖塔残局 01：巨斧重校.

This version intentionally keeps the public puzzle window to the opening hand:
- player deck: 8 slots
- the player may select 0..8 challenge cards
- unfilled slots are filled with Defend instead of using a hard thin-deck HP lock
- natural opening draw: 5 cards
- no in-turn draw/discard resources in the official pool
- enemy kills the player on its first action if the player has not killed first

The solver enumerates every legal opening hand, every legal potion/relic choice
in the configured pool, and every legal play order from that hand. It is exact
for the local puzzle model below; no fixed draw order is assumed.
"""

from __future__ import annotations

import itertools
import json
import math
from functools import lru_cache


BASE_HP = 122
DECK_SLOTS = 8
FILLER_CARD = "Defend"
OPENING_DRAW = 5

CARD_POOL = (
    "BeamCell",
    "Inflame",
    "Bludgeon",
    "Bash",
    "Hemokinesis",
    "TwinStrike",
    "Slice",
    "Bloodletting",
)

CARD_NAMES = {
    "BeamCell": "光束射线",
    "Inflame": "燃烧",
    "Bludgeon": "重锤",
    "Bash": "痛击",
    "Hemokinesis": "御血术",
    "TwinStrike": "双重打击",
    "Slice": "切割",
    "Bloodletting": "放血",
    "Defend": "防御",
}

POTION_POOL = ("FirePotion", "ExplosiveAmpoule")
POTION_NAMES = {
    "FirePotion": "火焰药水",
    "ExplosiveAmpoule": "爆炸安瓿",
}

RELIC_POOL = (None, "Akabeko", "BagOfMarbles", "Whetstone", "HappyFlower", "RedSkull")
RELIC_NAMES = {
    None: "无",
    "Akabeko": "赤牛",
    "BagOfMarbles": "弹珠袋",
    "Whetstone": "磨刀石",
    "HappyFlower": "开心小花",
    "RedSkull": "红头骨",
}

CARD_COST = {
    "BeamCell": 0,
    "Inflame": 1,
    "Bludgeon": 3,
    "Bash": 2,
    "Hemokinesis": 1,
    "TwinStrike": 1,
    "Slice": 0,
    "Bloodletting": 0,
    "Defend": 1,
}

ATTACKS = {
    "BeamCell": (3, 1),
    "Bludgeon": (32, 1),
    "Bash": (8, 1),
    "Hemokinesis": (15, 1),
    "TwinStrike": (5, 2),
    "Slice": (6, 1),
}


def initial_hp(card_count: int) -> int:
    return BASE_HP


def upgrade_bonus(card: str, upgraded: frozenset[str]) -> int:
    if card not in upgraded:
        return 0
    if card == "Bludgeon":
        return 10
    if card in {"Bash", "Hemokinesis"}:
        return 3
    if card in {"BeamCell", "TwinStrike", "Slice"}:
        return 2
    return 0


def apply_action(state, hand, potions, action, relic: str | None, upgraded: frozenset[str]):
    enemy_hp, player_hp, energy, strength, vulnerable, akabeko_used, burst_bonus = state
    hand = set(hand)
    potions = set(potions)
    action_type, name = action

    if action_type == "potion":
        if name not in potions:
            return None
        potions.remove(name)
        if name == "FirePotion":
            enemy_hp -= 20
        elif name == "ExplosiveAmpoule":
            burst_bonus += 10
        else:
            raise KeyError(name)
        return (enemy_hp, player_hp, energy, strength, vulnerable, akabeko_used, burst_bonus), tuple(sorted(hand)), tuple(sorted(potions))

    if name not in hand or energy < CARD_COST[name]:
        return None

    hand.remove(name)
    energy -= CARD_COST[name]

    if name == "Inflame":
        strength += 2
    elif name == "Bloodletting":
        player_hp -= 3
        energy += 2
    elif name == "Defend":
        pass
    else:
        base, hit_count = ATTACKS[name]
        for _ in range(hit_count):
            raw = base + upgrade_bonus(name, upgraded) + strength
            if relic == "RedSkull" and player_hp < 40:
                raw += 3
            if relic == "Akabeko" and not akabeko_used:
                raw += 8
                akabeko_used = True
            if burst_bonus:
                raw += burst_bonus
                burst_bonus = 0
            if vulnerable > 0:
                raw = int(raw * 1.5)
            raw = min(raw, 40)
            enemy_hp -= raw

        if name == "BeamCell":
            vulnerable += 1
        elif name == "Bash":
            vulnerable += 2
        elif name == "Hemokinesis":
            player_hp -= 2

    if player_hp <= 0:
        return None
    return (enemy_hp, player_hp, energy, strength, vulnerable, akabeko_used, burst_bonus), tuple(sorted(hand)), tuple(sorted(potions))


@lru_cache(maxsize=None)
def dfs_can_kill(state, hand, remaining_potions, relic: str | None, upgraded: tuple[str, ...]) -> bool:
    if state[0] <= 0:
        return True

    actions = [("card", card) for card in sorted(set(hand))] + [("potion", potion) for potion in remaining_potions]
    for action in actions:
        next_state = apply_action(state, hand, remaining_potions, action, relic, frozenset(upgraded))
        if next_state is not None and dfs_can_kill(next_state[0], next_state[1], next_state[2], relic, upgraded):
            return True
    return False


def can_kill(opening_hand: tuple[str, ...], potions: tuple[str, ...], relic: str | None, upgraded: frozenset[str], enemy_hp: int) -> bool:
    initial_vulnerable = 1 if relic == "BagOfMarbles" else 0
    initial_state = (enemy_hp, 80, 3, 0, initial_vulnerable, False, 0)
    return dfs_can_kill(
        initial_state,
        tuple(sorted(opening_hand)),
        tuple(sorted(potions)),
        relic,
        tuple(sorted(upgraded)),
    )


def solve_loadout(cards: tuple[str, ...], potions: tuple[str, ...], relic: str | None):
    enemy_hp = initial_hp(len(cards))
    effective_cards = tuple(cards) + (FILLER_CARD,) * max(0, DECK_SLOTS - len(cards))

    upgraded_sets = [frozenset()]
    if relic == "Whetstone":
        attacks = tuple(card for card in cards if card in ATTACKS)
        upgraded_sets = [
            frozenset(combo)
            for combo in itertools.combinations(attacks, min(2, len(attacks)))
        ] or [frozenset()]

    total_cases = math.comb(len(effective_cards), OPENING_DRAW) * len(upgraded_sets)
    success_cases = 0
    successful_hands = []
    opening_hands = {}
    for opening_hand in itertools.combinations(effective_cards, OPENING_DRAW):
        key = tuple(sorted(opening_hand))
        opening_hands[key] = opening_hands.get(key, 0) + 1

    for upgraded in upgraded_sets:
        for opening_hand, weight in opening_hands.items():
            if can_kill(opening_hand, potions, relic, upgraded, enemy_hp):
                success_cases += weight
                successful_hands.append({
                    "hand": [CARD_NAMES[card] for card in opening_hand],
                    "upgraded": [CARD_NAMES[card] for card in upgraded],
                    "weight": weight,
                })

    return enemy_hp, success_cases, total_cases, successful_hands


def zh(items, names):
    return [names[item] for item in items]


def main() -> int:
    viable = []
    checked = 0

    for card_count in range(0, len(CARD_POOL) + 1):
        for cards in itertools.combinations(CARD_POOL, card_count):
            for potion_count in range(0, len(POTION_POOL) + 1):
                for potions in itertools.combinations(POTION_POOL, potion_count):
                    for relic in RELIC_POOL:
                        checked += 1
                        enemy_hp, success_cases, total_cases, successful_hands = solve_loadout(cards, potions, relic)
                        if success_cases == 0:
                            continue
                        viable.append({
                            "enemyHp": enemy_hp,
                            "successCases": success_cases,
                            "totalCases": total_cases,
                            "successProbability": success_cases / total_cases,
                            "cards": zh(cards, CARD_NAMES),
                            "potions": zh(potions, POTION_NAMES),
                            "relic": RELIC_NAMES[relic],
                            "successfulHands": successful_hands,
                        })

    viable.sort(key=lambda item: (-item["successProbability"], item["relic"], item["potions"]))
    print(json.dumps({
        "checked": checked,
        "viableCount": len(viable),
        "viable": viable,
    }, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
