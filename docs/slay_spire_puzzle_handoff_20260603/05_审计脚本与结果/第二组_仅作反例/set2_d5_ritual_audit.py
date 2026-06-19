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
    "Interrupt", "Interrupt",
    "Offering", "Offering",
    "Rite Guard",
    "Payoff",
    "Seal Cut",
    "False Interrupt",
    "Safe Hit",
    "Heavy Slash",
)

POTIONS = ("Silence", "Offering", "Guard")
RELICS = ("Bell", "Bowl")

CN = {
    "Strike": "打击",
    "Defend": "防御",
    "Interrupt": "断仪斩（改）",
    "Offering": "献祭刻痕（改）",
    "Rite Guard": "仪式格挡（改）",
    "Payoff": "献值收束（改）",
    "Seal Cut": "封仪切（改）",
    "False Interrupt": "假打断（改）",
    "Safe Hit": "稳击",
    "Heavy Slash": "重斩",
}

POTION_CN = {
    "Silence": "静默药水",
    "Offering": "献祭药水",
    "Guard": "护壳药水",
}

RELIC_CN = {
    "Bell": "止仪铃",
    "Bowl": "献祭碗",
}

COST = {
    "Strike": 1,
    "Defend": 1,
    "Interrupt": 1,
    "Offering": 0,
    "Rite Guard": 1,
    "Payoff": 2,
    "Seal Cut": 1,
    "False Interrupt": 0,
    "Safe Hit": 1,
    "Heavy Slash": 2,
}

ORDER = (
    "Offering",
    "Rite Guard",
    "Interrupt",
    "False Interrupt",
    "Seal Cut",
    "Payoff",
    "Safe Hit",
    "Heavy Slash",
    "Strike",
    "Defend",
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


def clamp_ritual(x: int) -> int:
    return max(0, min(5, x))


def clamp_sac(x: int) -> int:
    return max(0, min(8, x))


def apply_card(card: str, enemy: int, block: int, ritual: int, sac: int, relic: str) -> tuple[int, int, int, int, int]:
    hp_loss = 0
    bowl_bonus = 0
    if card == "Strike":
        enemy -= 6
    elif card == "Defend":
        block += 5
    elif card == "Interrupt":
        enemy -= 4
        ritual -= 2
    elif card == "Offering":
        enemy -= 3
        sac += 1 + bowl_bonus
        hp_loss += 1
    elif card == "Rite Guard":
        block += 7
        sac += 1
    elif card == "Payoff":
        enemy -= 8 + sac * 5
        sac = 0
    elif card == "Seal Cut":
        enemy -= 6
        ritual -= 1
        sac += 1
    elif card == "False Interrupt":
        enemy -= 5
        ritual += 3
        hp_loss += 2
    elif card == "Safe Hit":
        enemy -= 7
        block += 2
    elif card == "Heavy Slash":
        enemy -= 14
    else:
        raise ValueError(card)
    return enemy, block, clamp_ritual(ritual), clamp_sac(sac), hp_loss


@lru_cache(maxsize=None)
def play_options(hand: tuple[str, ...], discard: tuple[str, ...], enemy: int, ritual: int, sac: int, energy: int, block: int, relic: str):
    h = hand
    disc = discard
    e = enemy
    r = ritual
    s = sac
    en = energy
    b = block
    hp_loss = 0
    for card in ORDER:
        while e > 0 and h.count(card) > 0 and COST[card] <= en:
            h = remove_one(h, card)
            disc = add_discard(disc, card)
            e, b, r, s, loss = apply_card(card, e, b, r, s, relic)
            hp_loss += loss
            en -= COST[card]
    return ((h, disc, e, r, s, en, b, hp_loss),)


def apply_potion(potion: str, enemy: int, ritual: int, sac: int, block: int) -> tuple[int, int, int, int, int]:
    hp_loss = 0
    if potion == "Silence":
        ritual = 0
        block += 3
    elif potion == "Offering":
        sac += 2
        hp_loss += 2
    elif potion == "Guard":
        block += 14
    else:
        raise ValueError(potion)
    return enemy, clamp_ritual(ritual), clamp_sac(sac), block, hp_loss


def better(a: tuple[float, ...], b: tuple[float, ...] | None) -> bool:
    if b is None:
        return True
    return (sum(a[:-1]), a[0], a[1], a[2], a[3], a[4]) > (sum(b[:-1]), b[0], b[1], b[2], b[3], b[4])


def result_for(deck: tuple[str, ...], potion: str, relic: str, enemy_hp: int, player_hp: int, incoming: tuple[int, ...]) -> tuple[float, ...]:
    hand_size = min(DRAW, len(deck))
    start_ritual = 0 if relic == "Bell" else 1
    start_sac = 1 if relic == "Bowl" else 0

    @lru_cache(maxsize=None)
    def solve(turn: int, hand: tuple[str, ...], draw: tuple[str, ...], discard: tuple[str, ...], hp: int, enemy: int, ritual: int, sac: int, potion_used: int):
        ritual_now = clamp_ritual(ritual + 1)
        if ritual_now >= 5:
            return tuple([0.0] * MAX_TURN + [1.0])

        starts = [(enemy, ritual_now, sac, 0, 0, potion_used)]
        if not potion_used:
            pe, pr, ps, pb, loss = apply_potion(potion, enemy, ritual_now, sac, 0)
            starts.append((pe, pr, ps, pb, loss, 1))

        best = None
        for start_enemy, start_ritual2, start_sac, start_block, potion_loss, used in starts:
            for h, disc, e, r, s, _en, block, card_loss in play_options(hand, discard, start_enemy, start_ritual2, start_sac, ENERGY, start_block, relic):
                out = [0.0] * (MAX_TURN + 1)
                if e <= 0:
                    out[turn - 1] = 1.0
                    cand = tuple(out)
                elif turn >= MAX_TURN:
                    out[-1] = 1.0
                    cand = tuple(out)
                else:
                    ritual_tax = r * 2
                    hp2 = hp - max(0, incoming[turn - 1] - block) - potion_loss - card_loss - ritual_tax
                    if hp2 <= 0:
                        out[-1] = 1.0
                        cand = tuple(out)
                    else:
                        discard2 = tuple(sorted(disc + h))
                        merged = [0.0] * (MAX_TURN + 1)
                        for nh, ndraw, ndiscard, p in next_draws(draw, discard2, hand_size):
                            fut = solve(turn + 1, nh, ndraw, ndiscard, hp2, e, r, s, used)
                            for i, val in enumerate(fut):
                                merged[i] += p * val
                        cand = tuple(merged)
                if better(cand, best):
                    best = cand
        return best or tuple([0.0] * MAX_TURN + [1.0])

    total = [0.0] * (MAX_TURN + 1)
    for hand, draw, p in draw_outcomes(deck, hand_size):
        res = solve(1, hand, draw, (), player_hp, enemy_hp, start_ritual, start_sac, 0)
        for i, x in enumerate(res):
            total[i] += p * x
    return tuple(total)


def classify(deck: tuple[str, ...], potion: str, relic: str) -> str:
    c = Counter(deck)
    if c["False Interrupt"]:
        return "假打断陷阱线"
    if relic == "Bowl" and c["Offering"] and c["Payoff"]:
        return "献祭碗收束线"
    if potion == "Silence" and c["Interrupt"] and c["Payoff"]:
        return "静默打断线"
    if c["Interrupt"] and c["Seal Cut"] and c["Payoff"]:
        return "封仪节奏线"
    if c["Heavy Slash"]:
        return "重斩诱导线"
    return "基础仪式线"


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
    parser.add_argument("--enemy-hp", type=int, default=94)
    parser.add_argument("--player-hp", type=int, default=20)
    parser.add_argument("--incoming", default="6,12,18,24,32")
    parser.add_argument("--output", default="set2_d5_ritual_audit.json")
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
