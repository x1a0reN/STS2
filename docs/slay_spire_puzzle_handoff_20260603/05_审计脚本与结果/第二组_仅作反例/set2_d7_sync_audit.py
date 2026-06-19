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
    "Left Cut", "Left Cut",
    "Right Cut", "Right Cut",
    "Twin Cut", "Twin Cut",
    "Sync Mark",
    "Mirror Finish",
    "Balance Hit",
    "Single Greed",
    "Guard Sweep",
)

POTIONS = ("Equalize", "Sweep", "Guard")
RELICS = ("Twin Seal", "Sharp Ring")

CN = {
    "Strike": "打击",
    "Defend": "防御",
    "Left Cut": "本体切（改）",
    "Right Cut": "影子切（改）",
    "Twin Cut": "双切（改）",
    "Sync Mark": "同步标记（改）",
    "Mirror Finish": "镜像收束（改）",
    "Balance Hit": "校准击（改）",
    "Single Greed": "贪单斩（改）",
    "Guard Sweep": "护扫",
}

POTION_CN = {
    "Equalize": "均衡药水",
    "Sweep": "横扫药水",
    "Guard": "护壳药水",
}

RELIC_CN = {
    "Twin Seal": "双生印",
    "Sharp Ring": "利刃环",
}

COST = {
    "Strike": 1,
    "Defend": 1,
    "Left Cut": 1,
    "Right Cut": 1,
    "Twin Cut": 1,
    "Sync Mark": 0,
    "Mirror Finish": 2,
    "Balance Hit": 1,
    "Single Greed": 1,
    "Guard Sweep": 1,
}

ORDER = (
    "Sync Mark",
    "Twin Cut",
    "Balance Hit",
    "Left Cut",
    "Right Cut",
    "Single Greed",
    "Mirror Finish",
    "Guard Sweep",
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


def display_build(deck: tuple[str, ...], potion: str, relic: str) -> str:
    return f"{display(deck)}；{POTION_CN[potion]}；{RELIC_CN[relic]}"


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


def clamp_sync(x: int) -> int:
    return max(0, min(6, x))


def damage_bonus(relic: str) -> int:
    return 1 if relic == "Sharp Ring" else 0


def apply_card(card: str, body: int, shadow: int, block: int, sync: int, relic: str) -> tuple[int, int, int, int]:
    bonus = damage_bonus(relic)
    if card == "Strike":
        if body >= shadow:
            body -= 6 + bonus
        else:
            shadow -= 6 + bonus
    elif card == "Defend":
        block += 5
    elif card == "Left Cut":
        body -= 10 + bonus
    elif card == "Right Cut":
        shadow -= 10 + bonus
    elif card == "Twin Cut":
        body -= 5 + bonus
        shadow -= 5 + bonus
    elif card == "Sync Mark":
        sync += 1
    elif card == "Mirror Finish":
        body -= 7 + sync * 4
        shadow -= 7 + sync * 4
        sync = 0
    elif card == "Balance Hit":
        if body > shadow:
            body -= 9 + bonus
        elif shadow > body:
            shadow -= 9 + bonus
        else:
            body -= 5 + bonus
            shadow -= 5 + bonus
    elif card == "Single Greed":
        if body >= shadow:
            body -= 7
        else:
            shadow -= 7
        sync = 0
    elif card == "Guard Sweep":
        body -= 3
        shadow -= 3
        block += 5
    else:
        raise ValueError(card)
    return body, shadow, block, clamp_sync(sync)


@lru_cache(maxsize=None)
def play_options(hand: tuple[str, ...], discard: tuple[str, ...], body: int, shadow: int, sync: int, energy: int, block: int, relic: str):
    h = hand
    disc = discard
    bdy = body
    shd = shadow
    syn = sync
    en = energy
    blk = block
    for card in ORDER:
        while h.count(card) > 0 and COST[card] <= en and not (bdy <= 0 and shd <= 0):
            h = remove_one(h, card)
            disc = add_discard(disc, card)
            bdy, shd, blk, syn = apply_card(card, bdy, shd, blk, syn, relic)
            en -= COST[card]
    return ((h, disc, bdy, shd, syn, en, blk),)


def apply_potion(potion: str, body: int, shadow: int, sync: int, block: int) -> tuple[int, int, int, int]:
    if potion == "Equalize":
        high = max(body, shadow)
        low = min(body, shadow)
        gap = min(10, high - low)
        if body > shadow:
            body -= gap
        elif shadow > body:
            shadow -= gap
        sync += 1
    elif potion == "Sweep":
        body -= 10
        shadow -= 10
    elif potion == "Guard":
        block += 14
    else:
        raise ValueError(potion)
    return body, shadow, clamp_sync(sync), block


def sync_state(body: int, shadow: int) -> str:
    if body <= 0 and shadow <= 0:
        return "both"
    if body <= 0 or shadow <= 0:
        return "one"
    return "none"


def better(a: tuple[float, ...], b: tuple[float, ...] | None) -> bool:
    if b is None:
        return True
    return (sum(a[:-1]), a[0], a[1], a[2], a[3], a[4]) > (sum(b[:-1]), b[0], b[1], b[2], b[3], b[4])


def result_for(deck: tuple[str, ...], potion: str, relic: str, body_hp: int, shadow_hp: int, player_hp: int, incoming: tuple[int, ...]) -> tuple[float, ...]:
    hand_size = min(DRAW, len(deck))
    start_sync = 1 if relic == "Twin Seal" else 0

    @lru_cache(maxsize=None)
    def solve(turn: int, hand: tuple[str, ...], draw: tuple[str, ...], discard: tuple[str, ...], hp: int, body: int, shadow: int, sync: int, potion_used: int):
        starts = [(body, shadow, sync, 0, potion_used)]
        if not potion_used:
            pb, ps, py, pblock = apply_potion(potion, body, shadow, sync, 0)
            starts.append((pb, ps, py, pblock, 1))
        best = None
        for sb, ss, sy, start_block, used in starts:
            for h, disc, bdy, shd, syn, _en, block in play_options(hand, discard, sb, ss, sy, ENERGY, start_block, relic):
                out = [0.0] * (MAX_TURN + 1)
                status = sync_state(bdy, shd)
                if status == "both":
                    out[turn - 1] = 1.0
                    cand = tuple(out)
                elif status == "one":
                    out[-1] = 1.0
                    cand = tuple(out)
                elif turn >= MAX_TURN:
                    out[-1] = 1.0
                    cand = tuple(out)
                else:
                    gap_tax = 4 if abs(bdy - shd) >= 18 else 0
                    hp2 = hp - max(0, incoming[turn - 1] - block) - gap_tax
                    if hp2 <= 0:
                        out[-1] = 1.0
                        cand = tuple(out)
                    else:
                        discard2 = tuple(sorted(disc + h))
                        merged = [0.0] * (MAX_TURN + 1)
                        for nh, ndraw, ndiscard, prob in next_draws(draw, discard2, hand_size):
                            fut = solve(turn + 1, nh, ndraw, ndiscard, hp2, bdy, shd, syn, used)
                            for i, val in enumerate(fut):
                                merged[i] += prob * val
                        cand = tuple(merged)
                if better(cand, best):
                    best = cand
        return best or tuple([0.0] * MAX_TURN + [1.0])

    total = [0.0] * (MAX_TURN + 1)
    for hand, draw, prob in draw_outcomes(deck, hand_size):
        res = solve(1, hand, draw, (), player_hp, body_hp, shadow_hp, start_sync, 0)
        for i, x in enumerate(res):
            total[i] += prob * x
    return tuple(total)


def classify(deck: tuple[str, ...], potion: str, relic: str) -> str:
    c = Counter(deck)
    if c["Single Greed"]:
        return "贪单陷阱线"
    if c["Twin Cut"] >= 2 and c["Mirror Finish"]:
        return "双切收束线"
    if potion == "Equalize" and c["Balance Hit"]:
        return "均衡校准线"
    if c["Left Cut"] and c["Right Cut"] and c["Mirror Finish"]:
        return "左右同步线"
    if c["Heavy Slash"] or relic == "Sharp Ring":
        return "单点诱导线"
    return "基础同步线"


def first_turn(vec: tuple[float, ...]) -> int:
    for i, x in enumerate(vec[:-1], 1):
        if x > 1e-9:
            return i
    return 0


def pct_vec(vec: tuple[float, ...]) -> list[float]:
    return [round(x * 100, 4) for x in vec]


def run(body_hp: int, shadow_hp: int, player_hp: int, incoming: tuple[int, ...]):
    rows = []
    decks = unique_decks(7)
    for deck in decks:
        for potion in POTIONS:
            for relic in RELICS:
                vec = result_for(deck, potion, relic, body_hp, shadow_hp, player_hp, incoming)
                rows.append({
                    "deck": list(deck),
                    "potion": potion,
                    "relic": relic,
                    "display": display_build(deck, potion, relic),
                    "family": classify(deck, potion, relic),
                    "first_turn": first_turn(vec),
                    "kill_vector": pct_vec(vec),
                    "success": round(sum(vec[:-1]) * 100, 4),
                })
    rows.sort(key=lambda r: (r["success"], r["kill_vector"][0], r["kill_vector"][1], r["kill_vector"][2], r["kill_vector"][3], r["kill_vector"][4]), reverse=True)
    best_by_turn = {}
    best_by_family = {}
    best_by_potion = {}
    best_by_relic = {}
    for r in rows:
        best_by_turn.setdefault(str(r["first_turn"]), r)
        best_by_family.setdefault(r["family"], r)
        best_by_potion.setdefault(r["potion"], r)
        best_by_relic.setdefault(r["relic"], r)
    return {
        "body_hp": body_hp,
        "shadow_hp": shadow_hp,
        "player_hp": player_hp,
        "incoming": list(incoming),
        "legal_deck_count": len(decks),
        "legal_build_count": len(rows),
        "perfect_success_count": sum(1 for r in rows if r["success"] >= 99.9999),
        "top30": rows[:30],
        "best_by_first_turn": best_by_turn,
        "best_by_family": best_by_family,
        "best_by_potion": best_by_potion,
        "best_by_relic": best_by_relic,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--body-hp", type=int, default=64)
    parser.add_argument("--shadow-hp", type=int, default=60)
    parser.add_argument("--player-hp", type=int, default=18)
    parser.add_argument("--incoming", default="6,12,18,26,35")
    parser.add_argument("--output", default="set2_d7_sync_audit.json")
    args = parser.parse_args()
    incoming = tuple(int(x) for x in args.incoming.split(","))
    summary = run(args.body_hp, args.shadow_hp, args.player_hp, incoming)
    with open(args.output, "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)
    print("body_hp", summary["body_hp"], "shadow_hp", summary["shadow_hp"], "player_hp", summary["player_hp"], "incoming", summary["incoming"])
    print("legal_deck_count", summary["legal_deck_count"])
    print("legal_build_count", summary["legal_build_count"])
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
    print("best_by_potion")
    for k, r in sorted(summary["best_by_potion"].items()):
        print(k, r["success"], "first", r["first_turn"], r["family"], r["kill_vector"], r["display"])
    print("best_by_relic")
    for k, r in sorted(summary["best_by_relic"].items()):
        print(k, r["success"], "first", r["first_turn"], r["family"], r["kill_vector"], r["display"])


if __name__ == "__main__":
    main()
