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
    "Pulse", "Pulse",
    "Tremor", "Tremor",
    "Focus Guard",
    "Quake",
    "Breaker",
    "Premature",
    "Snap Cut",
    "Guard Step",
    "Heavy Slash",
)

CN = {
    "Strike": "打击",
    "Defend": "防御",
    "Pulse": "脉冲（改）",
    "Tremor": "震击（改）",
    "Focus Guard": "稳震格挡（改）",
    "Quake": "余震爆发（改）",
    "Breaker": "破震斩（改）",
    "Premature": "早爆（改）",
    "Snap Cut": "快斩",
    "Guard Step": "护步",
    "Heavy Slash": "重斩",
}

COST = {
    "Strike": 1,
    "Defend": 1,
    "Pulse": 0,
    "Tremor": 1,
    "Focus Guard": 1,
    "Quake": 2,
    "Breaker": 1,
    "Premature": 0,
    "Snap Cut": 1,
    "Guard Step": 1,
    "Heavy Slash": 2,
}

ORDER = (
    "Pulse",
    "Tremor",
    "Focus Guard",
    "Premature",
    "Quake",
    "Breaker",
    "Snap Cut",
    "Guard Step",
    "Heavy Slash",
    "Strike",
    "Defend",
)

MAX_TURN = 4
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


def clamp_shock(x: int) -> int:
    return max(0, min(7, x))


def apply_card(card: str, enemy: int, block: int, shock: int) -> tuple[int, int, int, int]:
    backlash = 0
    if card == "Strike":
        enemy -= 6
    elif card == "Defend":
        block += 5
    elif card == "Pulse":
        shock += 1
    elif card == "Tremor":
        enemy -= 4
        shock += 2
    elif card == "Focus Guard":
        block += 6
        shock += 1
    elif card == "Quake":
        enemy -= 7 + 6 * shock
        shock = 0
    elif card == "Breaker":
        dmg = 5
        if shock >= 2:
            dmg += 12
            shock -= 2
        enemy -= dmg
    elif card == "Premature":
        enemy -= 5
        backlash += max(0, shock - 1) * 2
        shock = 0
    elif card == "Snap Cut":
        enemy -= 7
    elif card == "Guard Step":
        enemy -= 2
        block += 4
    elif card == "Heavy Slash":
        enemy -= 14
    else:
        raise ValueError(card)
    return enemy, block, clamp_shock(shock), backlash


@lru_cache(maxsize=None)
def play_options(hand: tuple[str, ...], discard: tuple[str, ...], enemy: int, shock: int, energy: int, block: int):
    h = hand
    disc = discard
    e = enemy
    s = shock
    en = energy
    b = block
    backlash = 0
    for card in ORDER:
        while e > 0 and h.count(card) > 0 and COST[card] <= en:
            h = remove_one(h, card)
            disc = add_discard(disc, card)
            e, b, s, tax = apply_card(card, e, b, s)
            backlash += tax
            en -= COST[card]
    return ((h, disc, e, s, en, b, backlash),)


def better(a: tuple[float, ...], b: tuple[float, ...] | None) -> bool:
    if b is None:
        return True
    return (sum(a[:-1]), a[0], a[1], a[2], a[3]) > (sum(b[:-1]), b[0], b[1], b[2], b[3])


def result_for(deck: tuple[str, ...], enemy_hp: int, player_hp: int, incoming: tuple[int, ...]) -> tuple[float, ...]:
    hand_size = min(DRAW, len(deck))

    @lru_cache(maxsize=None)
    def solve(turn: int, hand: tuple[str, ...], draw: tuple[str, ...], discard: tuple[str, ...], hp: int, enemy: int, shock: int):
        best = None
        for h, disc, e, s, _en, block, backlash in play_options(hand, discard, enemy, shock, ENERGY, 0):
            out = [0.0] * (MAX_TURN + 1)
            if e <= 0:
                out[turn - 1] = 1.0
                cand = tuple(out)
            elif turn >= MAX_TURN:
                out[-1] = 1.0
                cand = tuple(out)
            else:
                retention_tax = 5 if s >= 6 else 0
                hp2 = hp - max(0, incoming[turn - 1] - block) - backlash - retention_tax
                if hp2 <= 0:
                    out[-1] = 1.0
                    cand = tuple(out)
                else:
                    discard2 = tuple(sorted(disc + h))
                    merged = [0.0] * (MAX_TURN + 1)
                    for nh, ndraw, ndiscard, p in next_draws(draw, discard2, hand_size):
                        fut = solve(turn + 1, nh, ndraw, ndiscard, hp2, e, s)
                        for i, val in enumerate(fut):
                            merged[i] += p * val
                    cand = tuple(merged)
            if better(cand, best):
                best = cand
        return best or tuple([0.0] * MAX_TURN + [1.0])

    total = [0.0] * (MAX_TURN + 1)
    for hand, draw, p in draw_outcomes(deck, hand_size):
        res = solve(1, hand, draw, (), player_hp, enemy_hp, 0)
        for i, x in enumerate(res):
            total[i] += p * x
    return tuple(total)


def classify(deck: tuple[str, ...]) -> str:
    c = Counter(deck)
    if c["Premature"]:
        return "早爆陷阱线"
    if c["Quake"] and c["Tremor"] >= 2 and c["Pulse"]:
        return "余震爆发线"
    if c["Breaker"] and c["Tremor"]:
        return "破震斩线"
    if c["Focus Guard"] and c["Defend"] >= 2:
        return "稳震拖回合线"
    if c["Heavy Slash"]:
        return "重斩诱导线"
    return "基础余震线"


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
        vec = result_for(deck, enemy_hp, player_hp, incoming)
        rows.append({
            "deck": list(deck),
            "display": display(deck),
            "family": classify(deck),
            "first_turn": first_turn(vec),
            "kill_vector": pct_vec(vec),
            "success": round(sum(vec[:-1]) * 100, 4),
        })
    rows.sort(key=lambda r: (r["success"], r["kill_vector"][0], r["kill_vector"][1], r["kill_vector"][2], r["kill_vector"][3]), reverse=True)
    best_by_turn = {}
    best_by_family = {}
    for r in rows:
        best_by_turn.setdefault(str(r["first_turn"]), r)
        best_by_family.setdefault(r["family"], r)
    return {
        "enemy_hp": enemy_hp,
        "player_hp": player_hp,
        "incoming": list(incoming),
        "legal_deck_count": len(decks),
        "perfect_success_count": sum(1 for r in rows if r["success"] >= 99.9999),
        "top30": rows[:30],
        "best_by_first_turn": best_by_turn,
        "best_by_family": best_by_family,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--enemy-hp", type=int, default=86)
    parser.add_argument("--player-hp", type=int, default=15)
    parser.add_argument("--incoming", default="7,14,21,28")
    parser.add_argument("--output", default="set2_d3_aftershock_audit.json")
    args = parser.parse_args()
    incoming = tuple(int(x) for x in args.incoming.split(","))
    summary = run(args.enemy_hp, args.player_hp, incoming)
    with open(args.output, "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)
    print("enemy_hp", summary["enemy_hp"], "player_hp", summary["player_hp"], "incoming", summary["incoming"])
    print("legal_deck_count", summary["legal_deck_count"])
    print("perfect_success_count", summary["perfect_success_count"])
    print("top10")
    for r in summary["top30"][:10]:
        print(r["success"], "first", r["first_turn"], r["family"], r["kill_vector"], r["display"])
    print("best_by_first_turn")
    for k, r in sorted(summary["best_by_first_turn"].items(), key=lambda kv: 99 if kv[0] == "0" else int(kv[0])):
        print(k, r["success"], r["family"], r["kill_vector"], r["display"])
    print("best_by_family")
    for k, r in sorted(summary["best_by_family"].items()):
        print(k, r["success"], "first", r["first_turn"], r["kill_vector"], r["display"])


if __name__ == "__main__":
    main()
