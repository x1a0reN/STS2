from __future__ import annotations

from collections import Counter, defaultdict
from functools import lru_cache
from itertools import combinations
from math import comb
import argparse
import json


POOL = (
    "Strike", "Strike",
    "Defend", "Defend",
    "Cut", "Cut",
    "Blood Price", "Blood Price",
    "Cauterize",
    "Suture",
    "Transfuse",
    "Safe Jab",
    "Heavy Slash",
    "False Blood",
)

POTIONS = ("Hemostasis", "Guard", "Rage")

CN = {
    "Strike": "打击",
    "Defend": "防御",
    "Cut": "裂创（改）",
    "Blood Price": "血价（改）",
    "Cauterize": "烙止（改）",
    "Suture": "缝合（改）",
    "Transfuse": "转伤斩（改）",
    "Safe Jab": "稳刺",
    "Heavy Slash": "重斩",
    "False Blood": "伪血印（改）",
}

POTION_CN = {
    "Hemostasis": "止血药水",
    "Guard": "护壳药水",
    "Rage": "狂血药水",
}

COST = {
    "Strike": 1,
    "Defend": 1,
    "Cut": 1,
    "Blood Price": 0,
    "Cauterize": 1,
    "Suture": 1,
    "Transfuse": 2,
    "Safe Jab": 1,
    "Heavy Slash": 2,
    "False Blood": 0,
}

ORDER = (
    "Blood Price",
    "Cut",
    "False Blood",
    "Suture",
    "Cauterize",
    "Transfuse",
    "Safe Jab",
    "Heavy Slash",
    "Strike",
    "Defend",
)

MAX_TURN = 5
DRAW = 5
ENERGY = 3


def unique_decks(size: int = 7) -> list[tuple[str, ...]]:
    out = set()
    for pick in combinations(range(len(POOL)), size):
        out.add(tuple(sorted(POOL[i] for i in pick)))
    return sorted(out)


def display(deck: tuple[str, ...]) -> str:
    c = Counter(deck)
    parts = []
    for card in ORDER:
        if c[card] == 1:
            parts.append(CN[card])
        elif c[card] > 1:
            parts.append(f"{CN[card]} x{c[card]}")
    return "、".join(parts)


def display_build(deck: tuple[str, ...], potion: str) -> str:
    return f"{display(deck)}；{POTION_CN[potion]}"


@lru_cache(maxsize=None)
def draw_outcomes(cards: tuple[str, ...], n: int):
    if n >= len(cards):
        return ((tuple(sorted(cards)), (), 1.0),)
    denom = comb(len(cards), n)
    merged = defaultdict(float)
    for pick in combinations(range(len(cards)), n):
        pset = set(pick)
        hand = tuple(sorted(cards[i] for i in pick))
        rest = tuple(sorted(cards[i] for i in range(len(cards)) if i not in pset))
        merged[(hand, rest)] += 1 / denom
    return tuple((h, r, p) for (h, r), p in merged.items())


@lru_cache(maxsize=None)
def next_draws(draw: tuple[str, ...], discard: tuple[str, ...], n: int):
    if len(draw) >= n:
        return tuple((h, r, discard, p) for h, r, p in draw_outcomes(draw, n))
    fixed = draw
    need = n - len(draw)
    if not discard:
        return ((tuple(sorted(fixed)), (), (), 1.0),)
    return tuple((tuple(sorted(fixed + h)), r, (), p) for h, r, p in draw_outcomes(discard, need))


def remove_one(hand: tuple[str, ...], card: str) -> tuple[str, ...]:
    x = list(hand)
    x.remove(card)
    return tuple(sorted(x))


def add_discard(discard: tuple[str, ...], card: str) -> tuple[str, ...]:
    return tuple(sorted(discard + (card,)))


def clamp_wound(x: int) -> int:
    return max(0, min(9, x))


def apply_card(card: str, enemy: int, block: int, wound: int) -> tuple[int, int, int]:
    if card == "Strike":
        enemy -= 6
    elif card == "Defend":
        block += 5
    elif card == "Cut":
        enemy -= 5
        wound += 1
    elif card == "Blood Price":
        enemy -= 4
        wound += 1
    elif card == "Cauterize":
        enemy -= 6
        wound -= 1
    elif card == "Suture":
        block += 7
        wound -= 2
    elif card == "Transfuse":
        enemy -= 8 + wound * 4
        wound = 0
    elif card == "Safe Jab":
        enemy -= 7
        block += 2
    elif card == "Heavy Slash":
        enemy -= 14
    elif card == "False Blood":
        enemy -= 3
        wound -= 2
    else:
        raise ValueError(card)
    return enemy, block, clamp_wound(wound)


@lru_cache(maxsize=None)
def play_options(hand: tuple[str, ...], discard: tuple[str, ...], enemy: int, wound: int, energy: int, block: int):
    h = hand
    disc = discard
    e = enemy
    w = wound
    en = energy
    b = block
    for card in ORDER:
        while e > 0 and h.count(card) > 0 and COST[card] <= en:
            h = remove_one(h, card)
            disc = add_discard(disc, card)
            e, b, w = apply_card(card, e, b, w)
            en -= COST[card]
    return ((h, disc, e, w, en, b),)


def apply_potion(potion: str, enemy: int, wound: int, block: int) -> tuple[int, int, int, int]:
    no_tax = 0
    if potion == "Hemostasis":
        enemy -= wound * 5
        block += 4
        no_tax = 1
    elif potion == "Guard":
        block += 14
    elif potion == "Rage":
        enemy -= 8
        wound += 3
    else:
        raise ValueError(potion)
    return enemy, clamp_wound(wound), block, no_tax


def better(a: tuple[float, ...], b: tuple[float, ...] | None) -> bool:
    if b is None:
        return True
    return (sum(a[:-1]), a[0], a[1], a[2], a[3], a[4]) > (sum(b[:-1]), b[0], b[1], b[2], b[3], b[4])


def result_for(deck: tuple[str, ...], potion: str, enemy_hp: int, player_hp: int, incoming: tuple[int, ...]) -> tuple[float, ...]:
    hand_size = min(DRAW, len(deck))

    @lru_cache(maxsize=None)
    def solve(turn: int, hand: tuple[str, ...], draw: tuple[str, ...], discard: tuple[str, ...], hp: int, enemy: int, wound: int, potion_used: int):
        starts = [(enemy, wound, 0, 0, potion_used)]
        if not potion_used:
            pe, pw, pb, no_tax = apply_potion(potion, enemy, wound, 0)
            starts.append((pe, pw, pb, no_tax, 1))

        best = None
        for start_enemy, start_wound, start_block, no_tax, used in starts:
            for h, disc, e, w, _en, block in play_options(hand, discard, start_enemy, start_wound, ENERGY, start_block):
                out = [0.0] * (MAX_TURN + 1)
                if e <= 0:
                    out[turn - 1] = 1.0
                    cand = tuple(out)
                elif turn >= MAX_TURN:
                    out[-1] = 1.0
                    cand = tuple(out)
                else:
                    tax = 0 if no_tax else w * 2
                    hp2 = hp - max(0, incoming[turn - 1] - block) - tax
                    if hp2 <= 0:
                        out[-1] = 1.0
                        cand = tuple(out)
                    else:
                        discard2 = tuple(sorted(disc + h))
                        merged = [0.0] * (MAX_TURN + 1)
                        for nh, ndraw, ndiscard, p in next_draws(draw, discard2, hand_size):
                            fut = solve(turn + 1, nh, ndraw, ndiscard, hp2, e, w, used)
                            for i, val in enumerate(fut):
                                merged[i] += p * val
                        cand = tuple(merged)
                if better(cand, best):
                    best = cand
        return best or tuple([0.0] * MAX_TURN + [1.0])

    total = [0.0] * (MAX_TURN + 1)
    for hand, draw, p in draw_outcomes(deck, hand_size):
        res = solve(1, hand, draw, (), player_hp, enemy_hp, 0, 0)
        for i, x in enumerate(res):
            total[i] += p * x
    return tuple(total)


def classify(deck: tuple[str, ...], potion: str) -> str:
    c = Counter(deck)
    if c["False Blood"]:
        return "伪血印陷阱线"
    if potion == "Hemostasis" and c["Transfuse"] and c["Cut"]:
        return "止血转伤线"
    if potion == "Guard" and c["Suture"] and c["Transfuse"]:
        return "护壳缝合线"
    if potion == "Rage" and c["Transfuse"]:
        return "狂血抢杀线"
    if c["Heavy Slash"]:
        return "重斩诱导线"
    return "基础伤口线"


def first_turn(vec: tuple[float, ...]) -> int:
    for i, x in enumerate(vec[:-1], 1):
        if x > 1e-9:
            return i
    return 0


def pct_vec(vec: tuple[float, ...]) -> list[float]:
    return [round(x * 100, 4) for x in vec]


def run(enemy_hp: int, player_hp: int, incoming: tuple[int, ...]):
    rows = []
    decks = unique_decks(7)
    for deck in decks:
        for potion in POTIONS:
            vec = result_for(deck, potion, enemy_hp, player_hp, incoming)
            rows.append({
                "deck": list(deck),
                "potion": potion,
                "display": display_build(deck, potion),
                "family": classify(deck, potion),
                "first_turn": first_turn(vec),
                "kill_vector": pct_vec(vec),
                "success": round(sum(vec[:-1]) * 100, 4),
            })
    rows.sort(key=lambda r: (r["success"], r["kill_vector"][0], r["kill_vector"][1], r["kill_vector"][2], r["kill_vector"][3], r["kill_vector"][4]), reverse=True)
    best_by_turn = {}
    best_by_family = {}
    best_by_potion = {}
    for r in rows:
        best_by_turn.setdefault(str(r["first_turn"]), r)
        best_by_family.setdefault(r["family"], r)
        best_by_potion.setdefault(r["potion"], r)
    return {
        "enemy_hp": enemy_hp,
        "player_hp": player_hp,
        "incoming": list(incoming),
        "legal_deck_count": len(decks),
        "legal_build_count": len(rows),
        "perfect_success_count": sum(1 for r in rows if r["success"] >= 99.9999),
        "top30": rows[:30],
        "best_by_first_turn": best_by_turn,
        "best_by_family": best_by_family,
        "best_by_potion": best_by_potion,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--enemy-hp", type=int, default=88)
    parser.add_argument("--player-hp", type=int, default=18)
    parser.add_argument("--incoming", default="6,12,18,24,30")
    parser.add_argument("--output", default="set2_d4_wound_audit.json")
    args = parser.parse_args()
    incoming = tuple(int(x) for x in args.incoming.split(","))
    summary = run(args.enemy_hp, args.player_hp, incoming)
    with open(args.output, "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)
    print("enemy_hp", summary["enemy_hp"], "player_hp", summary["player_hp"], "incoming", summary["incoming"])
    print("legal_deck_count", summary["legal_deck_count"])
    print("legal_build_count", summary["legal_build_count"])
    print("perfect_success_count", summary["perfect_success_count"])
    print("top10")
    for r in summary["top30"][:10]:
        print(r["success"], "first", r["first_turn"], r["family"], r["kill_vector"], r["display"])
    print("best_by_first_turn")
    for k, r in sorted(summary["best_by_first_turn"].items(), key=lambda kv: 99 if kv[0] == "0" else int(kv[0])):
        print(k, r["success"], r["family"], r["kill_vector"], r["display"])
    print("best_by_potion")
    for k, r in sorted(summary["best_by_potion"].items()):
        print(k, r["success"], "first", r["first_turn"], r["family"], r["kill_vector"], r["display"])
    print("best_by_family")
    for k, r in sorted(summary["best_by_family"].items()):
        print(k, r["success"], "first", r["first_turn"], r["kill_vector"], r["display"])


if __name__ == "__main__":
    main()
