from __future__ import annotations

from collections import Counter, defaultdict
from functools import lru_cache
from itertools import combinations
from math import comb
import json


POOL = (
    "Strike", "Strike", "Strike", "Strike",
    "Defend", "Defend", "Defend",
    "Bash",
    "Perfected Strike",
    "Bludgeon",
    "Body Slam",
    "Iron Wave",
    "Sword Boomerang",
)

CN = {
    "Strike": "打击",
    "Defend": "防御",
    "Bash": "重击",
    "Perfected Strike": "完美打击",
    "Bludgeon": "重锤（改）",
    "Body Slam": "全身撞击",
    "Iron Wave": "铁斩波",
    "Sword Boomerang": "飞剑回旋",
}

COST = {
    "Strike": 1,
    "Defend": 1,
    "Bash": 2,
    "Perfected Strike": 2,
    "Bludgeon": 3,
    "Body Slam": 1,
    "Iron Wave": 1,
    "Sword Boomerang": 1,
}

ORDER = ("Bludgeon", "Bash", "Perfected Strike", "Sword Boomerang", "Iron Wave", "Strike", "Defend", "Body Slam")


def unique_decks(size: int = 7) -> list[tuple[str, ...]]:
    out: set[tuple[str, ...]] = set()
    for pick in combinations(range(len(POOL)), size):
        out.add(tuple(sorted(POOL[i] for i in pick)))
    return sorted(out)


def display(deck: tuple[str, ...]) -> str:
    counts = Counter(deck)
    parts = []
    for card in ORDER:
        n = counts[card]
        if n == 1:
            parts.append(CN[card])
        elif n > 1:
            parts.append(f"{CN[card]} x{n}")
    return "、".join(parts)


def vuln_damage(base: int, vuln: int) -> int:
    return (base * 3) // 2 if vuln > 0 else base


def weak_damage(base: int, weak: int) -> int:
    return (base * 3) // 4 if weak > 0 else base


@lru_cache(maxsize=None)
def draw_outcomes(cards: tuple[str, ...], n: int):
    if n >= len(cards):
        return ((tuple(sorted(cards)), (), 1.0),)
    denom = comb(len(cards), n)
    merged = defaultdict(float)
    for pick in combinations(range(len(cards)), n):
        chosen = tuple(sorted(cards[i] for i in pick))
        rest = tuple(sorted(cards[i] for i in range(len(cards)) if i not in set(pick)))
        merged[(chosen, rest)] += 1 / denom
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
    items = list(hand)
    items.remove(card)
    return tuple(sorted(items))


def add_discard(discard: tuple[str, ...], card: str) -> tuple[str, ...]:
    return tuple(sorted(discard + (card,)))


def card_damage(card: str, deck: tuple[str, ...], block: int, vuln: int) -> tuple[int, int]:
    if card == "Strike":
        return vuln_damage(6, vuln), block
    if card == "Perfected Strike":
        strike_names = sum(1 for c in deck if "Strike" in c)
        return vuln_damage(6 + 2 * strike_names, vuln), block
    if card == "Bash":
        return vuln_damage(8, vuln), block
    if card == "Uppercut":
        return vuln_damage(13, vuln), block
    if card == "Sword Boomerang":
        return vuln_damage(3, vuln) * 3, block
    if card == "Iron Wave":
        return vuln_damage(5, vuln), block + 5
    if card == "Body Slam":
        return vuln_damage(block, vuln), block
    if card == "Bludgeon":
        return vuln_damage(23, vuln), block
    return 0, block


def better(a: tuple[float, ...], b: tuple[float, ...] | None) -> bool:
    if b is None:
        return True
    return (sum(a[:-1]), a[0], a[1], a[2], a[3]) > (sum(b[:-1]), b[0], b[1], b[2], b[3])


def result_for(deck: tuple[str, ...], enemy_hp: int, player_hp: int, incoming: tuple[int, ...]) -> tuple[float, ...]:
    max_turn = len(incoming)
    hand_size = min(5, len(deck))

    @lru_cache(maxsize=None)
    def play_options(turn: int, hand: tuple[str, ...], draw: tuple[str, ...], discard: tuple[str, ...], hp: int, enemy: int, vuln: int, weak: int, energy: int, block: int):
        states = {(hand, draw, discard, hp, enemy, vuln, weak, energy, block)}
        changed = True
        while changed:
            changed = False
            for state in list(states):
                h, dr, disc, cur_hp, cur_enemy, cur_vuln, cur_weak, cur_energy, cur_block = state
                if cur_hp <= 0 or cur_enemy <= 0:
                    continue
                for card in sorted(set(h), key=ORDER.index):
                    if COST[card] > cur_energy:
                        continue
                    nh = remove_one(h, card)
                    nd = add_discard(disc, card)
                    ne = cur_energy - COST[card]
                    nb = cur_block
                    nv = cur_vuln
                    nw = cur_weak
                    dmg = 0
                    if card == "Defend":
                        nb += 5
                    elif card == "Iron Wave":
                        dmg, nb = card_damage(card, deck, nb, nv)
                    else:
                        dmg, nb = card_damage(card, deck, nb, nv)
                        if card == "Bash":
                            nv += 2
                    ns = (nh, dr, nd, cur_hp, cur_enemy - dmg, nv, nw, ne, nb)
                    if ns not in states:
                        states.add(ns)
                        changed = True
        return tuple(states)

    @lru_cache(maxsize=None)
    def solve(turn: int, hand: tuple[str, ...], draw: tuple[str, ...], discard: tuple[str, ...], hp: int, enemy: int, vuln: int, weak: int):
        best = None
        for h, dr, disc, cur_hp, cur_enemy, cur_vuln, cur_weak, energy, block in play_options(turn, hand, draw, discard, hp, enemy, vuln, weak, 3, 0):
            out = [0.0] * (max_turn + 1)
            if cur_enemy <= 0:
                out[turn - 1] = 1.0
                cand = tuple(out)
            elif turn >= max_turn:
                out[-1] = 1.0
                cand = tuple(out)
            else:
                hp2 = cur_hp - max(0, weak_damage(incoming[turn - 1], cur_weak) - block)
                if hp2 <= 0:
                    out[-1] = 1.0
                    cand = tuple(out)
                else:
                    discard2 = tuple(sorted(disc + h))
                    merged = [0.0] * (max_turn + 1)
                    for nh, ndraw, ndiscard, p in next_draws(dr, discard2, hand_size):
                        fut = solve(turn + 1, nh, ndraw, ndiscard, hp2, cur_enemy, max(0, cur_vuln - 1), max(0, cur_weak - 1))
                        for i, val in enumerate(fut):
                            merged[i] += p * val
                    cand = tuple(merged)
            if better(cand, best):
                best = cand
        return best or tuple([0.0] * max_turn + [1.0])

    merged = [0.0] * (max_turn + 1)
    for hand, draw, p in draw_outcomes(deck, hand_size):
        res = solve(1, hand, draw, (), player_hp, enemy_hp, 0, 0)
        for i, val in enumerate(res):
            merged[i] += p * val
    return tuple(merged)


def classify(deck: tuple[str, ...]) -> str:
    c = Counter(deck)
    if c["Perfected Strike"] >= 2 and c["Strike"] >= 3:
        return "完美打击快线"
    if c["Bludgeon"]:
        return "重锤快线"
    if c["Body Slam"] and c["Defend"] >= 2:
        return "格挡撞击线"
    if c["Bash"] or c["Uppercut"]:
        return "易伤混合线"
    return "基础攻击线"


def run(enemy_hp: int, player_hp: int, incoming: tuple[int, ...]):
    rows = []
    for deck in unique_decks(7):
        res = result_for(deck, enemy_hp, player_hp, incoming)
        rows.append({
            "deck": deck,
            "display": display(deck),
            "family": classify(deck),
            "first_turn": next((i + 1 for i, x in enumerate(res[:-1]) if x > 1e-9), 0),
            "result": res,
            "success": sum(res[:-1]),
        })
    rows.sort(key=lambda r: (r["success"], r["result"][0], r["result"][1], r["result"][2], r["result"][3]), reverse=True)
    best_by_turn = {}
    best_by_family = {}
    for row in rows:
        best_by_turn.setdefault(str(row["first_turn"]), row)
        best_by_family.setdefault(row["family"], row)
    return {
        "enemy_hp": enemy_hp,
        "player_hp": player_hp,
        "incoming": incoming,
        "legal_deck_count": len(rows),
        "perfect_success_count": sum(1 for r in rows if r["success"] >= 0.999999),
        "top30": rows[:30],
        "best_by_turn": best_by_turn,
        "best_by_family": best_by_family,
    }


def pct_vec(v):
    return [round(x * 100, 4) for x in v]


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser()
    parser.add_argument("--enemy-hp", type=int, default=55)
    parser.add_argument("--player-hp", type=int, default=10)
    parser.add_argument("--incoming", default="3,9,11,13")
    parser.add_argument("--output", default="difficulty1_pressure_audit.json")
    args = parser.parse_args()
    incoming = tuple(int(x) for x in args.incoming.split(","))
    summary = run(args.enemy_hp, args.player_hp, incoming)
    serial = {
        **{k: v for k, v in summary.items() if k not in {"top30", "best_by_turn", "best_by_family"}},
        "top30": [
            {**{k: v for k, v in row.items() if k not in {"deck", "result"}}, "deck": list(row["deck"]), "result": pct_vec(row["result"])}
            for row in summary["top30"]
        ],
        "best_by_turn": {
            k: {**{kk: vv for kk, vv in row.items() if kk not in {"deck", "result"}}, "deck": list(row["deck"]), "result": pct_vec(row["result"])}
            for k, row in summary["best_by_turn"].items()
        },
        "best_by_family": {
            k: {**{kk: vv for kk, vv in row.items() if kk not in {"deck", "result"}}, "deck": list(row["deck"]), "result": pct_vec(row["result"])}
            for k, row in summary["best_by_family"].items()
        },
    }
    with open(args.output, "w", encoding="utf-8") as f:
        json.dump(serial, f, ensure_ascii=False, indent=2)
    print("enemy_hp", summary["enemy_hp"], "player_hp", summary["player_hp"], "incoming", summary["incoming"])
    print("legal_deck_count", summary["legal_deck_count"])
    print("perfect_success_count", summary["perfect_success_count"])
    print("top10")
    for row in summary["top30"][:10]:
        print(round(row["success"] * 100, 4), "first", row["first_turn"], row["family"], pct_vec(row["result"]), row["display"])
    print("best_by_turn")
    for turn, row in sorted(summary["best_by_turn"].items(), key=lambda item: 99 if item[0] == "0" else int(item[0])):
        print(turn, round(row["success"] * 100, 4), row["family"], pct_vec(row["result"]), row["display"])
    print("best_by_family")
    for fam, row in summary["best_by_family"].items():
        print(fam, round(row["success"] * 100, 4), "first", row["first_turn"], pct_vec(row["result"]), row["display"])
