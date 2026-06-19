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
    "Key", "Key",
    "Seal Hit", "Seal Hit",
    "Guard Key",
    "Release",
    "Delay Lock",
    "Deep Seal",
    "Safe Hit",
    "Heavy Slash",
    "Blank"
)

POTIONS = ("Key", "Guard", "Break")
RELICS = ("Keyring", "Ward")

CN = {
    "Strike": "打击",
    "Defend": "防御",
    "Key": "解封钥（改）",
    "Seal Hit": "封印击（改）",
    "Guard Key": "护钥（改）",
    "Release": "解封释放（改）",
    "Delay Lock": "延锁（改）",
    "Deep Seal": "深封（改）",
    "Safe Hit": "稳击",
    "Heavy Slash": "重斩",
    "Blank": "空牌",
}

POTION_CN = {"Key": "钥药水", "Guard": "护壳药水", "Break": "破封药水"}
RELIC_CN = {"Keyring": "钥环", "Ward": "封印护符"}

COST = {
    "Strike": 1,
    "Defend": 1,
    "Key": 0,
    "Seal Hit": 1,
    "Guard Key": 1,
    "Release": 2,
    "Delay Lock": 1,
    "Deep Seal": 0,
    "Safe Hit": 1,
    "Heavy Slash": 2,
    "Blank": 0,
}

ORDER = (
    "Key",
    "Deep Seal",
    "Guard Key",
    "Delay Lock",
    "Seal Hit",
    "Release",
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


def unlocked(key: int) -> bool:
    return key >= 3


def apply_card(card: str, enemy: int, block: int, key: int, seal: int, relic: str) -> tuple[int, int, int, int, int]:
    hp_loss = 0
    if card == "Strike":
        enemy -= 6
    elif card == "Defend":
        block += 5
    elif card == "Key":
        key += 1
    elif card == "Seal Hit":
        if unlocked(key):
            enemy -= 13
            seal = max(0, seal - 1)
        else:
            enemy -= 3
            seal += 1
    elif card == "Guard Key":
        block += 7
        key += 1
    elif card == "Release":
        if unlocked(key):
            enemy -= 10 + seal * 6
            seal = 0
        else:
            enemy -= 5
            seal += 1
    elif card == "Delay Lock":
        block += 4
        seal = max(0, seal - 1)
    elif card == "Deep Seal":
        enemy -= 4
        seal += 2
        key = max(0, key - 1)
        hp_loss += 8
    elif card == "Safe Hit":
        enemy -= 7
        block += 2
    elif card == "Heavy Slash":
        enemy -= 8
    elif card == "Blank":
        pass
    else:
        raise ValueError(card)
    if relic == "Keyring" and key > 0 and card in ("Seal Hit", "Release"):
        enemy -= 2
    return enemy, block, clamp(key, 5), clamp(seal, 7), hp_loss


@lru_cache(maxsize=None)
def play_options(hand: tuple[str, ...], discard: tuple[str, ...], enemy: int, key: int, seal: int, energy: int, block: int, relic: str):
    h = hand
    disc = discard
    e = enemy
    k = key
    s = seal
    en = energy
    blk = block
    hp_loss = 0
    for card in ORDER:
        while e > 0 and h.count(card) > 0 and COST[card] <= en:
            h = remove_one(h, card)
            disc = add_discard(disc, card)
            e, blk, k, s, loss = apply_card(card, e, blk, k, s, relic)
            hp_loss += loss
            en -= COST[card]
    return ((h, disc, e, k, s, en, blk, hp_loss),)


def apply_potion(potion: str, enemy: int, key: int, seal: int, block: int) -> tuple[int, int, int, int]:
    if potion == "Key":
        key += 2
    elif potion == "Guard":
        block += 14
    elif potion == "Break":
        enemy -= 12
        seal = max(0, seal - 2)
    else:
        raise ValueError(potion)
    return enemy, clamp(key, 5), clamp(seal, 7), block


def better(a: tuple[float, ...], b: tuple[float, ...] | None) -> bool:
    if b is None:
        return True
    return (sum(a[:-1]), a[0], a[1], a[2], a[3], a[4]) > (sum(b[:-1]), b[0], b[1], b[2], b[3], b[4])


def result_for(deck: tuple[str, ...], potion: str, relic: str, enemy_hp: int, player_hp: int, incoming: tuple[int, ...]) -> tuple[float, ...]:
    hand_size = min(DRAW, len(deck))
    start_block = 2 if relic == "Ward" else 0

    @lru_cache(maxsize=None)
    def solve(turn: int, hand: tuple[str, ...], draw: tuple[str, ...], discard: tuple[str, ...], hp: int, enemy: int, key: int, seal: int, potion_used: int):
        starts = [(enemy, key, seal, start_block, potion_used)]
        if not potion_used:
            pe, pk, ps, pb = apply_potion(potion, enemy, key, seal, start_block)
            starts.append((pe, pk, ps, pb, 1))
        best = None
        for start_enemy, start_key, start_seal, start_block2, used in starts:
            for h, disc, e, k, s, _en, block, card_loss in play_options(hand, discard, start_enemy, start_key, start_seal, ENERGY, start_block2, relic):
                out = [0.0] * (MAX_TURN + 1)
                if e <= 0:
                    out[turn - 1] = 1.0
                    cand = tuple(out)
                elif turn >= MAX_TURN:
                    out[-1] = 1.0
                    cand = tuple(out)
                else:
                    seal_tax = max(0, s - 2) * (2 if relic == "Ward" else 3)
                    hp2 = hp - max(0, incoming[turn - 1] - block) - card_loss - seal_tax
                    if hp2 <= 0:
                        out[-1] = 1.0
                        cand = tuple(out)
                    else:
                        discard2 = tuple(sorted(disc + h))
                        merged = [0.0] * (MAX_TURN + 1)
                        for nh, ndraw, ndiscard, prob in next_draws(draw, discard2, hand_size):
                            fut = solve(turn + 1, nh, ndraw, ndiscard, hp2, e, k, s, used)
                            for i, val in enumerate(fut):
                                merged[i] += prob * val
                        cand = tuple(merged)
                if better(cand, best):
                    best = cand
        return best or tuple([0.0] * MAX_TURN + [1.0])

    total = [0.0] * (MAX_TURN + 1)
    for hand, draw, prob in draw_outcomes(deck, hand_size):
        res = solve(1, hand, draw, (), player_hp, enemy_hp, 0, 0, 0)
        for i, x in enumerate(res):
            total[i] += prob * x
    return tuple(total)


def classify(deck: tuple[str, ...], potion: str, relic: str) -> str:
    c = Counter(deck)
    if c["Deep Seal"]:
        return "深封陷阱线"
    if c["Key"] >= 2 and c["Release"]:
        return "钥环释放线" if relic == "Keyring" else "解封释放线"
    if c["Guard Key"] and c["Seal Hit"] >= 2:
        return "护钥封击线"
    if potion == "Break" and c["Release"]:
        return "破封补刀线"
    if c["Heavy Slash"]:
        return "重斩诱导线"
    return "基础封印线"


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
    parser.add_argument("--enemy-hp", type=int, default=94)
    parser.add_argument("--player-hp", type=int, default=18)
    parser.add_argument("--incoming", default="7,15,23,32,42")
    parser.add_argument("--output", default="set2_d9_seal_audit.json")
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
