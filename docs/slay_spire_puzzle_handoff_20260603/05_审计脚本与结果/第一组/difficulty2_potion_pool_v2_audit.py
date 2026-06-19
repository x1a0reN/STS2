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
    "Ball Lightning*": 1,
    "Survivor": 1,
    "Quick Slash*": 1,
    "Dagger Throw*": 1,
    "Clothesline": 2,
    "Steam Barrier": 0,
}

CARD_CN = {
    "Strike": "打击",
    "Defend": "防御",
    "Bash": "重击",
    "Neutralize": "中和",
    "Ball Lightning*": "球状闪电（改）",
    "Survivor": "生存者",
    "Quick Slash*": "快斩（改）",
    "Dagger Throw*": "投掷匕首（改）",
    "Clothesline": "金刚臂",
    "Steam Barrier": "蒸汽屏障",
}

CARD_ORDER = (
    "Bash",
    "Clothesline",
    "Neutralize",
    "Ball Lightning*",
    "Dagger Throw*",
    "Quick Slash*",
    "Strike",
    "Survivor",
    "Defend",
    "Steam Barrier",
)

PLAY_ORDER = (
    "Bash",
    "Clothesline",
    "Neutralize",
    "Dagger Throw*",
    "Quick Slash*",
    "Ball Lightning*",
    "Strike",
    "Survivor",
    "Defend",
    "Steam Barrier",
)

POTION_CN = {
    "Fire": "火焰药水",
    "Vulnerable": "破甲药水",
    "Weak": "虚弱药水",
}


@dataclass(frozen=True)
class Variant:
    name: str
    card_pool: tuple[str, ...]
    potion_pool: tuple[str, ...]
    enemy_hp: int
    player_hp: int
    enemy_damage_by_turn: tuple[int, ...]
    enemy_armor_gain_by_turn: tuple[int, ...]
    enemy_heal_by_turn: tuple[int, ...]
    min_deck_size: int
    max_deck_size: int
    draw_per_turn: int = 5
    energy: int = 3


def display_deck(deck: tuple[str, ...]) -> str:
    counts = Counter(deck)
    parts = []
    for card in CARD_ORDER:
        n = counts[card]
        if n == 1:
            parts.append(CARD_CN[card])
        elif n > 1:
            parts.append(f"{CARD_CN[card]} ×{n}")
    return "、".join(parts)


def display_build(deck: tuple[str, ...], potion: str) -> str:
    return f"{display_deck(deck)}；{POTION_CN[potion]}"


def unique_decks(pool: tuple[str, ...], min_size: int, max_size: int) -> list[tuple[str, ...]]:
    decks: set[tuple[str, ...]] = set()
    for size in range(min_size, max_size + 1):
        for pick in combinations(range(len(pool)), size):
            decks.add(tuple(sorted(pool[i] for i in pick)))
    return sorted(decks)


def deal_damage(enemy_hp: int, armor: int, amount: int) -> tuple[int, int]:
    absorbed = min(armor, amount)
    armor -= absorbed
    enemy_hp -= amount - absorbed
    return enemy_hp, armor


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


@lru_cache(maxsize=None)
def playable_subsets(hand: tuple[str, ...], energy: int) -> tuple[tuple[str, ...], ...]:
    out: set[tuple[str, ...]] = {()}
    indices = range(len(hand))
    for size in range(1, len(hand) + 1):
        for pick in combinations(indices, size):
            subset = tuple(sorted(hand[i] for i in pick))
            if sum(CARD_COST[c] for c in subset) <= energy:
                out.add(subset)
    maximal = []
    for subset in out:
        counts = Counter(subset)
        cost = sum(CARD_COST[c] for c in subset)
        can_add = False
        for card in hand:
            if counts[card] < hand.count(card) and cost + CARD_COST[card] <= energy:
                can_add = True
                break
        if not can_add:
            maximal.append(subset)
    return tuple(sorted(maximal))


def remove_subset(hand: tuple[str, ...], subset: tuple[str, ...]) -> tuple[str, ...]:
    remaining = list(hand)
    for card in subset:
        remaining.remove(card)
    return tuple(sorted(remaining))


def canonical_resolve(
    subset: tuple[str, ...],
    enemy_hp: int,
    enemy_armor: int,
    vulnerable: int,
    weak: int,
    block: int,
) -> tuple[int, int, int, int, int]:
    counts = Counter(subset)
    for card in PLAY_ORDER:
        for _ in range(counts[card]):
            if card == "Strike":
                enemy_hp, enemy_armor = deal_damage(enemy_hp, enemy_armor, attack_damage(6, vulnerable))
            elif card == "Defend":
                block += 5
            elif card == "Bash":
                enemy_hp, enemy_armor = deal_damage(enemy_hp, enemy_armor, attack_damage(8, vulnerable))
                vulnerable += 2
            elif card == "Neutralize":
                enemy_hp, enemy_armor = deal_damage(enemy_hp, enemy_armor, attack_damage(3, vulnerable))
                weak += 1
            elif card == "Ball Lightning*":
                enemy_hp, enemy_armor = deal_damage(enemy_hp, enemy_armor, attack_damage(7, vulnerable))
            elif card == "Survivor":
                block += 8
            elif card == "Quick Slash*":
                enemy_hp, enemy_armor = deal_damage(enemy_hp, enemy_armor, attack_damage(8, vulnerable))
            elif card == "Dagger Throw*":
                enemy_hp, enemy_armor = deal_damage(enemy_hp, enemy_armor, attack_damage(9, vulnerable))
            elif card == "Clothesline":
                enemy_hp, enemy_armor = deal_damage(enemy_hp, enemy_armor, attack_damage(12, vulnerable))
                weak += 2
            elif card == "Steam Barrier":
                block += 6
    return enemy_hp, enemy_armor, vulnerable, weak, block


def apply_potion(
    potion: str,
    enemy_hp: int,
    enemy_armor: int,
    vulnerable: int,
    weak: int,
) -> tuple[int, int, int, int]:
    if potion == "Fire":
        enemy_hp, enemy_armor = deal_damage(enemy_hp, enemy_armor, 20)
    elif potion == "Vulnerable":
        vulnerable += 2
    elif potion == "Weak":
        weak += 2
    else:
        raise ValueError(potion)
    return enemy_hp, enemy_armor, vulnerable, weak


@lru_cache(maxsize=None)
def state_value(
    potion: str,
    enemy_max_hp: int,
    damage: tuple[int, ...],
    armor_gain: tuple[int, ...],
    heal: tuple[int, ...],
    energy: int,
    hand_size: int,
    turn: int,
    hand: tuple[str, ...],
    draw_pile: tuple[str, ...],
    discard_pile: tuple[str, ...],
    hp: int,
    enemy_hp: int,
    enemy_armor: int,
    vulnerable: int,
    weak: int,
    potion_used: bool,
) -> tuple[float, ...]:
    max_turn = len(damage)
    vec = [0.0] * (max_turn + 1)
    if hp <= 0:
        vec[-1] = 1.0
        return tuple(vec)
    if enemy_hp <= 0:
        vec[turn - 1] = 1.0
        return tuple(vec)

    best = None
    discard_after_turn_base = tuple(sorted(discard_pile + hand))
    for use_potion in ((False, True) if not potion_used else (False,)):
        p_enemy_hp, p_enemy_armor, p_vulnerable, p_weak = enemy_hp, enemy_armor, vulnerable, weak
        if use_potion:
            p_enemy_hp, p_enemy_armor, p_vulnerable, p_weak = apply_potion(
                potion,
                p_enemy_hp,
                p_enemy_armor,
                p_vulnerable,
                p_weak,
            )
        next_potion_used = potion_used or use_potion
        for subset in playable_subsets(hand, energy):
            next_enemy_hp, next_enemy_armor, next_vulnerable, next_weak, block = canonical_resolve(
                subset,
                p_enemy_hp,
                p_enemy_armor,
                p_vulnerable,
                p_weak,
                0,
            )
            if next_enemy_hp <= 0:
                candidate = [0.0] * (max_turn + 1)
                candidate[turn - 1] = 1.0
                candidate = tuple(candidate)
            else:
                incoming = weak_damage(damage[turn - 1], next_weak)
                hp_after = hp - max(0, incoming - block)
                healed_enemy_hp = min(enemy_max_hp, next_enemy_hp + heal[turn - 1])
                armor_after = next_enemy_armor + armor_gain[turn - 1]
                if turn >= max_turn or hp_after <= 0:
                    candidate = [0.0] * (max_turn + 1)
                    candidate[-1] = 1.0
                    candidate = tuple(candidate)
                else:
                    merged = [0.0] * (max_turn + 1)
                    for next_hand, next_draw, next_discard, prob in next_turn_draws(draw_pile, discard_after_turn_base, hand_size):
                        future = state_value(
                            potion,
                            enemy_max_hp,
                            damage,
                            armor_gain,
                            heal,
                            energy,
                            hand_size,
                            turn + 1,
                            next_hand,
                            next_draw,
                            next_discard,
                            hp_after,
                            healed_enemy_hp,
                            armor_after,
                            max(0, next_vulnerable - 1),
                            max(0, next_weak - 1),
                            next_potion_used,
                        )
                        for idx, p in enumerate(future):
                            merged[idx] += prob * p
                    candidate = tuple(merged)
            if best is None or (sum(candidate[:-1]), candidate[:-1]) > (sum(best[:-1]), best[:-1]):
                best = candidate
    assert best is not None
    return best


def exact_result(deck: tuple[str, ...], potion: str, variant: Variant) -> tuple[float, ...]:
    deck = tuple(sorted(deck))
    hand_size = min(variant.draw_per_turn, len(deck))
    total = [0.0] * (len(variant.enemy_damage_by_turn) + 1)
    for hand, draw_pile, prob in draw_outcomes(deck, hand_size):
        result = state_value(
            potion,
            variant.enemy_hp,
            variant.enemy_damage_by_turn,
            variant.enemy_armor_gain_by_turn,
            variant.enemy_heal_by_turn,
            variant.energy,
            hand_size,
            1,
            hand,
            draw_pile,
            (),
            variant.player_hp,
            variant.enemy_hp,
            0,
            0,
            0,
            False,
        )
        for idx, p in enumerate(result):
            total[idx] += prob * p
    return tuple(total)


def first_turn(result: tuple[float, ...]) -> int | None:
    for idx, p in enumerate(result[:-1], 1):
        if p > 1e-9:
            return idx
    return None


def classify(row: dict) -> str:
    deck = Counter(row["deck"])
    potion = row["potion"]
    if potion == "Fire" and deck["Bash"] and deck["Quick Slash*"] >= 2:
        return "火焰快杀线"
    if potion == "Vulnerable" and deck["Clothesline"] and deck["Dagger Throw*"]:
        return "破甲压血线"
    if potion == "Weak" and deck["Survivor"] and deck["Neutralize"]:
        return "虚弱生存线"
    if deck["Defend"] >= 2 and deck["Survivor"]:
        return "过量防守陷阱"
    return "混合线"


def run(
    enemy_hp=72,
    player_hp=15,
    damage=(6, 20, 26, 30),
    armor_gain=(14, 0, 0, 0),
    heal=(0, 0, 0, 0),
    verbose=True,
) -> dict:
    pool = (
        "Strike",
        "Strike",
        "Strike",
        "Defend",
        "Defend",
        "Bash",
        "Neutralize",
        "Ball Lightning*",
        "Ball Lightning*",
        "Survivor",
        "Quick Slash*",
        "Quick Slash*",
        "Dagger Throw*",
        "Clothesline",
    )
    potions = ("Fire", "Vulnerable", "Weak")
    variant = Variant("difficulty2_potion_pool_v2", pool, potions, enemy_hp, player_hp, damage, armor_gain, heal, 6, 6)
    rows = []
    decks = unique_decks(pool, variant.min_deck_size, variant.max_deck_size)
    total = len(decks) * len(potions)
    index = 0
    for deck in decks:
        for potion in potions:
            index += 1
            result = exact_result(deck, potion, variant)
            row = {
                "deck": list(deck),
                "potion": potion,
                "build_display": display_build(deck, potion),
                "family": "",
                "first_turn": first_turn(result),
                "result": result,
                "success": sum(result[:-1]),
                "fail": result[-1],
            }
            row["family"] = classify(row)
            rows.append(row)
            if verbose and (index % 10 == 0 or index == total):
                print(f"audited {index}/{total}", flush=True)
    rows.sort(key=lambda row: (row["success"], -(row["first_turn"] or 99)), reverse=True)
    by_turn: dict[str, list[dict]] = defaultdict(list)
    by_family: dict[str, list[dict]] = defaultdict(list)
    by_potion: dict[str, list[dict]] = defaultdict(list)
    for row in rows:
        by_turn[str(row["first_turn"])].append(row)
        by_family[row["family"]].append(row)
        by_potion[row["potion"]].append(row)
    return {
        "variant": {
            "name": variant.name,
            "card_pool": list(pool),
            "potion_pool": list(potions),
            "enemy_hp": enemy_hp,
            "player_hp": player_hp,
            "enemy_damage_by_turn": list(damage),
            "enemy_armor_gain_by_turn": list(armor_gain),
            "enemy_heal_by_turn": list(heal),
            "deck_size": [6, 6],
            "draw_per_turn": 5,
            "energy": 3,
            "audit_note": "Deck and potion are selected before battle. Each hand enumerates all playable card subsets and the one remaining potion timing. In-turn resolution uses a fixed damage-optimal order for this controlled card set.",
        },
        "legal_deck_count": len(decks),
        "legal_build_count": len(rows),
        "perfect_success_count": sum(1 for row in rows if row["success"] > 0.999999),
        "top20": rows[:20],
        "best_by_turn": {turn: vals[:10] for turn, vals in by_turn.items()},
        "best_by_family": {family: vals[:10] for family, vals in by_family.items()},
        "best_by_potion": {potion: vals[:10] for potion, vals in by_potion.items()},
    }


def main() -> None:
    summary = run()
    with open("difficulty2_potion_pool_v2_audit.json", "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)
    print("legal_deck_count", summary["legal_deck_count"])
    print("legal_build_count", summary["legal_build_count"])
    print("perfect_success_count", summary["perfect_success_count"])
    print("top20")
    for row in summary["top20"]:
        print(f"{row['success'] * 100:.4f}%", "first", row["first_turn"], row["family"], [round(p * 100, 4) for p in row["result"]], row["build_display"])
    print("best_by_turn")
    for turn in sorted(summary["best_by_turn"], key=lambda x: 99 if x == "None" else int(x)):
        row = summary["best_by_turn"][turn][0]
        print(turn, f"{row['success'] * 100:.4f}%", row["family"], [round(p * 100, 4) for p in row["result"]], row["build_display"])
    print("best_by_potion")
    for potion, vals in summary["best_by_potion"].items():
        row = vals[0]
        print(POTION_CN[potion], f"{row['success'] * 100:.4f}%", "first", row["first_turn"], row["family"], row["build_display"])


if __name__ == "__main__":
    main()
