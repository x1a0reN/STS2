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
    "Left Bid", "Left Bid",
    "Right Bid", "Right Bid",
    "Dual Bid",
    "Auction Guard",
    "Final Lot",
    "Underbid",
    "Safe Hit",
    "Heavy Slash",
    "Blank",
)

POTIONS = ("Left Fund", "Right Fund", "Guard")
RELICS = ("Ledger", "Insurance")

CN = {
    "Strike": "打击",
    "Defend": "防御",
    "Left Bid": "左价码（改）",
    "Right Bid": "右价码（改）",
    "Dual Bid": "双价码（改）",
    "Auction Guard": "拍卖护栏（改）",
    "Final Lot": "终局拍品（改）",
    "Underbid": "压价（改）",
    "Safe Hit": "稳击",
    "Heavy Slash": "重斩",
    "Blank": "空牌",
}

POTION_CN = {"Left Fund": "左筹码药水", "Right Fund": "右筹码药水", "Guard": "护壳药水"}
RELIC_CN = {"Ledger": "拍卖账本", "Insurance": "保险契约"}

COST = {
    "Strike": 1,
    "Defend": 1,
    "Left Bid": 1,
    "Right Bid": 1,
    "Dual Bid": 1,
    "Auction Guard": 1,
    "Final Lot": 2,
    "Underbid": 0,
    "Safe Hit": 1,
    "Heavy Slash": 2,
    "Blank": 0,
}

ORDER = (
    "Left Bid",
    "Right Bid",
    "Dual Bid",
    "Underbid",
    "Auction Guard",
    "Final Lot",
    "Safe Hit",
    "Heavy Slash",
    "Strike",
    "Defend",
    "Blank",
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


def clamp(x: int, hi: int) -> int:
    return max(0, min(hi, x))


def apply_card(card: str, enemy: int, block: int, left: int, right: int, value: int) -> tuple[int, int, int, int, int, int]:
    hp_loss = 0
    if card == "Strike":
        enemy -= 6
    elif card == "Defend":
        block += 5
    elif card == "Left Bid":
        enemy -= 4
        left -= 2
        value += 1
    elif card == "Right Bid":
        enemy -= 4
        right -= 2
        value += 1
    elif card == "Dual Bid":
        enemy -= 5
        left -= 1
        right -= 1
        value += 1
    elif card == "Auction Guard":
        block += 7
        if left > right:
            left -= 1
        else:
            right -= 1
    elif card == "Final Lot":
        if left <= 2 and right <= 2:
            enemy -= 8 + value * 8
        else:
            enemy -= 5
        value = 0
    elif card == "Underbid":
        enemy -= 2
        left += 2
        right += 2
        hp_loss += 8
    elif card == "Safe Hit":
        enemy -= 7
        block += 2
    elif card == "Heavy Slash":
        enemy -= 9
    elif card == "Blank":
        pass
    else:
        raise ValueError(card)
    return enemy, block, clamp(left, 6), clamp(right, 6), clamp(value, 8), hp_loss


@lru_cache(maxsize=None)
def play_options(hand: tuple[str, ...], discard: tuple[str, ...], enemy: int, left: int, right: int, value: int, energy: int, block: int):
    h = hand
    disc = discard
    e = enemy
    l = left
    r = right
    v = value
    en = energy
    blk = block
    hp_loss = 0
    for card in ORDER:
        while e > 0 and h.count(card) > 0 and COST[card] <= en:
            h = remove_one(h, card)
            disc = add_discard(disc, card)
            e, blk, l, r, v, loss = apply_card(card, e, blk, l, r, v)
            hp_loss += loss
            en -= COST[card]
    return ((h, disc, e, l, r, v, en, blk, hp_loss),)


def apply_potion(potion: str, left: int, right: int, value: int, block: int) -> tuple[int, int, int, int]:
    if potion == "Left Fund":
        left -= 3
        value += 1
    elif potion == "Right Fund":
        right -= 3
        value += 1
    elif potion == "Guard":
        block += 14
    else:
        raise ValueError(potion)
    return clamp(left, 6), clamp(right, 6), clamp(value, 8), block


def better(a: tuple[float, ...], b: tuple[float, ...] | None) -> bool:
    if b is None:
        return True
    return (sum(a[:-1]), a[0], a[1], a[2], a[3], a[4]) > (sum(b[:-1]), b[0], b[1], b[2], b[3], b[4])


def result_for(deck: tuple[str, ...], potion: str, relic: str, enemy_hp: int, player_hp: int, incoming: tuple[int, ...]) -> tuple[float, ...]:
    hand_size = min(DRAW, len(deck))
    start_value = 1 if relic == "Ledger" else 0
    start_block = 2 if relic == "Insurance" else 0

    @lru_cache(maxsize=None)
    def solve(turn: int, hand: tuple[str, ...], draw: tuple[str, ...], discard: tuple[str, ...], hp: int, enemy: int, left: int, right: int, value: int, potion_used: int):
        left_now = clamp(left + 1, 6)
        right_now = clamp(right + 1, 6)
        if left_now >= 5 or right_now >= 5:
            return tuple([0.0] * MAX_TURN + [1.0])
        starts = [(left_now, right_now, value, start_block, potion_used)]
        if not potion_used:
            pl, pr, pv, pb = apply_potion(potion, left_now, right_now, value, start_block)
            starts.append((pl, pr, pv, pb, 1))
        best = None
        for sl, sr, sv, sblock, used in starts:
            for h, disc, e, l, r, v, _en, block, card_loss in play_options(hand, discard, enemy, sl, sr, sv, ENERGY, sblock):
                out = [0.0] * (MAX_TURN + 1)
                if e <= 0:
                    out[turn - 1] = 1.0
                    cand = tuple(out)
                elif turn >= MAX_TURN:
                    out[-1] = 1.0
                    cand = tuple(out)
                else:
                    debt_tax = max(0, l + r - (5 if relic == "Insurance" else 4)) * 2
                    hp2 = hp - max(0, incoming[turn - 1] - block) - card_loss - debt_tax
                    if hp2 <= 0:
                        out[-1] = 1.0
                        cand = tuple(out)
                    else:
                        discard2 = tuple(sorted(disc + h))
                        merged = [0.0] * (MAX_TURN + 1)
                        for nh, ndraw, ndiscard, prob in next_draws(draw, discard2, hand_size):
                            fut = solve(turn + 1, nh, ndraw, ndiscard, hp2, e, l, r, v, used)
                            for i, val in enumerate(fut):
                                merged[i] += prob * val
                        cand = tuple(merged)
                if better(cand, best):
                    best = cand
        return best or tuple([0.0] * MAX_TURN + [1.0])

    total = [0.0] * (MAX_TURN + 1)
    for hand, draw, prob in draw_outcomes(deck, hand_size):
        res = solve(1, hand, draw, (), player_hp, enemy_hp, 0, 0, start_value, 0)
        for i, x in enumerate(res):
            total[i] += prob * x
    return tuple(total)


def classify(deck: tuple[str, ...], potion: str, relic: str) -> str:
    c = Counter(deck)
    if c["Underbid"]:
        return "压价陷阱线"
    if c["Left Bid"] and c["Right Bid"] and c["Final Lot"]:
        return "双价终局线"
    if c["Dual Bid"] and c["Final Lot"]:
        return "双价码线"
    if potion in ("Left Fund", "Right Fund") and c["Final Lot"]:
        return "单侧筹码线"
    if c["Heavy Slash"]:
        return "重斩诱导线"
    return "基础拍卖线"


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
            for relic in RELICS:
                vec = result_for(deck, potion, relic, enemy_hp, player_hp, incoming)
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
    rows.sort(key=lambda row: (row["success"], row["kill_vector"][0], row["kill_vector"][1], row["kill_vector"][2], row["kill_vector"][3], row["kill_vector"][4]), reverse=True)
    best_by_turn = {}
    best_by_family = {}
    best_by_potion = {}
    best_by_relic = {}
    for row in rows:
        best_by_turn.setdefault(str(row["first_turn"]), row)
        best_by_family.setdefault(row["family"], row)
        best_by_potion.setdefault(row["potion"], row)
        best_by_relic.setdefault(row["relic"], row)
    return {
        "enemy_hp": enemy_hp,
        "player_hp": player_hp,
        "incoming": list(incoming),
        "legal_deck_count": len(decks),
        "legal_build_count": len(rows),
        "perfect_success_count": sum(1 for row in rows if row["success"] >= 99.9999),
        "top30": rows[:30],
        "best_by_first_turn": best_by_turn,
        "best_by_family": best_by_family,
        "best_by_potion": best_by_potion,
        "best_by_relic": best_by_relic,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--enemy-hp", type=int, default=92)
    parser.add_argument("--player-hp", type=int, default=20)
    parser.add_argument("--incoming", default="8,16,25,35,46")
    parser.add_argument("--output", default="set2_d10_auction_audit.json")
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
    for row in summary["top30"][:10]:
        print(row["success"], "first", row["first_turn"], row["family"], row["kill_vector"], row["display"])
    print("best_by_first_turn")
    for key, row in sorted(summary["best_by_first_turn"].items(), key=lambda item: 99 if item[0] == "0" else int(item[0])):
        print(key, row["success"], row["family"], row["kill_vector"], row["display"])
    print("best_by_family")
    for key, row in sorted(summary["best_by_family"].items()):
        print(key, row["success"], "first", row["first_turn"], row["kill_vector"], row["display"])
    print("best_by_potion")
    for key, row in sorted(summary["best_by_potion"].items()):
        print(key, row["success"], "first", row["first_turn"], row["family"], row["kill_vector"], row["display"])
    print("best_by_relic")
    for key, row in sorted(summary["best_by_relic"].items()):
        print(key, row["success"], "first", row["first_turn"], row["family"], row["kill_vector"], row["display"])


if __name__ == "__main__":
    main()
