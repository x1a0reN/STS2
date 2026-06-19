#!/usr/bin/env python3
"""
Enumerate viable loadouts for "尖塔残局 01：巨斧重校".

Scope:
- Verifies every modeled card/potion/relic/enemy name against the local STS2
  install before solving.
- Enumerates resource loadouts and checks whether at least one legal favorable
  draw order plus optimal play sequence can kill the enemy within the configured
  turn window.

This is a viability enumerator, not a full shuffle-probability solver. It finds
non-zero-probability solutions under the local puzzle model, then optionally
prints a compact summary for review.
"""

from __future__ import annotations

import argparse
import itertools
import json
import math
import mmap
from dataclasses import dataclass, replace
from functools import lru_cache
from pathlib import Path
from typing import Iterable


GAME_DIR = Path(r"D:\Steam\steamapps\common\Slay the Spire 2")
# The public maintenance-failure window means a loadout must kill before the
# turn-4 maintenance completes.
MAX_TURNS = 4
MAX_CARDS = 16
MAX_POTIONS = 2
MAX_RELICS = 1
BASE_HP = 295
THIN_DECK_MIN_CARDS = 14
THIN_DECK_HP_PER_MISSING = 80


CARD_NAMES = {
    "BeamCell": "光束射线",
    "Thunderclap": "闪电霹雳",
    "Inflame": "燃烧",
    "Bludgeon": "重锤",
    "Bash": "痛击",
    "Hemokinesis": "御血术",
    "TwinStrike": "双重打击",
    "PommelStrike": "剑柄打击",
    "FlashOfSteel": "亮剑",
    "Slice": "切割",
    "Bloodletting": "放血",
    "DeadlyPoison": "致命毒药",
    "BouncingFlask": "弹跳药瓶",
    "NoxiousFumes": "毒雾",
    "PoisonedStab": "带毒刺击",
    "DefendIronclad": "防御",
    "Finesse": "妙计",
    "Prepared": "早有准备",
}

POTION_NAMES = {
    "SwiftPotion": "迅捷药水",
    "EnergyPotion": "能量药水",
    "FirePotion": "火焰药水",
    "PoisonPotion": "毒药水",
    "StrengthPotion": "力量药水",
    "VulnerablePotion": "易伤药水",
    "GamblersBrew": "赌徒特酿",
    "ExplosiveAmpoule": "爆炸安瓿",
}

RELIC_NAMES = {
    "BagOfPreparation": "准备背包",
    "Akabeko": "赤牛",
    "BagOfMarbles": "弹珠袋",
    "Whetstone": "磨刀石",
    "HappyFlower": "开心小花",
    "RedSkull": "红头骨",
}

ENEMY_NAMES = {"Axebot": "巨斧机器人"}

OFFICIAL_CARD_EXCLUSIONS = {"PommelStrike", "FlashOfSteel", "Finesse", "Prepared"}
OFFICIAL_POTION_EXCLUSIONS = {"SwiftPotion", "GamblersBrew"}

CARD_POOL = tuple(card for card in CARD_NAMES if card not in OFFICIAL_CARD_EXCLUSIONS)
POTION_POOL = tuple(potion for potion in POTION_NAMES if potion not in OFFICIAL_POTION_EXCLUSIONS)
RELIC_POOL = tuple(RELIC_NAMES.keys())

CARD_PRIORITY = {
    "Inflame": 110,
    "Bloodletting": 105,
    "BeamCell": 100,
    "Bash": 95,
    "Bludgeon": 94,
    "Hemokinesis": 90,
    "BouncingFlask": 84,
    "DeadlyPoison": 82,
    "NoxiousFumes": 80,
    "PoisonedStab": 78,
    "TwinStrike": 72,
    "PommelStrike": 70,
    "FlashOfSteel": 68,
    "Slice": 66,
    "Thunderclap": 64,
    "Finesse": 45,
    "Prepared": 42,
    "DefendIronclad": 15,
}

POTION_PRIORITY = {
    "EnergyPotion": 100,
    "SwiftPotion": 96,
    "PoisonPotion": 86,
    "VulnerablePotion": 84,
    "FirePotion": 82,
    "StrengthPotion": 78,
    "ExplosiveAmpoule": 75,
    "GamblersBrew": 60,
}

ATTACKS = {
    "BeamCell",
    "Thunderclap",
    "Bludgeon",
    "Bash",
    "Hemokinesis",
    "TwinStrike",
    "PommelStrike",
    "FlashOfSteel",
    "Slice",
    "PoisonedStab",
}

ACTION_LIMIT_PER_TURN = 8

CARD_INDEX = {card: index for index, card in enumerate(CARD_POOL)}
CARD_BY_INDEX = {index: card for card, index in CARD_INDEX.items()}
POTION_INDEX = {potion: index for index, potion in enumerate(POTION_POOL)}
POTION_BY_INDEX = {index: potion for potion, index in POTION_INDEX.items()}

CARD_COST = {
    "BeamCell": 0,
    "Thunderclap": 1,
    "Inflame": 1,
    "Bludgeon": 3,
    "Bash": 2,
    "Hemokinesis": 1,
    "TwinStrike": 1,
    "PommelStrike": 1,
    "FlashOfSteel": 0,
    "Slice": 0,
    "Bloodletting": 0,
    "DeadlyPoison": 1,
    "BouncingFlask": 2,
    "NoxiousFumes": 1,
    "PoisonedStab": 1,
    "DefendIronclad": 1,
    "Finesse": 0,
    "Prepared": 0,
}

ATTACK_BASE_AND_HITS = {
    "BeamCell": (3, 1),
    "Thunderclap": (4, 1),
    "Bludgeon": (32, 1),
    "Bash": (8, 1),
    "Hemokinesis": (15, 1),
    "TwinStrike": (5, 2),
    "PommelStrike": (9, 1),
    "FlashOfSteel": (5, 1),
    "Slice": (6, 1),
    "PoisonedStab": (6, 1),
}

DRAW_CARDS = {"PommelStrike", "FlashOfSteel", "Finesse"}


@dataclass(frozen=True)
class State:
    turn: int
    enemy_hp: int
    player_hp: int
    energy: int
    block: int
    strength: int
    vulnerable: int
    poison: int
    fumes: int
    hand: tuple[str, ...]
    draw: tuple[str, ...]
    discard: tuple[str, ...]
    potions: tuple[str, ...]
    relic: str | None
    akabeko_used: bool
    explosive_bonus: int
    upgraded: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class ExactState:
    turn: int
    enemy_hp: int
    player_hp: int
    energy: int
    block: int
    strength: int
    vulnerable: int
    poison: int
    fumes: int
    hand: int
    draw: int
    discard: int
    potions: int
    relic: str | None
    akabeko_used: bool
    explosive_bonus: int
    upgraded: int
    actions_left: int


def mask_from_items(items: Iterable[str], indexes: dict[str, int]) -> int:
    mask = 0
    for item in items:
        mask |= 1 << indexes[item]
    return mask


def items_from_mask(mask: int, reverse: dict[int, str]) -> tuple[str, ...]:
    return tuple(reverse[index] for index in range(len(reverse)) if mask & (1 << index))


def bit_count(mask: int) -> int:
    return mask.bit_count()


def iter_bits(mask: int) -> Iterable[int]:
    while mask:
        low = mask & -mask
        yield low.bit_length() - 1
        mask ^= low


@lru_cache(maxsize=None)
def submasks_of_size(mask: int, size: int) -> tuple[int, ...]:
    count = bit_count(mask)
    if size < 0 or size > count:
        return ()
    if size == 0:
        return (0,)
    if size == count:
        return (mask,)

    bits = tuple(iter_bits(mask))
    result: list[int] = []
    for combo in itertools.combinations(bits, size):
        submask = 0
        for bit in combo:
            submask |= 1 << bit
        result.append(submask)
    return tuple(result)


@lru_cache(maxsize=None)
def all_submasks(mask: int) -> tuple[int, ...]:
    bits = tuple(iter_bits(mask))
    result: list[int] = []
    for size in range(len(bits) + 1):
        for combo in itertools.combinations(bits, size):
            submask = 0
            for bit in combo:
                submask |= 1 << bit
            result.append(submask)
    return tuple(result)


def enemy_initial_hp(card_count: int) -> int:
    missing = max(0, THIN_DECK_MIN_CARDS - card_count)
    return BASE_HP + missing * THIN_DECK_HP_PER_MISSING


def verify_resources(game_dir: Path) -> None:
    dll_path = game_dir / "data_sts2_windows_x86_64" / "sts2.dll"
    pck_path = game_dir / "SlayTheSpire2.pck"
    dll_bytes = dll_path.read_bytes()
    failures: list[str] = []
    with pck_path.open("rb") as handle:
        mapped = mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ)
        for english, chinese in {
            **CARD_NAMES,
            **POTION_NAMES,
            **RELIC_NAMES,
            **ENEMY_NAMES,
        }.items():
            if english.encode("utf-8") not in dll_bytes:
                failures.append(f"{english}/{chinese}: missing English model name in sts2.dll")
            if mapped.find(chinese.encode("utf-8")) < 0:
                failures.append(f"{english}/{chinese}: missing Chinese display name in SlayTheSpire2.pck")
        mapped.close()

    if failures:
        raise RuntimeError("Resource verification failed:\n" + "\n".join(failures))


def draw_cards(state: State, count: int) -> State:
    if count <= 0:
        return state

    draw = list(state.draw)
    discard = list(state.discard)
    hand = list(state.hand)
    for _ in range(count):
        if not draw and discard:
            draw = sorted(discard, key=lambda card: CARD_PRIORITY.get(card, 0), reverse=True)
            discard = []
        if not draw:
            break
        hand.append(draw.pop(0))

    hand = sorted(set(hand), key=lambda card: CARD_PRIORITY.get(card, 0), reverse=True)
    return replace(state, hand=tuple(hand), draw=tuple(draw), discard=tuple(discard))


def damage_with_modifiers(state: State, base: int, card: str, hit_count: int = 1) -> tuple[int, State]:
    total = 0
    akabeko_used = state.akabeko_used
    explosive_bonus = state.explosive_bonus
    upgrade_bonus = upgrade_damage_bonus(card, state.upgraded)
    red_skull_strength = 3 if state.relic == "RedSkull" and state.player_hp < 40 else 0

    for _ in range(hit_count):
        raw = base + upgrade_bonus + state.strength + red_skull_strength
        if not akabeko_used and state.relic == "Akabeko":
            raw += 8
            akabeko_used = True
        if explosive_bonus > 0:
            raw += explosive_bonus
            explosive_bonus = 0
        if state.vulnerable > 0:
            raw = int(raw * 1.5)
        if state.turn == 1:
            raw = min(raw, 40)
        total += max(0, raw)

    return total, replace(state, akabeko_used=akabeko_used, explosive_bonus=explosive_bonus)


def upgrade_damage_bonus(card: str, upgraded: tuple[str, ...]) -> int:
    if card not in upgraded:
        return 0
    if card == "Bludgeon":
        return 10
    if card == "TwinStrike":
        return 2
    if card in {"Bash", "Hemokinesis", "PommelStrike", "PoisonedStab"}:
        return 3
    if card in {"BeamCell", "Thunderclap", "FlashOfSteel", "Slice"}:
        return 2
    return 0


def can_pay(state: State, cost: int) -> bool:
    return state.energy >= cost


def spend(state: State, cost: int) -> State:
    return replace(state, energy=state.energy - cost)


def remove_from_hand(state: State, card: str) -> State:
    hand = list(state.hand)
    hand.remove(card)
    return replace(state, hand=tuple(hand), discard=tuple(sorted((*state.discard, card))))


def apply_attack(state: State, card: str, base: int, cost: int, hit_count: int = 1) -> State | None:
    if not can_pay(state, cost):
        return None
    state = spend(state, cost)
    damage, state = damage_with_modifiers(state, base, card, hit_count)
    state = replace(state, enemy_hp=state.enemy_hp - damage)
    return remove_from_hand(state, card)


def play_card(state: State, card: str) -> State | None:
    if card == "BeamCell":
        state = apply_attack(state, card, 3, 0)
        return None if state is None else replace(state, vulnerable=state.vulnerable + 1)
    if card == "Thunderclap":
        state = apply_attack(state, card, 4, 1)
        return None if state is None else replace(state, vulnerable=state.vulnerable + 1)
    if card == "Inflame":
        if not can_pay(state, 1):
            return None
        return remove_from_hand(replace(spend(state, 1), strength=state.strength + 2), card)
    if card == "Bludgeon":
        return apply_attack(state, card, 32, 3)
    if card == "Bash":
        state = apply_attack(state, card, 8, 2)
        return None if state is None else replace(state, vulnerable=state.vulnerable + 2)
    if card == "Hemokinesis":
        state = apply_attack(state, card, 15, 1)
        return None if state is None else replace(state, player_hp=state.player_hp - 2)
    if card == "TwinStrike":
        return apply_attack(state, card, 5, 1, hit_count=2)
    if card == "PommelStrike":
        state = apply_attack(state, card, 9, 1)
        return None if state is None else draw_cards(state, 1)
    if card == "FlashOfSteel":
        state = apply_attack(state, card, 5, 0)
        return None if state is None else draw_cards(state, 1)
    if card == "Slice":
        return apply_attack(state, card, 6, 0)
    if card == "Bloodletting":
        state = remove_from_hand(state, card)
        return replace(state, player_hp=state.player_hp - 3, energy=state.energy + 2)
    if card == "DeadlyPoison":
        if not can_pay(state, 1):
            return None
        return remove_from_hand(replace(spend(state, 1), poison=state.poison + 5), card)
    if card == "BouncingFlask":
        if not can_pay(state, 2):
            return None
        return remove_from_hand(replace(spend(state, 2), poison=state.poison + 9), card)
    if card == "NoxiousFumes":
        if not can_pay(state, 1):
            return None
        return remove_from_hand(replace(spend(state, 1), fumes=state.fumes + 2), card)
    if card == "PoisonedStab":
        state = apply_attack(state, card, 6, 1)
        return None if state is None else replace(state, poison=state.poison + 3)
    if card == "DefendIronclad":
        if not can_pay(state, 1):
            return None
        return remove_from_hand(replace(spend(state, 1), block=state.block + 5), card)
    if card == "Finesse":
        return draw_cards(remove_from_hand(replace(state, block=state.block + 4), card), 1)
    if card == "Prepared":
        state = draw_cards(remove_from_hand(state, card), 1)
        # Optimistic discard: drop the least valuable card if hand is non-empty.
        if state.hand:
            hand = list(state.hand)
            hand.sort(key=lambda c: CARD_PRIORITY.get(c, 0))
            discarded = hand.pop(0)
            return replace(state, hand=tuple(sorted(hand, key=lambda c: CARD_PRIORITY.get(c, 0), reverse=True)),
                           discard=tuple(sorted((*state.discard, discarded))))
        return state
    raise KeyError(card)


def use_potion(state: State, potion: str) -> State | None:
    if potion not in state.potions:
        return None
    potions = list(state.potions)
    potions.remove(potion)
    state = replace(state, potions=tuple(potions))
    if potion == "SwiftPotion":
        return draw_cards(state, 3)
    if potion == "EnergyPotion":
        return replace(state, energy=state.energy + 2)
    if potion == "FirePotion":
        return replace(state, enemy_hp=state.enemy_hp - 20)
    if potion == "PoisonPotion":
        return replace(state, poison=state.poison + 12)
    if potion == "StrengthPotion":
        return replace(state, strength=state.strength + 2)
    if potion == "VulnerablePotion":
        return replace(state, vulnerable=state.vulnerable + 3)
    if potion == "ExplosiveAmpoule":
        return replace(state, explosive_bonus=state.explosive_bonus + 10)
    if potion == "GamblersBrew":
        # Optimistic model: redraw low-priority half of the hand.
        hand = list(state.hand)
        hand.sort(key=lambda c: CARD_PRIORITY.get(c, 0))
        redraw_count = len(hand) // 2
        kept = hand[redraw_count:]
        discarded = hand[:redraw_count]
        state = replace(
            state,
            hand=tuple(sorted(kept, key=lambda c: CARD_PRIORITY.get(c, 0), reverse=True)),
            discard=tuple(sorted((*state.discard, *discarded))),
        )
        return draw_cards(state, redraw_count)
    raise KeyError(potion)


def start_turn(state: State) -> State:
    energy = 3 + (1 if state.relic == "HappyFlower" and state.turn % 3 == 0 else 0)
    state = replace(state, energy=energy, block=0)
    if state.fumes:
        state = replace(state, poison=state.poison + state.fumes)
    draw_count = 5
    if state.turn == 1 and state.relic == "BagOfPreparation":
        draw_count += 2
    return draw_cards(state, draw_count)


def end_turn(state: State) -> State | None:
    enemy_hp = state.enemy_hp
    poison = state.poison
    vulnerable = max(0, state.vulnerable - 1)
    if poison > 0:
        enemy_hp -= poison
        poison = max(0, poison - 1)
    if enemy_hp <= 0:
        return replace(state, enemy_hp=enemy_hp, poison=poison, vulnerable=vulnerable)

    attack = [10, 16, 22, 10][(state.turn - 1) % 4]
    attack += ((state.turn - 1) // 4) * 4
    damage = max(0, attack - state.block)
    player_hp = state.player_hp - damage
    if player_hp <= 0:
        return None

    discard = tuple(sorted((*state.discard, *state.hand)))
    next_state = replace(
        state,
        turn=state.turn + 1,
        enemy_hp=enemy_hp,
        player_hp=player_hp,
        poison=poison,
        vulnerable=vulnerable,
        hand=(),
        discard=discard,
        block=0,
    )
    if state.turn % 4 == 0 and enemy_hp > 0:
        next_state = replace(
            next_state,
            enemy_hp=min(enemy_initial_hp(len(state.hand) + len(state.draw) + len(state.discard)), enemy_hp + 24),
            poison=0,
            vulnerable=0,
        )
    return next_state


def zero_vec() -> tuple[float, ...]:
    return (0.0,) * MAX_TURNS


def kill_vec(turn: int) -> tuple[float, ...]:
    result = [0.0] * MAX_TURNS
    if 1 <= turn <= MAX_TURNS:
        result[turn - 1] = 1.0
    return tuple(result)


def vec_total(vec: tuple[float, ...]) -> float:
    return sum(vec)


def vec_add(left: tuple[float, ...], right: tuple[float, ...], weight: float = 1.0) -> tuple[float, ...]:
    return tuple(a + b * weight for a, b in zip(left, right))


def better_vec(left: tuple[float, ...], right: tuple[float, ...]) -> tuple[float, ...]:
    left_total = vec_total(left)
    right_total = vec_total(right)
    if left_total > right_total + 1e-15:
        return left
    if right_total > left_total + 1e-15:
        return right
    # Tie-break toward earlier kills so "bestTurn" is meaningful.
    for left_value, right_value in zip(left, right):
        if left_value > right_value + 1e-15:
            return left
        if right_value > left_value + 1e-15:
            return right
    return left


@lru_cache(maxsize=None)
def exact_draw_outcomes(hand: int, draw: int, discard: int, count: int) -> tuple[tuple[float, int, int, int], ...]:
    if count <= 0:
        return ((1.0, hand, draw, discard),)

    draw_count = bit_count(draw)
    if draw_count >= count:
        denom = math.comb(draw_count, count)
        return tuple(
            (1.0 / denom, hand | drawn, draw ^ drawn, discard)
            for drawn in submasks_of_size(draw, count)
        )

    hand_after_draw = hand | draw
    remaining_count = count - draw_count
    if discard == 0:
        return ((1.0, hand_after_draw, 0, 0),)

    discard_count = bit_count(discard)
    if discard_count <= remaining_count:
        return ((1.0, hand_after_draw | discard, 0, 0),)

    denom = math.comb(discard_count, remaining_count)
    return tuple(
        (1.0 / denom, hand_after_draw | drawn, discard ^ drawn, 0)
        for drawn in submasks_of_size(discard, remaining_count)
    )


def exact_damage_with_modifiers(state: ExactState, card: str, base: int, hit_count: int = 1) -> tuple[int, ExactState]:
    total = 0
    akabeko_used = state.akabeko_used
    explosive_bonus = state.explosive_bonus
    upgrade_bonus = upgrade_damage_bonus(card, items_from_mask(state.upgraded, CARD_BY_INDEX))
    red_skull_strength = 3 if state.relic == "RedSkull" and state.player_hp < 40 else 0

    for _ in range(hit_count):
        raw = base + upgrade_bonus + state.strength + red_skull_strength
        if not akabeko_used and state.relic == "Akabeko":
            raw += 8
            akabeko_used = True
        if explosive_bonus > 0:
            raw += explosive_bonus
            explosive_bonus = 0
        if state.vulnerable > 0:
            raw = int(raw * 1.5)
        if state.turn == 1:
            raw = min(raw, 40)
        total += max(0, raw)

    return total, replace(state, akabeko_used=akabeko_used, explosive_bonus=explosive_bonus)


def remove_card_from_exact_hand(state: ExactState, card: str, add_to_discard: bool = True) -> ExactState:
    bit = 1 << CARD_INDEX[card]
    discard = state.discard | bit if add_to_discard else state.discard
    return replace(state, hand=state.hand & ~bit, discard=discard)


def exact_draw_then_continue(state: ExactState, count: int, played_card: str | None = None) -> tuple[tuple[float, ExactState], ...]:
    played_bit = 0 if played_card is None else 1 << CARD_INDEX[played_card]
    outcomes: list[tuple[float, ExactState]] = []
    for probability, hand, draw, discard in exact_draw_outcomes(state.hand, state.draw, state.discard, count):
        outcomes.append((probability, replace(state, hand=hand, draw=draw, discard=discard | played_bit)))
    return tuple(outcomes)


def exact_play_card_outcomes(state: ExactState, card: str) -> tuple[tuple[float, ExactState], ...]:
    bit = 1 << CARD_INDEX[card]
    if not state.hand & bit:
        return ()

    cost = CARD_COST[card]
    if state.energy < cost:
        return ()

    state = replace(state, energy=state.energy - cost, actions_left=state.actions_left - 1)

    if card in ATTACK_BASE_AND_HITS:
        base, hit_count = ATTACK_BASE_AND_HITS[card]
        damage, state = exact_damage_with_modifiers(state, card, base, hit_count)
        state = replace(state, enemy_hp=state.enemy_hp - damage)

    if card == "BeamCell":
        state = replace(remove_card_from_exact_hand(state, card), vulnerable=state.vulnerable + 1)
        return ((1.0, state),)
    if card == "Thunderclap":
        state = replace(remove_card_from_exact_hand(state, card), vulnerable=state.vulnerable + 1)
        return ((1.0, state),)
    if card == "Inflame":
        state = replace(remove_card_from_exact_hand(state, card), strength=state.strength + 2)
        return ((1.0, state),)
    if card == "Bludgeon":
        return ((1.0, remove_card_from_exact_hand(state, card)),)
    if card == "Bash":
        state = replace(remove_card_from_exact_hand(state, card), vulnerable=state.vulnerable + 2)
        return ((1.0, state),)
    if card == "Hemokinesis":
        state = replace(remove_card_from_exact_hand(state, card), player_hp=state.player_hp - 2)
        return ((1.0, state),)
    if card == "TwinStrike":
        return ((1.0, remove_card_from_exact_hand(state, card)),)
    if card == "PommelStrike":
        state = remove_card_from_exact_hand(state, card, add_to_discard=False)
        return exact_draw_then_continue(state, 1, played_card=card)
    if card == "FlashOfSteel":
        state = remove_card_from_exact_hand(state, card, add_to_discard=False)
        return exact_draw_then_continue(state, 1, played_card=card)
    if card == "Slice":
        return ((1.0, remove_card_from_exact_hand(state, card)),)
    if card == "Bloodletting":
        state = replace(remove_card_from_exact_hand(state, card), player_hp=state.player_hp - 3, energy=state.energy + 2)
        return ((1.0, state),)
    if card == "DeadlyPoison":
        state = replace(remove_card_from_exact_hand(state, card), poison=state.poison + 5)
        return ((1.0, state),)
    if card == "BouncingFlask":
        state = replace(remove_card_from_exact_hand(state, card), poison=state.poison + 9)
        return ((1.0, state),)
    if card == "NoxiousFumes":
        state = replace(remove_card_from_exact_hand(state, card), fumes=state.fumes + 2)
        return ((1.0, state),)
    if card == "PoisonedStab":
        state = replace(remove_card_from_exact_hand(state, card), poison=state.poison + 3)
        return ((1.0, state),)
    if card == "DefendIronclad":
        state = replace(remove_card_from_exact_hand(state, card), block=state.block + 5)
        return ((1.0, state),)
    if card == "Finesse":
        state = replace(remove_card_from_exact_hand(state, card, add_to_discard=False), block=state.block + 4)
        return exact_draw_then_continue(state, 1, played_card=card)
    if card == "Prepared":
        state = remove_card_from_exact_hand(state, card, add_to_discard=False)
        outcomes: list[tuple[float, ExactState]] = []
        for probability, drawn_state in exact_draw_then_continue(state, 1, played_card=card):
            if drawn_state.hand == 0:
                outcomes.append((probability, drawn_state))
                continue
            for discard_bit_index in iter_bits(drawn_state.hand):
                discard_bit = 1 << discard_bit_index
                outcomes.append((
                    probability,
                    replace(
                        drawn_state,
                        hand=drawn_state.hand & ~discard_bit,
                        discard=drawn_state.discard | discard_bit,
                    ),
                ))
        return tuple(outcomes)
    raise KeyError(card)


def exact_use_potion_outcomes(state: ExactState, potion: str) -> tuple[tuple[float, ExactState], ...]:
    bit = 1 << POTION_INDEX[potion]
    if not state.potions & bit:
        return ()

    state = replace(state, potions=state.potions & ~bit, actions_left=state.actions_left - 1)
    if potion == "SwiftPotion":
        return tuple(
            (probability, replace(state, hand=hand, draw=draw, discard=discard))
            for probability, hand, draw, discard in exact_draw_outcomes(state.hand, state.draw, state.discard, 3)
        )
    if potion == "EnergyPotion":
        return ((1.0, replace(state, energy=state.energy + 2)),)
    if potion == "FirePotion":
        return ((1.0, replace(state, enemy_hp=state.enemy_hp - 20)),)
    if potion == "PoisonPotion":
        return ((1.0, replace(state, poison=state.poison + 12)),)
    if potion == "StrengthPotion":
        return ((1.0, replace(state, strength=state.strength + 2)),)
    if potion == "VulnerablePotion":
        return ((1.0, replace(state, vulnerable=state.vulnerable + 3)),)
    if potion == "ExplosiveAmpoule":
        return ((1.0, replace(state, explosive_bonus=state.explosive_bonus + 10)),)
    if potion == "GamblersBrew":
        outcomes: list[tuple[float, ExactState]] = []
        for discarded in all_submasks(state.hand):
            kept = state.hand & ~discarded
            redraw_count = bit_count(discarded)
            draw_base = replace(state, hand=kept, discard=state.discard | discarded)
            for probability, hand, draw, discard in exact_draw_outcomes(draw_base.hand, draw_base.draw, draw_base.discard, redraw_count):
                outcomes.append((probability, replace(draw_base, hand=hand, draw=draw, discard=discard)))
        return tuple(outcomes)
    raise KeyError(potion)


def exact_prepared_vec(state: ExactState) -> tuple[float, ...]:
    card = "Prepared"
    bit = 1 << CARD_INDEX[card]
    if not state.hand & bit or state.energy < CARD_COST[card]:
        return zero_vec()

    base = replace(
        state,
        energy=state.energy - CARD_COST[card],
        actions_left=state.actions_left - 1,
        hand=state.hand & ~bit,
    )
    played_bit = bit
    result = zero_vec()
    for probability, hand, draw, discard in exact_draw_outcomes(base.hand, base.draw, base.discard, 1):
        drawn_state = replace(base, hand=hand, draw=draw, discard=discard)
        if drawn_state.hand == 0:
            result = vec_add(result, exact_action_vec(replace(drawn_state, discard=drawn_state.discard | played_bit)), probability)
            continue

        best_after_draw = zero_vec()
        for discard_bit_index in iter_bits(drawn_state.hand):
            discard_bit = 1 << discard_bit_index
            next_state = replace(
                drawn_state,
                hand=drawn_state.hand & ~discard_bit,
                discard=drawn_state.discard | discard_bit | played_bit,
            )
            best_after_draw = better_vec(best_after_draw, exact_action_vec(next_state))
        result = vec_add(result, best_after_draw, probability)
    return result


def exact_start_turn_vec(state: ExactState) -> tuple[float, ...]:
    energy = 3 + (1 if state.relic == "HappyFlower" and state.turn % 3 == 0 else 0)
    state = replace(state, energy=energy, block=0, actions_left=ACTION_LIMIT_PER_TURN)
    if state.fumes:
        state = replace(state, poison=state.poison + state.fumes)
    draw_count = 5 + (2 if state.turn == 1 and state.relic == "BagOfPreparation" else 0)
    result = zero_vec()
    for probability, hand, draw, discard in exact_draw_outcomes(state.hand, state.draw, state.discard, draw_count):
        result = vec_add(result, exact_action_vec(replace(state, hand=hand, draw=draw, discard=discard)), probability)
    return result


def exact_end_turn_vec(state: ExactState) -> tuple[float, ...]:
    enemy_hp = state.enemy_hp
    poison = state.poison
    vulnerable = max(0, state.vulnerable - 1)
    if poison > 0:
        enemy_hp -= poison
        poison = max(0, poison - 1)
    if enemy_hp <= 0:
        return kill_vec(state.turn)

    if state.turn >= MAX_TURNS:
        return zero_vec()

    attack = [10, 16, 22, 10][(state.turn - 1) % 4]
    attack += ((state.turn - 1) // 4) * 4
    player_hp = state.player_hp - max(0, attack - state.block)
    if player_hp <= 0:
        return zero_vec()

    next_state = replace(
        state,
        turn=state.turn + 1,
        enemy_hp=enemy_hp,
        player_hp=player_hp,
        poison=poison,
        vulnerable=vulnerable,
        hand=0,
        discard=state.discard | state.hand,
        block=0,
        actions_left=ACTION_LIMIT_PER_TURN,
    )
    return exact_start_turn_vec(next_state)


@lru_cache(maxsize=None)
def exact_action_vec(state: ExactState) -> tuple[float, ...]:
    if state.enemy_hp <= 0:
        return kill_vec(state.turn)
    if state.player_hp <= 0 or state.turn > MAX_TURNS:
        return zero_vec()
    if state.actions_left <= 0:
        return exact_end_turn_vec(state)

    best = exact_end_turn_vec(state)

    for potion_index in iter_bits(state.potions):
        potion = POTION_BY_INDEX[potion_index]
        if potion == "GamblersBrew":
            grouped_best = zero_vec()
            # GamblersBrew outcomes include all discard choices; group by chosen
            # state probability through direct max over resulting expected values.
            for discarded in all_submasks(state.hand):
                potion_bit = 1 << POTION_INDEX[potion]
                base = replace(
                    state,
                    potions=state.potions & ~potion_bit,
                    actions_left=state.actions_left - 1,
                    hand=state.hand & ~discarded,
                    discard=state.discard | discarded,
                )
                value = zero_vec()
                redraw_count = bit_count(discarded)
                for probability, hand, draw, discard in exact_draw_outcomes(base.hand, base.draw, base.discard, redraw_count):
                    value = vec_add(value, exact_action_vec(replace(base, hand=hand, draw=draw, discard=discard)), probability)
                grouped_best = better_vec(grouped_best, value)
            best = better_vec(best, grouped_best)
            continue

        value = zero_vec()
        for probability, next_state in exact_use_potion_outcomes(state, potion):
            value = vec_add(value, exact_action_vec(next_state), probability)
        best = better_vec(best, value)

    for card_index in iter_bits(state.hand):
        card = CARD_BY_INDEX[card_index]
        if card == "Prepared":
            best = better_vec(best, exact_prepared_vec(state))
            continue
        outcomes = exact_play_card_outcomes(state, card)
        if not outcomes:
            continue
        value = zero_vec()
        for probability, next_state in outcomes:
            value = vec_add(value, exact_action_vec(next_state), probability)
        best = better_vec(best, value)

    return best


def solve_loadout_exact(cards: tuple[str, ...], potions: tuple[str, ...], relic: str | None) -> dict[str, object]:
    exact_action_vec.cache_clear()
    exact_draw_outcomes.cache_clear()

    card_mask = mask_from_items(cards, CARD_INDEX)
    potion_mask = mask_from_items(potions, POTION_INDEX)
    upgraded_masks = [0]
    if relic == "Whetstone":
        attacks = tuple(card for card in cards if card in ATTACKS)
        attack_masks = [mask_from_items(combo, CARD_INDEX) for combo in itertools.combinations(attacks, min(2, len(attacks)))]
        upgraded_masks = attack_masks or [0]

    total = zero_vec()
    for upgraded in upgraded_masks:
        initial = ExactState(
            turn=1,
            enemy_hp=enemy_initial_hp(len(cards)),
            player_hp=80,
            energy=0,
            block=0,
            strength=0,
            vulnerable=1 if relic == "BagOfMarbles" else 0,
            poison=0,
            fumes=0,
            hand=0,
            draw=card_mask,
            discard=0,
            potions=potion_mask,
            relic=relic,
            akabeko_used=False,
            explosive_bonus=0,
            upgraded=upgraded,
            actions_left=ACTION_LIMIT_PER_TURN,
        )
        total = vec_add(total, exact_start_turn_vec(initial), 1.0 / len(upgraded_masks))

    success_probability = vec_total(total)
    best_turn = next((index + 1 for index, value in enumerate(total) if value > 1e-12), None)
    return {
        "successProbability": success_probability,
        "killTurnDistribution": total,
        "killTurn": best_turn,
    }


def priority_sorted_cards(mask: int) -> list[str]:
    return sorted(items_from_mask(mask, CARD_BY_INDEX), key=lambda card: CARD_PRIORITY.get(card, 0), reverse=True)


def priority_sorted_potions(mask: int) -> list[str]:
    return sorted(items_from_mask(mask, POTION_BY_INDEX), key=lambda potion: POTION_PRIORITY.get(potion, 0), reverse=True)


def policy_prepared_outcomes(state: ExactState) -> tuple[tuple[float, ExactState], ...]:
    card = "Prepared"
    bit = 1 << CARD_INDEX[card]
    if not state.hand & bit or state.energy < CARD_COST[card]:
        return ()
    base = replace(
        state,
        energy=state.energy - CARD_COST[card],
        actions_left=state.actions_left - 1,
        hand=state.hand & ~bit,
    )
    outcomes: list[tuple[float, ExactState]] = []
    for probability, hand, draw, discard in exact_draw_outcomes(base.hand, base.draw, base.discard, 1):
        played_discard = discard | bit
        if hand:
            discarded_card = min(items_from_mask(hand, CARD_BY_INDEX), key=lambda card_id: CARD_PRIORITY.get(card_id, 0))
            discarded_bit = 1 << CARD_INDEX[discarded_card]
            hand &= ~discarded_bit
            played_discard |= discarded_bit
        outcomes.append((probability, replace(base, hand=hand, draw=draw, discard=played_discard)))
    return tuple(outcomes)


def policy_gamblers_outcomes(state: ExactState) -> tuple[tuple[float, ExactState], ...]:
    potion = "GamblersBrew"
    bit = 1 << POTION_INDEX[potion]
    if not state.potions & bit:
        return ()
    hand_cards = sorted(items_from_mask(state.hand, CARD_BY_INDEX), key=lambda card_id: CARD_PRIORITY.get(card_id, 0))
    redraw_count = len(hand_cards) // 2
    discarded_cards = hand_cards[:redraw_count]
    discarded = mask_from_items(discarded_cards, CARD_INDEX)
    base = replace(
        state,
        potions=state.potions & ~bit,
        actions_left=state.actions_left - 1,
        hand=state.hand & ~discarded,
        discard=state.discard | discarded,
    )
    return tuple(
        (probability, replace(base, hand=hand, draw=draw, discard=discard))
        for probability, hand, draw, discard in exact_draw_outcomes(base.hand, base.draw, base.discard, redraw_count)
    )


def policy_card_outcomes(state: ExactState, card: str) -> tuple[tuple[float, ExactState], ...]:
    if card == "Prepared":
        return policy_prepared_outcomes(state)
    return exact_play_card_outcomes(state, card)


def policy_potion_outcomes(state: ExactState, potion: str) -> tuple[tuple[float, ExactState], ...]:
    if potion == "GamblersBrew":
        return policy_gamblers_outcomes(state)
    return exact_use_potion_outcomes(state, potion)


def policy_start_turn_outcomes(state: ExactState) -> tuple[tuple[float, ExactState], ...]:
    energy = 3 + (1 if state.relic == "HappyFlower" and state.turn % 3 == 0 else 0)
    state = replace(state, energy=energy, block=0, actions_left=ACTION_LIMIT_PER_TURN)
    if state.fumes:
        state = replace(state, poison=state.poison + state.fumes)
    draw_count = 5 + (2 if state.turn == 1 and state.relic == "BagOfPreparation" else 0)
    return tuple(
        (probability, replace(state, hand=hand, draw=draw, discard=discard))
        for probability, hand, draw, discard in exact_draw_outcomes(state.hand, state.draw, state.discard, draw_count)
    )


def policy_end_turn(state: ExactState) -> tuple[str, int | ExactState | None]:
    enemy_hp = state.enemy_hp
    poison = state.poison
    vulnerable = max(0, state.vulnerable - 1)
    if poison > 0:
        enemy_hp -= poison
        poison = max(0, poison - 1)
    if enemy_hp <= 0:
        return ("kill", state.turn)
    if state.turn >= MAX_TURNS:
        return ("fail", None)

    attack = [10, 16, 22, 10][(state.turn - 1) % 4]
    attack += ((state.turn - 1) // 4) * 4
    player_hp = state.player_hp - max(0, attack - state.block)
    if player_hp <= 0:
        return ("fail", None)

    return ("next", replace(
        state,
        turn=state.turn + 1,
        enemy_hp=enemy_hp,
        player_hp=player_hp,
        poison=poison,
        vulnerable=vulnerable,
        hand=0,
        discard=state.discard | state.hand,
        block=0,
        actions_left=ACTION_LIMIT_PER_TURN,
    ))


def add_weight(target: dict[ExactState, float], state: ExactState, probability: float) -> None:
    target[state] = target.get(state, 0.0) + probability


def solve_loadout_policy(cards: tuple[str, ...], potions: tuple[str, ...], relic: str | None) -> dict[str, object]:
    exact_draw_outcomes.cache_clear()

    card_mask = mask_from_items(cards, CARD_INDEX)
    potion_mask = mask_from_items(potions, POTION_INDEX)
    upgraded_masks = [0]
    if relic == "Whetstone":
        attacks = tuple(card for card in cards if card in ATTACKS)
        upgraded_masks = [mask_from_items(combo, CARD_INDEX) for combo in itertools.combinations(attacks, min(2, len(attacks)))] or [0]

    kill_distribution = [0.0] * MAX_TURNS
    for upgraded in upgraded_masks:
        initial = ExactState(
            turn=1,
            enemy_hp=enemy_initial_hp(len(cards)),
            player_hp=80,
            energy=0,
            block=0,
            strength=0,
            vulnerable=1 if relic == "BagOfMarbles" else 0,
            poison=0,
            fumes=0,
            hand=0,
            draw=card_mask,
            discard=0,
            potions=potion_mask,
            relic=relic,
            akabeko_used=False,
            explosive_bonus=0,
            upgraded=upgraded,
            actions_left=ACTION_LIMIT_PER_TURN,
        )
        frontier: dict[ExactState, float] = {}
        for probability, state in policy_start_turn_outcomes(initial):
            add_weight(frontier, state, probability / len(upgraded_masks))

        while frontier:
            action_frontier: dict[ExactState, float] = frontier
            frontier = {}
            while action_frontier:
                next_action_frontier: dict[ExactState, float] = {}
                ended_states: dict[ExactState, float] = {}
                for state, state_probability in action_frontier.items():
                    if state.enemy_hp <= 0:
                        kill_distribution[state.turn - 1] += state_probability
                        continue
                    if state.player_hp <= 0:
                        continue
                    if state.actions_left <= 0:
                        add_weight(ended_states, state, state_probability)
                        continue

                    chosen_outcomes: tuple[tuple[float, ExactState], ...] = ()
                    for potion in priority_sorted_potions(state.potions):
                        chosen_outcomes = policy_potion_outcomes(state, potion)
                        if chosen_outcomes:
                            break
                    if not chosen_outcomes:
                        for card in priority_sorted_cards(state.hand):
                            chosen_outcomes = policy_card_outcomes(state, card)
                            if chosen_outcomes:
                                break

                    if not chosen_outcomes:
                        add_weight(ended_states, state, state_probability)
                        continue

                    for probability, next_state in chosen_outcomes:
                        add_weight(next_action_frontier, next_state, state_probability * probability)

                for state, state_probability in ended_states.items():
                    status, payload = policy_end_turn(state)
                    if status == "kill":
                        kill_distribution[int(payload) - 1] += state_probability
                    elif status == "next":
                        for probability, next_state in policy_start_turn_outcomes(payload):  # type: ignore[arg-type]
                            add_weight(frontier, next_state, state_probability * probability)
                action_frontier = next_action_frontier

    distribution = tuple(kill_distribution)
    success_probability = sum(distribution)
    best_turn = next((index + 1 for index, value in enumerate(distribution) if value > 1e-12), None)
    return {
        "successProbability": success_probability,
        "killTurnDistribution": distribution,
        "killTurn": best_turn,
    }


def exact_state_dominates(left: ExactState, right: ExactState) -> bool:
    if (
        left.turn != right.turn
        or left.hand != right.hand
        or left.draw != right.draw
        or left.discard != right.discard
        or left.potions != right.potions
        or left.relic != right.relic
        or left.akabeko_used != right.akabeko_used
        or left.explosive_bonus != right.explosive_bonus
        or left.upgraded != right.upgraded
    ):
        return False

    return (
        left.enemy_hp <= right.enemy_hp
        and left.player_hp >= right.player_hp
        and left.energy >= right.energy
        and left.block >= right.block
        and left.strength >= right.strength
        and left.vulnerable >= right.vulnerable
        and left.poison >= right.poison
        and left.fumes >= right.fumes
        and left.actions_left >= right.actions_left
    )


def prune_exact_states(states: Iterable[ExactState]) -> tuple[ExactState, ...]:
    kept: list[ExactState] = []
    ranked = sorted(
        set(states),
        key=lambda state: (
            state.enemy_hp,
            -state.player_hp,
            -state.energy,
            -state.block,
            -state.strength,
            -state.vulnerable,
            -state.poison,
            -state.fumes,
            -state.actions_left,
        ),
    )
    for state in ranked:
        if any(exact_state_dominates(other, state) for other in kept):
            continue
        kept.append(state)
    return tuple(kept)


def solve_loadout_random(cards: tuple[str, ...], potions: tuple[str, ...], relic: str | None) -> dict[str, object]:
    unsupported_cards = sorted(set(cards) & (DRAW_CARDS | {"Prepared"}))
    unsupported_potions = sorted(set(potions) & {"SwiftPotion", "GamblersBrew"})
    if unsupported_cards or unsupported_potions:
        raise RuntimeError(
            "The random solver expects no in-turn draw/discard resources. "
            f"unsupportedCards={unsupported_cards} unsupportedPotions={unsupported_potions}"
        )

    exact_draw_outcomes.cache_clear()

    card_mask = mask_from_items(cards, CARD_INDEX)
    potion_mask = mask_from_items(potions, POTION_INDEX)
    upgraded_masks = [0]
    if relic == "Whetstone":
        attacks = tuple(card for card in cards if card in ATTACKS)
        upgrade_count = min(2, len(attacks))
        upgraded_masks = [mask_from_items(combo, CARD_INDEX) for combo in itertools.combinations(attacks, upgrade_count)] or [0]

    def canonical_action_order(card_subset: int, potion_subset: int) -> tuple[tuple[str, str], ...]:
        actions: list[tuple[str, str]] = []

        for potion in ("EnergyPotion", "StrengthPotion", "PoisonPotion", "VulnerablePotion"):
            if potion in POTION_INDEX and potion_subset & (1 << POTION_INDEX[potion]):
                actions.append(("potion", potion))

        for card in ("Inflame", "Bloodletting", "NoxiousFumes", "DeadlyPoison", "BouncingFlask"):
            if card in CARD_INDEX and card_subset & (1 << CARD_INDEX[card]):
                actions.append(("card", card))

        for card in ("BeamCell", "Bash", "Thunderclap"):
            if card in CARD_INDEX and card_subset & (1 << CARD_INDEX[card]):
                actions.append(("card", card))

        if "ExplosiveAmpoule" in POTION_INDEX and potion_subset & (1 << POTION_INDEX["ExplosiveAmpoule"]):
            actions.append(("potion", "ExplosiveAmpoule"))

        for card in ("Bludgeon", "Hemokinesis", "TwinStrike", "PoisonedStab", "Slice", "StrikeIronclad", "DefendIronclad"):
            if card in CARD_INDEX and card_subset & (1 << CARD_INDEX[card]):
                actions.append(("card", card))

        if "FirePotion" in POTION_INDEX and potion_subset & (1 << POTION_INDEX["FirePotion"]):
            actions.append(("potion", "FirePotion"))

        used_cards = {name for action_type, name in actions if action_type == "card"}
        used_potions = {name for action_type, name in actions if action_type == "potion"}
        for card in priority_sorted_cards(card_subset):
            if card not in used_cards:
                actions.append(("card", card))
        for potion in priority_sorted_potions(potion_subset):
            if potion not in used_potions:
                actions.append(("potion", potion))
        return tuple(actions)

    def apply_canonical_subset(state: ExactState, card_subset: int, potion_subset: int) -> ExactState | None:
        next_state = state
        for action_type, name in canonical_action_order(card_subset, potion_subset):
            outcomes = exact_play_card_outcomes(next_state, name) if action_type == "card" else exact_use_potion_outcomes(next_state, name)
            if not outcomes:
                return None
            if len(outcomes) != 1 or outcomes[0][0] != 1.0:
                raise RuntimeError(f"Unexpected random action in random solver: {action_type}:{name}")
            next_state = outcomes[0][1]
        return next_state

    @lru_cache(maxsize=None)
    def turn_end_states(state: ExactState) -> tuple[ExactState, ...]:
        results: list[ExactState] = []
        for card_subset in all_submasks(state.hand):
            for potion_subset in all_submasks(state.potions):
                ended = apply_canonical_subset(state, card_subset, potion_subset)
                if ended is not None:
                    results.append(ended)
        return tuple(set(results))

    def resolve_enemy_turn(state: ExactState) -> tuple[str, int | ExactState | None]:
        enemy_hp = state.enemy_hp
        poison = state.poison
        vulnerable = max(0, state.vulnerable - 1)
        if poison > 0:
            enemy_hp -= poison
            poison = max(0, poison - 1)
        if enemy_hp <= 0:
            return ("kill", state.turn)
        if state.turn >= MAX_TURNS:
            return ("fail", None)

        attack = [10, 16, 22, 10][(state.turn - 1) % 4]
        attack += ((state.turn - 1) // 4) * 4
        player_hp = state.player_hp - max(0, attack - state.block)
        if player_hp <= 0:
            return ("fail", None)

        return ("next", replace(
            state,
            turn=state.turn + 1,
            enemy_hp=enemy_hp,
            player_hp=player_hp,
            poison=poison,
            vulnerable=vulnerable,
            hand=0,
            discard=state.discard | state.hand,
            block=0,
            actions_left=32,
        ))

    if MAX_TURNS == 2:
        @lru_cache(maxsize=None)
        def turn2_action_vec(state: ExactState) -> tuple[float, ...]:
            best = zero_vec()
            for ended_state in turn_end_states(state):
                status, payload = resolve_enemy_turn(ended_state)
                if status == "kill":
                    best = better_vec(best, kill_vec(int(payload)))
            return best

        @lru_cache(maxsize=None)
        def turn2_start_vec(state: ExactState) -> tuple[float, ...]:
            state = replace(state, energy=3, block=0, actions_left=32)
            if state.fumes:
                state = replace(state, poison=state.poison + state.fumes)

            result = zero_vec()
            for probability, hand, draw, discard in exact_draw_outcomes(state.hand, state.draw, state.discard, 5):
                drawn_state = replace(state, hand=hand, draw=draw, discard=discard)
                result = vec_add(result, turn2_action_vec(drawn_state), probability)
            return result

        distribution = zero_vec()
        for upgraded in upgraded_masks:
            initial = ExactState(
                turn=1,
                enemy_hp=enemy_initial_hp(len(cards)),
                player_hp=80,
                energy=3,
                block=0,
                strength=0,
                vulnerable=1 if relic == "BagOfMarbles" else 0,
                poison=0,
                fumes=0,
                hand=0,
                draw=card_mask,
                discard=0,
                potions=potion_mask,
                relic=relic,
                akabeko_used=False,
                explosive_bonus=0,
                upgraded=upgraded,
                actions_left=32,
            )
            draw_count = 5 + (2 if relic == "BagOfPreparation" else 0)
            upgraded_distribution = zero_vec()
            for probability, hand, draw, discard in exact_draw_outcomes(initial.hand, initial.draw, initial.discard, draw_count):
                drawn_state = replace(initial, hand=hand, draw=draw, discard=discard)
                best = zero_vec()
                for ended_state in turn_end_states(drawn_state):
                    status, payload = resolve_enemy_turn(ended_state)
                    if status == "kill":
                        candidate = kill_vec(int(payload))
                    elif status == "next":
                        candidate = turn2_start_vec(payload)  # type: ignore[arg-type]
                    else:
                        candidate = zero_vec()
                    best = better_vec(best, candidate)
                upgraded_distribution = vec_add(upgraded_distribution, best, probability)
            distribution = vec_add(distribution, upgraded_distribution, 1.0 / len(upgraded_masks))

        success_probability = vec_total(distribution)
        best_turn = next((index + 1 for index, value in enumerate(distribution) if value > 1e-12), None)
        return {
            "successProbability": success_probability,
            "killTurnDistribution": distribution,
            "killTurn": best_turn,
        }

    @lru_cache(maxsize=None)
    def start_turn_vec(state: ExactState) -> tuple[float, ...]:
        energy = 3 + (1 if state.relic == "HappyFlower" and state.turn % 3 == 0 else 0)
        state = replace(state, energy=energy, block=0, actions_left=32)
        if state.fumes:
            state = replace(state, poison=state.poison + state.fumes)

        draw_count = 5 + (2 if state.turn == 1 and state.relic == "BagOfPreparation" else 0)
        result = zero_vec()
        for probability, hand, draw, discard in exact_draw_outcomes(state.hand, state.draw, state.discard, draw_count):
            drawn_state = replace(state, hand=hand, draw=draw, discard=discard)
            result = vec_add(result, action_vec(drawn_state), probability)
        return result

    @lru_cache(maxsize=None)
    def end_turn_vec(state: ExactState) -> tuple[float, ...]:
        enemy_hp = state.enemy_hp
        poison = state.poison
        vulnerable = max(0, state.vulnerable - 1)
        if poison > 0:
            enemy_hp -= poison
            poison = max(0, poison - 1)
        if enemy_hp <= 0:
            return kill_vec(state.turn)
        if state.turn >= MAX_TURNS:
            return zero_vec()

        attack = [10, 16, 22, 10][(state.turn - 1) % 4]
        attack += ((state.turn - 1) // 4) * 4
        player_hp = state.player_hp - max(0, attack - state.block)
        if player_hp <= 0:
            return zero_vec()

        next_state = replace(
            state,
            turn=state.turn + 1,
            enemy_hp=enemy_hp,
            player_hp=player_hp,
            poison=poison,
            vulnerable=vulnerable,
            hand=0,
            discard=state.discard | state.hand,
            block=0,
            actions_left=32,
        )
        return start_turn_vec(next_state)

    @lru_cache(maxsize=None)
    def action_vec(state: ExactState) -> tuple[float, ...]:
        if state.enemy_hp <= 0:
            return kill_vec(state.turn)
        if state.player_hp <= 0 or state.turn > MAX_TURNS:
            return zero_vec()

        best = zero_vec()
        for ended_state in turn_end_states(state):
            candidate = kill_vec(ended_state.turn) if ended_state.enemy_hp <= 0 else end_turn_vec(ended_state)
            best = better_vec(best, candidate)
        return best

    distribution = zero_vec()
    for upgraded in upgraded_masks:
        initial = ExactState(
            turn=1,
            enemy_hp=enemy_initial_hp(len(cards)),
            player_hp=80,
            energy=0,
            block=0,
            strength=0,
            vulnerable=1 if relic == "BagOfMarbles" else 0,
            poison=0,
            fumes=0,
            hand=0,
            draw=card_mask,
            discard=0,
            potions=potion_mask,
            relic=relic,
            akabeko_used=False,
            explosive_bonus=0,
            upgraded=upgraded,
            actions_left=32,
        )
        distribution = vec_add(distribution, start_turn_vec(initial), 1.0 / len(upgraded_masks))

    success_probability = vec_total(distribution)
    best_turn = next((index + 1 for index, value in enumerate(distribution) if value > 1e-12), None)
    return {
        "successProbability": success_probability,
        "killTurnDistribution": distribution,
        "killTurn": best_turn,
    }


def useful_actions(state: State) -> Iterable[tuple[str, str]]:
    for potion in sorted(state.potions, key=lambda p: POTION_PRIORITY.get(p, 0), reverse=True):
        yield ("potion", potion)
    for card in sorted(state.hand, key=lambda c: CARD_PRIORITY.get(c, 0), reverse=True):
        yield ("card", card)


def dominates(a: State, b: State) -> bool:
    return (
        a.enemy_hp <= b.enemy_hp
        and a.player_hp >= b.player_hp
        and a.energy >= b.energy
        and a.block >= b.block
        and a.strength >= b.strength
        and a.vulnerable >= b.vulnerable
        and a.poison >= b.poison
        and set(a.hand).issuperset(b.hand)
        and set(a.potions).issuperset(b.potions)
    )


def compress(states: Iterable[State], beam: int) -> list[State]:
    kept: list[State] = []
    ranked = sorted(
        states,
        key=lambda s: (
            s.enemy_hp,
            -s.player_hp,
            -s.energy,
            -s.strength,
            -s.vulnerable,
            -s.poison,
            -len(s.hand),
            -len(s.potions),
        ),
    )
    for state in ranked:
        if any(dominates(other, state) for other in kept):
            continue
        kept.append(state)
        if len(kept) >= beam:
            break
    return kept


def solve_loadout(cards: tuple[str, ...], potions: tuple[str, ...], relic: str | None, beam: int) -> int | None:
    upgraded_sets = [()]
    if relic == "Whetstone":
        attacks = tuple(card for card in cards if card in ATTACKS)
        upgraded_sets = list(itertools.combinations(attacks, min(2, len(attacks)))) or [()]

    best_turn: int | None = None
    for upgraded in upgraded_sets:
        draw = tuple(sorted(cards, key=lambda c: CARD_PRIORITY.get(c, 0), reverse=True))
        initial = State(
            turn=1,
            enemy_hp=enemy_initial_hp(len(cards)),
            player_hp=80,
            energy=0,
            block=0,
            strength=0,
            vulnerable=1 if relic == "BagOfMarbles" else 0,
            poison=0,
            fumes=0,
            hand=(),
            draw=draw,
            discard=(),
            potions=tuple(sorted(potions, key=lambda p: POTION_PRIORITY.get(p, 0), reverse=True)),
            relic=relic,
            akabeko_used=False,
            explosive_bonus=0,
            upgraded=tuple(upgraded),
        )
        frontier = [initial]
        for _ in range(MAX_TURNS):
            turn_states = [start_turn(state) for state in frontier if state.turn <= MAX_TURNS]
            action_frontier = compress(turn_states, beam)
            for _step in range(20):
                next_states = list(action_frontier)
                for state in action_frontier:
                    if state.enemy_hp <= 0:
                        best_turn = state.turn if best_turn is None else min(best_turn, state.turn)
                        continue
                    for action_type, name in useful_actions(state):
                        new_state = play_card(state, name) if action_type == "card" else use_potion(state, name)
                        if new_state is None:
                            continue
                        if new_state.enemy_hp <= 0:
                            best_turn = new_state.turn if best_turn is None else min(best_turn, new_state.turn)
                        else:
                            next_states.append(new_state)
                compressed = compress(next_states, beam)
                if compressed == action_frontier:
                    break
                action_frontier = compressed

            if best_turn is not None:
                break
            frontier = [ended for state in action_frontier if (ended := end_turn(state)) is not None]
            if not frontier:
                break
    return best_turn


def solve_loadout_fast(cards: tuple[str, ...], potions: tuple[str, ...], relic: str | None) -> int | None:
    upgraded = ()
    if relic == "Whetstone":
        attacks = sorted((card for card in cards if card in ATTACKS), key=lambda c: CARD_PRIORITY.get(c, 0), reverse=True)
        upgraded = tuple(attacks[:2])

    state = State(
        turn=1,
        enemy_hp=enemy_initial_hp(len(cards)),
        player_hp=80,
        energy=0,
        block=0,
        strength=0,
        vulnerable=1 if relic == "BagOfMarbles" else 0,
        poison=0,
        fumes=0,
        hand=(),
        draw=tuple(sorted(cards, key=lambda c: CARD_PRIORITY.get(c, 0), reverse=True)),
        discard=(),
        potions=tuple(sorted(potions, key=lambda p: POTION_PRIORITY.get(p, 0), reverse=True)),
        relic=relic,
        akabeko_used=False,
        explosive_bonus=0,
        upgraded=upgraded,
    )

    while state.turn <= MAX_TURNS:
        state = start_turn(state)
        for _ in range(32):
            if state.enemy_hp <= 0:
                return state.turn
            played = False
            for action_type, name in useful_actions(state):
                new_state = play_card(state, name) if action_type == "card" else use_potion(state, name)
                if new_state is not None:
                    state = new_state
                    played = True
                    break
            if not played:
                break
        if state.enemy_hp <= 0:
            return state.turn
        ended = end_turn(state)
        if ended is None:
            return None
        state = ended
    return None


def upper_bound_damage(cards: tuple[str, ...], potions: tuple[str, ...], relic: str | None) -> int:
    damage = 0
    strength = 2 if "Inflame" in cards else 0
    if "StrengthPotion" in potions:
        strength += 2
    if relic == "RedSkull":
        strength += 3
    vulnerable_bonus = 1.5 if ("BeamCell" in cards or "Bash" in cards or "Thunderclap" in cards or "VulnerablePotion" in potions or relic == "BagOfMarbles") else 1
    for card in cards:
        if card == "BeamCell":
            damage += int((3 + strength) * vulnerable_bonus)
        elif card == "Thunderclap":
            damage += int((4 + strength) * vulnerable_bonus)
        elif card == "Bludgeon":
            damage += int((32 + strength) * vulnerable_bonus)
        elif card == "Bash":
            damage += int((8 + strength) * vulnerable_bonus)
        elif card == "Hemokinesis":
            damage += int((15 + strength) * vulnerable_bonus)
        elif card == "TwinStrike":
            damage += int((5 + strength) * vulnerable_bonus) * 2
        elif card == "PommelStrike":
            damage += int((9 + strength) * vulnerable_bonus)
        elif card == "FlashOfSteel":
            damage += int((5 + strength) * vulnerable_bonus)
        elif card == "Slice":
            damage += int((6 + strength) * vulnerable_bonus)
        elif card == "PoisonedStab":
            damage += int((6 + strength) * vulnerable_bonus) + 12
        elif card == "DeadlyPoison":
            damage += 20
        elif card == "BouncingFlask":
            damage += 36
        elif card == "NoxiousFumes":
            damage += 20
    if "PoisonPotion" in potions:
        damage += 48
    if "FirePotion" in potions:
        damage += 20
    if "ExplosiveAmpoule" in potions:
        damage += int(10 * vulnerable_bonus)
    if relic == "Akabeko":
        damage += int(8 * vulnerable_bonus)
    if relic == "Whetstone":
        damage += 20
    return damage


def combinations_upto(items: tuple[str, ...], max_count: int, min_count: int = 0) -> Iterable[tuple[str, ...]]:
    for count in range(min_count, max_count + 1):
        yield from itertools.combinations(items, count)


def zh_list(ids: Iterable[str], names: dict[str, str]) -> list[str]:
    return [names[item] for item in ids]


def main() -> int:
    global BASE_HP, THIN_DECK_MIN_CARDS, THIN_DECK_HP_PER_MISSING
    parser = argparse.ArgumentParser()
    parser.add_argument("--game-dir", type=Path, default=GAME_DIR)
    parser.add_argument("--base-hp", type=int, default=BASE_HP)
    parser.add_argument("--thin-min-cards", type=int, default=THIN_DECK_MIN_CARDS)
    parser.add_argument("--thin-hp-per-missing", type=int, default=THIN_DECK_HP_PER_MISSING)
    parser.add_argument("--min-cards", type=int, default=0)
    parser.add_argument("--max-cards", type=int, default=MAX_CARDS)
    parser.add_argument("--beam", type=int, default=80)
    parser.add_argument("--solver", choices=("random", "policy", "exact", "fast", "beam"), default="random")
    parser.add_argument("--deep", action="store_true", help="Deprecated alias for --solver beam.")
    parser.add_argument("--min-success-prob", type=float, default=0.0)
    parser.add_argument("--json", action="store_true")
    parser.add_argument("--limit", type=int, default=0, help="Stop after N viable loadouts; 0 means no limit.")
    args = parser.parse_args()
    if args.deep:
        args.solver = "beam"

    BASE_HP = args.base_hp
    THIN_DECK_MIN_CARDS = args.thin_min_cards
    THIN_DECK_HP_PER_MISSING = args.thin_hp_per_missing

    verify_resources(args.game_dir)

    viable: list[dict[str, object]] = []
    checked = 0
    pruned = 0
    potion_sets = list(combinations_upto(POTION_POOL, MAX_POTIONS))
    relic_sets = [(None,)] + [(relic,) for relic in RELIC_POOL]

    for cards in combinations_upto(CARD_POOL, args.max_cards, args.min_cards):
        hp = enemy_initial_hp(len(cards))
        if upper_bound_damage(cards, POTION_POOL, "Whetstone") < hp:
            pruned += len(potion_sets) * len(relic_sets)
            continue
        for potions in potion_sets:
            for relic_tuple in relic_sets:
                relic = relic_tuple[0]
                checked += 1
                if upper_bound_damage(cards, potions, relic) < hp:
                    pruned += 1
                    continue
                success_probability: float | None = None
                kill_distribution: tuple[float, ...] | None = None
                if args.solver in {"exact", "policy", "random"}:
                    if args.solver == "exact":
                        exact_result = solve_loadout_exact(cards, potions, relic)
                    elif args.solver == "policy":
                        exact_result = solve_loadout_policy(cards, potions, relic)
                    else:
                        exact_result = solve_loadout_random(cards, potions, relic)
                    success_probability = float(exact_result["successProbability"])
                    kill_distribution = tuple(float(value) for value in exact_result["killTurnDistribution"])
                    kill_turn = exact_result["killTurn"]
                    if success_probability <= args.min_success_prob or kill_turn is None:
                        continue
                elif args.solver == "beam":
                    kill_turn = solve_loadout(cards, potions, relic, args.beam)
                    if kill_turn is None:
                        continue
                else:
                    kill_turn = solve_loadout_fast(cards, potions, relic)
                    if kill_turn is None:
                        continue
                viable.append(
                    {
                        "killTurn": kill_turn,
                        "enemyHp": hp,
                        "successProbability": success_probability,
                        "killTurnDistribution": kill_distribution,
                        "cards": list(cards),
                        "cardNames": zh_list(cards, CARD_NAMES),
                        "potions": list(potions),
                        "potionNames": zh_list(potions, POTION_NAMES),
                        "relic": relic,
                        "relicName": None if relic is None else RELIC_NAMES[relic],
                    }
                )
                if args.limit and len(viable) >= args.limit:
                    break
            if args.limit and len(viable) >= args.limit:
                break
        if args.limit and len(viable) >= args.limit:
            break

    viable.sort(key=lambda item: (
        item["killTurn"],
        -(item["successProbability"] or 0.0),
        item["enemyHp"],
        len(item["cards"]),
        item["relicName"] or "",
        ",".join(item["potions"]),
    ))
    if args.json:
        print(json.dumps({"checked": checked, "pruned": pruned, "viable": viable}, ensure_ascii=False, indent=2))
    else:
        print(f"resourceVerification=ok")
        print(f"checked={checked} pruned={pruned} viable={len(viable)} solver={args.solver}")
        by_turn: dict[int, int] = {}
        for item in viable:
            by_turn[item["killTurn"]] = by_turn.get(item["killTurn"], 0) + 1
        print("byTurn=" + ", ".join(f"{turn}:{count}" for turn, count in sorted(by_turn.items())))
        for index, item in enumerate(viable, start=1):
            cards = "、".join(item["cardNames"])
            potions = "、".join(item["potionNames"]) if item["potionNames"] else "无"
            relic = item["relicName"] or "无"
            probability = item["successProbability"]
            probability_text = "" if probability is None else f" P={probability:.6%}"
            distribution = item["killTurnDistribution"]
            distribution_text = ""
            if distribution:
                distribution_text = " dist=" + ",".join(f"T{turn}:{value:.6%}" for turn, value in enumerate(distribution, start=1) if value > 1e-12)
            print(f"[{index}] T{item['killTurn']} HP{item['enemyHp']}{probability_text}{distribution_text} | 药水={potions} | 遗物={relic} | 卡牌={cards}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
