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
    "Plan", "Plan",
    "Hold Guard", "Hold Guard",
    "Stored Strike",
    "Trigger",
    "Release",
    "Overplan",
    "Safe Hit",
    "Heavy Slash",
    "Blank Guard",
)

POTIONS = ("Insight", "Guard", "Burst")
RELICS = ("Notebook", "Anchor")

RETAIN = {"Hold Guard", "Stored Strike", "Release"}

CN = {
    "Strike": "打击",
    "Defend": "防御",
    "Plan": "预谋（改）",
    "Hold Guard": "保留格挡（改）",
    "Stored Strike": "蓄势击（改）",
    "Trigger": "触发斩（改）",
    "Release": "预谋释放（改）",
    "Overplan": "过度预谋（改）",
    "Safe Hit": "稳击",
    "Heavy Slash": "重斩",
    "Blank Guard": "空护",
}

POTION_CN = {
    "Insight": "洞察药水",
    "Guard": "护壳药水",
    "Burst": "爆发药水",
}

RELIC_CN = {
    "Notebook": "预谋笔记",
    "Anchor": "留牌锚",
}

COST = {
    "Strike": 1,
    "Defend": 1,
    "Plan": 0,
    "Hold Guard": 1,
    "Stored Strike": 2,
    "Trigger": 1,
    "Release": 2,
    "Overplan": 0,
    "Safe Hit": 1,
    "Heavy Slash": 2,
    "Blank Guard": 1,
}

ORDER = (
    "Plan",
    "Overplan",
    "Hold Guard",
    "Trigger",
    "Release",
    "Stored Strike",
    "Safe Hit",
    "Heavy Slash",
    "Strike",
    "Defend",
    "Blank Guard",
)

MAX_TURN = 5
DRAW = 5
ENERGY = 3


def unique_decks(size: int = 6) -> list[tuple[str, ...]]:
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
    if n <= 0:
        return (((), tuple(sorted(cards)), 1.0),)
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
    if n <= 0:
        return (((), draw, discard, 1.0),)
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


def clamp_plan(x: int) -> int:
    return max(0, min(8, x))


def apply_card(card: str, enemy: int, block: int, plan: int) -> tuple[int, int, int, int]:
    hp_loss = 0
    if card == "Strike":
        enemy -= 6
    elif card == "Defend":
        block += 5
    elif card == "Plan":
        plan += 1
    elif card == "Hold Guard":
        block += 7
        plan += 1
    elif card == "Stored Strike":
        enemy -= 10 + plan * 5
        plan = 0
    elif card == "Trigger":
        dmg = 5
        if plan >= 2:
            dmg += 3
            plan -= 2
        enemy -= dmg
    elif card == "Release":
        enemy -= 8 + plan * 6
        plan = 0
    elif card == "Overplan":
        plan += 1
        hp_loss += 6
    elif card == "Safe Hit":
        enemy -= 7
        block += 2
    elif card == "Heavy Slash":
        enemy -= 14
    elif card == "Blank Guard":
        block += 8
        plan -= 1
    else:
        raise ValueError(card)
    return enemy, block, clamp_plan(plan), hp_loss


@lru_cache(maxsize=None)
def play_options(hand: tuple[str, ...], discard: tuple[str, ...], enemy: int, plan: int, energy: int, block: int):
    h = hand
    disc = discard
    e = enemy
    p = plan
    en = energy
    b = block
    hp_loss = 0
    for card in ORDER:
        while e > 0 and h.count(card) > 0 and COST[card] <= en:
            h = remove_one(h, card)
            disc = add_discard(disc, card)
            e, b, p, loss = apply_card(card, e, b, p)
            hp_loss += loss
            en -= COST[card]
    return ((h, disc, e, p, en, b, hp_loss),)


def apply_potion(potion: str, enemy: int, plan: int, block: int) -> tuple[int, int, int, int]:
    hp_loss = 0
    if potion == "Insight":
        plan += 2
    elif potion == "Guard":
        block += 14
    elif potion == "Burst":
        enemy -= 10
        plan = max(0, plan - 3)
    else:
        raise ValueError(potion)
    return enemy, clamp_plan(plan), block, hp_loss


def better(a: tuple[float, ...], b: tuple[float, ...] | None) -> bool:
    if b is None:
        return True
    return (sum(a[:-1]), a[0], a[1], a[2], a[3], a[4]) > (sum(b[:-1]), b[0], b[1], b[2], b[3], b[4])


def split_retained(hand: tuple[str, ...]) -> tuple[tuple[str, ...], tuple[str, ...]]:
    keep = []
    toss = []
    for card in hand:
        if card in RETAIN:
            keep.append(card)
        else:
            toss.append(card)
    return tuple(sorted(keep)), tuple(sorted(toss))


def result_for(deck: tuple[str, ...], potion: str, relic: str, enemy_hp: int, player_hp: int, incoming: tuple[int, ...]) -> tuple[float, ...]:
    start_plan = 1 if relic == "Notebook" else 0

    @lru_cache(maxsize=None)
    def solve(turn: int, hand: tuple[str, ...], draw: tuple[str, ...], discard: tuple[str, ...], hp: int, enemy: int, plan: int, potion_used: int):
        starts = [(enemy, plan, 2 if relic == "Anchor" else 0, 0, potion_used)]
        if not potion_used:
            pe, pp, pb, loss = apply_potion(potion, enemy, plan, 0)
            starts.append((pe, pp, pb + (2 if relic == "Anchor" else 0), loss, 1))

        best = None
        for start_enemy, start_plan2, start_block, potion_loss, used in starts:
            for h, disc, e, p, _en, block, card_loss in play_options(hand, discard, start_enemy, start_plan2, ENERGY, start_block):
                out = [0.0] * (MAX_TURN + 1)
                if e <= 0:
                    out[turn - 1] = 1.0
                    cand = tuple(out)
                elif turn >= MAX_TURN:
                    out[-1] = 1.0
                    cand = tuple(out)
                else:
                    plan_tax = 4 if p >= 7 else 0
                    hp2 = hp - max(0, incoming[turn - 1] - block) - potion_loss - card_loss - plan_tax
                    if hp2 <= 0:
                        out[-1] = 1.0
                        cand = tuple(out)
                    else:
                        retained, toss = split_retained(h)
                        discard2 = tuple(sorted(disc + toss))
                        draw_need = max(0, DRAW - len(retained))
                        merged = [0.0] * (MAX_TURN + 1)
                        for nh, ndraw, ndiscard, prob in next_draws(draw, discard2, draw_need):
                            fut = solve(turn + 1, tuple(sorted(retained + nh)), ndraw, ndiscard, hp2, e, p, used)
                            for i, val in enumerate(fut):
                                merged[i] += prob * val
                        cand = tuple(merged)
                if better(cand, best):
                    best = cand
        return best or tuple([0.0] * MAX_TURN + [1.0])

    total = [0.0] * (MAX_TURN + 1)
    for hand, draw, prob in draw_outcomes(deck, min(DRAW, len(deck))):
        res = solve(1, hand, draw, (), player_hp, enemy_hp, start_plan, 0)
        for i, x in enumerate(res):
            total[i] += prob * x
    return tuple(total)


def classify(deck: tuple[str, ...], potion: str, relic: str) -> str:
    c = Counter(deck)
    if c["Overplan"]:
        return "过度预谋陷阱线"
    if c["Hold Guard"] and (c["Release"] or c["Stored Strike"]) and relic == "Notebook":
        return "保留预谋线"
    if c["Trigger"] and c["Plan"] >= 2:
        return "触发斩线"
    if potion == "Burst" and c["Stored Strike"]:
        return "爆发补刀线"
    if c["Heavy Slash"]:
        return "重斩诱导线"
    return "基础留牌线"


def first_turn(vec: tuple[float, ...]) -> int:
    for i, x in enumerate(vec[:-1], 1):
        if x > 1e-9:
            return i
    return 0


def pct_vec(vec: tuple[float, ...]) -> list[float]:
    return [round(x * 100, 4) for x in vec]


def run(enemy_hp: int, player_hp: int, incoming: tuple[int, ...]):
    rows = []
    decks = unique_decks(6)
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
        "best_by_relic": best_by_relic,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--enemy-hp", type=int, default=96)
    parser.add_argument("--player-hp", type=int, default=19)
    parser.add_argument("--incoming", default="6,12,18,25,33")
    parser.add_argument("--output", default="set2_d6_retain_audit.json")
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
