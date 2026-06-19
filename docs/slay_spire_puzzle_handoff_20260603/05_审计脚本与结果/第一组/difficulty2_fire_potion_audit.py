from __future__ import annotations

from collections import Counter, defaultdict
from dataclasses import dataclass
from functools import lru_cache
from itertools import combinations
from math import comb
import json


CARD_COST = {
    "Strike": 1,
    "Defend": 1,
    "Bash": 2,
    "Neutralize": 0,
    "Ball Lightning": 1,
    "Survivor": 1,
    "Quick Slash": 1,
}

CARD_CN = {
    "Strike": "打击",
    "Defend": "防御",
    "Bash": "重击",
    "Neutralize": "中和",
    "Ball Lightning": "球状闪电",
    "Survivor": "生存者",
    "Quick Slash": "快斩",
}

ORDER = ("Bash", "Neutralize", "Ball Lightning", "Survivor", "Defend", "Quick Slash", "Strike")


@dataclass(frozen=True)
class Variant:
    name: str
    card_pool: tuple[str, ...]
    enemy_hp: int
    player_hp: int
    enemy_damage_by_turn: tuple[int, ...]
    min_deck_size: int
    max_deck_size: int
    draw_per_turn: int = 5


def display(deck: tuple[str, ...]) -> str:
    counts = Counter(deck)
    parts = []
    for card in ORDER:
        n = counts[card]
        if n == 1:
            parts.append(CARD_CN[card])
        elif n > 1:
            parts.append(f"{CARD_CN[card]} x{n}")
    return "、".join(parts)


def unique_decks(pool: tuple[str, ...], min_size: int, max_size: int) -> list[tuple[str, ...]]:
    decks: set[tuple[str, ...]] = set()
    for size in range(min_size, max_size + 1):
        for pick in combinations(range(len(pool)), size):
            decks.add(tuple(sorted(pool[i] for i in pick)))
    return sorted(decks)


def attack_damage(base: int, vulnerable: int) -> int:
    return (base * 3) // 2 if vulnerable > 0 else base


def weak_damage(base: int, weak: int) -> int:
    return (base * 3) // 4 if weak > 0 else base


@lru_cache(maxsize=None)
def draw_outcomes(cards: tuple[str, ...], count: int) -> tuple[tuple[tuple[str, ...], tuple[str, ...], float], ...]:
    if count <= 0:
        return (((), tuple(sorted(cards)), 1.0),)
    if count >= len(cards):
        return ((tuple(sorted(cards)), (), 1.0),)
    total = comb(len(cards), count)
    merged: dict[tuple[tuple[str, ...], tuple[str, ...]], float] = {}
    for pick in combinations(range(len(cards)), count):
        chosen = tuple(sorted(cards[i] for i in pick))
        pick_set = set(pick)
        remaining = tuple(sorted(cards[i] for i in range(len(cards)) if i not in pick_set))
        merged[(chosen, remaining)] = merged.get((chosen, remaining), 0.0) + 1 / total
    return tuple((hand, rest, prob) for (hand, rest), prob in merged.items())


@lru_cache(maxsize=None)
def next_turn_draws(draw_pile: tuple[str, ...], discard_pile: tuple[str, ...], hand_size: int):
    if len(draw_pile) >= hand_size:
        return tuple((hand, next_draw, discard_pile, prob) for hand, next_draw, prob in draw_outcomes(draw_pile, hand_size))
    fixed = draw_pile
    need = hand_size - len(draw_pile)
    if not discard_pile:
        return ((tuple(sorted(fixed)), (), (), 1.0),)
    return tuple((tuple(sorted(fixed + redraw)), next_draw, (), prob) for redraw, next_draw, prob in draw_outcomes(discard_pile, need))


def exact_result(deck: tuple[str, ...], variant: Variant) -> tuple[float, ...]:
    deck = tuple(sorted(deck))
    max_turn = len(variant.enemy_damage_by_turn)
    hand_size = min(variant.draw_per_turn, len(deck))

    @lru_cache(maxsize=None)
    def value(turn, hand, draw_pile, discard_pile, hp, enemy_hp, vulnerable, weak, potion_used):
        return in_turn_best(turn, hand, draw_pile, discard_pile, 3, hp, enemy_hp, vulnerable, weak, 0, potion_used)

    @lru_cache(maxsize=None)
    def end_turn(turn, hand, draw_pile, discard_pile, hp, enemy_hp, vulnerable, weak, block, potion_used):
        vec = [0.0] * (max_turn + 1)
        if enemy_hp <= 0:
            vec[turn - 1] = 1.0
            return tuple(vec)
        incoming = weak_damage(variant.enemy_damage_by_turn[turn - 1], weak)
        hp_after = hp - max(0, incoming - block)
        if turn >= max_turn or hp_after <= 0:
            vec[-1] = 1.0
            return tuple(vec)
        discard_after = tuple(sorted(discard_pile + hand))
        merged = [0.0] * (max_turn + 1)
        for next_hand, next_draw, next_discard, prob in next_turn_draws(draw_pile, discard_after, hand_size):
            future = value(turn + 1, next_hand, next_draw, next_discard, hp_after, enemy_hp, max(0, vulnerable - 1), max(0, weak - 1), potion_used)
            for idx, p in enumerate(future):
                merged[idx] += prob * p
        return tuple(merged)

    @lru_cache(maxsize=None)
    def in_turn_best(turn, hand, draw_pile, discard_pile, energy, hp, enemy_hp, vulnerable, weak, block, potion_used):
        vec = [0.0] * (max_turn + 1)
        if hp <= 0:
            vec[-1] = 1.0
            return tuple(vec)
        if enemy_hp <= 0:
            vec[turn - 1] = 1.0
            return tuple(vec)
        best = end_turn(turn, hand, draw_pile, discard_pile, hp, enemy_hp, vulnerable, weak, block, potion_used)

        if not potion_used:
            candidate = in_turn_best(turn, hand, draw_pile, discard_pile, energy, hp, enemy_hp - 20, vulnerable, weak, block, True)
            if candidate[:-1] > best[:-1]:
                best = candidate

        for card in tuple(sorted(set(hand))):
            cost = CARD_COST[card]
            if cost > energy:
                continue
            next_hand_list = list(hand)
            next_hand_list.remove(card)
            next_hand = tuple(sorted(next_hand_list))
            next_energy = energy - cost
            next_enemy_hp = enemy_hp
            next_vulnerable = vulnerable
            next_weak = weak
            next_block = block
            if card == "Strike":
                next_enemy_hp -= attack_damage(6, vulnerable)
            elif card == "Defend":
                next_block += 5
            elif card == "Bash":
                next_enemy_hp -= attack_damage(8, vulnerable)
                next_vulnerable += 2
            elif card == "Neutralize":
                next_enemy_hp -= attack_damage(3, vulnerable)
                next_weak += 1
            elif card == "Ball Lightning":
                next_enemy_hp -= attack_damage(7, vulnerable)
            elif card == "Survivor":
                next_block += 8
            elif card == "Quick Slash":
                next_enemy_hp -= attack_damage(8, vulnerable)
            candidate = in_turn_best(turn, next_hand, draw_pile, tuple(sorted(discard_pile + (card,))), next_energy, hp, next_enemy_hp, next_vulnerable, next_weak, next_block, potion_used)
            if candidate[:-1] > best[:-1]:
                best = candidate
        return best

    total = [0.0] * (max_turn + 1)
    for hand, draw_pile, prob in draw_outcomes(deck, hand_size):
        result = value(1, hand, draw_pile, (), variant.player_hp, variant.enemy_hp, 0, 0, False)
        for idx, p in enumerate(result):
            total[idx] += prob * p
    return tuple(total)


def first_turn(result: tuple[float, ...]) -> int | None:
    for idx, p in enumerate(result[:-1], 1):
        if p > 1e-9:
            return idx
    return None


def run(enemy_hp=66, player_hp=12, damage=(0, 11, 15, 18)) -> dict:
    pool = (
        "Strike",
        "Strike",
        "Strike",
        "Defend",
        "Defend",
        "Bash",
        "Neutralize",
        "Ball Lightning",
        "Ball Lightning",
        "Survivor",
        "Quick Slash",
        "Quick Slash",
    )
    variant = Variant("difficulty2_fire_potion", pool, enemy_hp, player_hp, damage, 7, 7)
    rows = []
    decks = unique_decks(pool, variant.min_deck_size, variant.max_deck_size)
    for deck in decks:
        result = exact_result(deck, variant)
        rows.append({
            "deck": list(deck),
            "deck_display": display(deck),
            "first_turn": first_turn(result),
            "result": result,
            "success": sum(result[:-1]),
            "fail": result[-1],
        })
    rows.sort(key=lambda row: (row["success"], -(row["first_turn"] or 99)), reverse=True)
    by_turn: dict[str, list[dict]] = defaultdict(list)
    for row in rows:
        by_turn[str(row["first_turn"])].append(row)
    summary = {
        "variant": {
            "name": variant.name,
            "card_pool": list(pool),
            "enemy_hp": enemy_hp,
            "player_hp": player_hp,
            "enemy_damage_by_turn": list(damage),
            "deck_size": [7, 7],
            "draw_per_turn": 5,
            "potion": "Fire Potion: deal 20 damage once, no energy.",
        },
        "legal_deck_count": len(rows),
        "perfect_success_count": sum(1 for row in rows if row["success"] > 0.999999),
        "top20": rows[:20],
        "best_by_turn": {turn: vals[:10] for turn, vals in by_turn.items()},
    }
    return summary


def main() -> None:
    summary = run()
    with open("difficulty2_fire_potion_audit.json", "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)
    print("legal_deck_count", summary["legal_deck_count"])
    print("perfect_success_count", summary["perfect_success_count"])
    print("top20")
    for row in summary["top20"]:
        print(f"{row['success'] * 100:.4f}%", "first", row["first_turn"], [round(p * 100, 4) for p in row["result"]], row["deck_display"])
    print("best_by_turn")
    for turn in sorted(summary["best_by_turn"], key=lambda x: 99 if x == "None" else int(x)):
        row = summary["best_by_turn"][turn][0]
        print(turn, f"{row['success'] * 100:.4f}%", [round(p * 100, 4) for p in row["result"]], row["deck_display"])


if __name__ == "__main__":
    main()
