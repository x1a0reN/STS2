from __future__ import annotations

from collections import Counter, defaultdict
from dataclasses import dataclass
from functools import lru_cache
from itertools import combinations
from math import comb
import json


POOL = (
    "Strike", "Strike", "Strike",
    "Defend", "Defend",
    "Bash",
    "Neutralize",
    "QuickSlash", "QuickSlash",
    "DaggerThrow",
    "Survivor",
    "Prepared",
    "Backstab", "Backstab",
    "Finisher",
)

ORDER = (
    "Bash", "Survivor", "Prepared", "Finisher", "Backstab",
    "DaggerThrow", "QuickSlash", "Neutralize", "Strike", "Defend",
)

CN = {
    "Strike": "打击",
    "Defend": "防御",
    "Bash": "重击",
    "Neutralize": "中和",
    "QuickSlash": "快斩（改）",
    "DaggerThrow": "投掷匕首（改）",
    "Survivor": "生存者",
    "Prepared": "预备（改）",
    "Backstab": "背刺（改，狡黠）",
    "Finisher": "终结（改）",
}

COST = {
    "Strike": 1,
    "Defend": 1,
    "Bash": 2,
    "Neutralize": 0,
    "QuickSlash": 1,
    "DaggerThrow": 1,
    "Survivor": 1,
    "Prepared": 0,
    "Backstab": 1,
    "Finisher": 1,
}

ATTACKS = {"Strike", "Bash", "Neutralize", "QuickSlash", "DaggerThrow", "Backstab", "Finisher"}

POTION_CN = {
    "SlyBrew": "狡黠药水",
    "Vulnerable": "破甲药水",
    "Fire": "火焰药水",
}

RELIC_CN = {
    "SharpDice": "锋利骰子",
    "ReturnHolster": "折返皮套",
    "HollowAmulet": "空心护符",
}


@dataclass(frozen=True)
class State:
    turn: int
    hand: tuple[str, ...]
    draw: tuple[str, ...]
    discard: tuple[str, ...]
    hp: int
    enemy_hp: int
    armor: int
    vulnerable: int
    weak: int
    potion_used: bool


def sort_cards(cards) -> tuple[str, ...]:
    return tuple(sorted(cards, key=lambda c: (ORDER.index(c), c)))


def remove_one(cards: tuple[str, ...], card: str) -> tuple[str, ...]:
    out = list(cards)
    out.remove(card)
    return sort_cards(out)


def add_discard(discard: tuple[str, ...], card: str) -> tuple[str, ...]:
    return sort_cards(discard + (card,))


def attack_damage(base: int, vulnerable: int) -> int:
    return (base * 3) // 2 if vulnerable > 0 else base


def weak_damage(base: int, weak: int) -> int:
    return (base * 3) // 4 if weak > 0 else base


def deal(enemy_hp: int, armor: int, amount: int) -> tuple[int, int]:
    blocked = min(armor, amount)
    return enemy_hp - (amount - blocked), armor - blocked


def kill_vec(turn: int) -> tuple[float, ...]:
    out = [0.0] * 5
    out[turn - 1] = 1.0
    return tuple(out)


def fail_vec() -> tuple[float, ...]:
    return (0.0, 0.0, 0.0, 0.0, 1.0)


def better(a: tuple[float, ...], b: tuple[float, ...] | None) -> bool:
    if b is None:
        return True
    return (sum(a[:-1]), a[0], a[1], a[2], a[3]) > (sum(b[:-1]), b[0], b[1], b[2], b[3])


def merge(items: list[tuple[float, tuple[float, ...]]]) -> tuple[float, ...]:
    out = [0.0] * 5
    for weight, vec in items:
        for i, value in enumerate(vec):
            out[i] += weight * value
    return tuple(out)


@lru_cache(maxsize=None)
def draw_outcomes(cards: tuple[str, ...], count: int):
    cards = sort_cards(cards)
    if count >= len(cards):
        return ((cards, (), 1.0),)
    total = comb(len(cards), count)
    merged = defaultdict(float)
    for pick in combinations(range(len(cards)), count):
        picked = set(pick)
        hand = sort_cards(cards[i] for i in pick)
        rest = sort_cards(cards[i] for i in range(len(cards)) if i not in picked)
        merged[(hand, rest)] += 1 / total
    return tuple((hand, rest, prob) for (hand, rest), prob in merged.items())


@lru_cache(maxsize=None)
def next_draws(draw: tuple[str, ...], discard: tuple[str, ...], count: int):
    draw = sort_cards(draw)
    discard = sort_cards(discard)
    if len(draw) >= count:
        return tuple((hand, rest, discard, prob) for hand, rest, prob in draw_outcomes(draw, count))
    fixed = draw
    need = count - len(draw)
    if not discard:
        return ((fixed, (), (), 1.0),)
    return tuple((sort_cards(fixed + hand), rest, (), prob) for hand, rest, prob in draw_outcomes(discard, need))


def unique_decks():
    decks = set()
    for pick in combinations(range(len(POOL)), 8):
        decks.add(sort_cards(POOL[i] for i in pick))
    return sorted(decks)


def display_deck(deck: tuple[str, ...]) -> str:
    counts = Counter(deck)
    parts = []
    for card in ORDER:
        n = counts[card]
        if n == 1:
            parts.append(CN[card])
        elif n > 1:
            parts.append(f"{CN[card]} x{n}")
    return "、".join(parts)


def display_build(deck: tuple[str, ...], potion: str, relic: str) -> str:
    return f"{display_deck(deck)}；{POTION_CN[potion]}；{RELIC_CN[relic]}"


def trigger_discard(ctx: dict, card: str) -> dict:
    ctx = ctx.copy()
    ctx["hand"] = remove_one(ctx["hand"], card)
    ctx["discard_count"] += 1
    if not ctx["first_discard"]:
        ctx["first_discard"] = True
        if ctx["relic"] == "ReturnHolster":
            ctx["energy"] += 1
        elif ctx["relic"] == "HollowAmulet":
            ctx["block"] += 6
    if card == "Backstab":
        base = 12
        if ctx["relic"] == "SharpDice" and not ctx["sharp_used"]:
            base += 6
            ctx["sharp_used"] = True
        ctx["sly_count"] += 1
        ctx["enemy_hp"], ctx["armor"] = deal(ctx["enemy_hp"], ctx["armor"], attack_damage(base, ctx["vulnerable"]))
    ctx["discard"] = add_discard(ctx["discard"], card)
    return ctx


def discard_targets(ctx: dict):
    return tuple(trigger_discard(ctx, card) for card in sorted(set(ctx["hand"]), key=lambda c: ORDER.index(c)))


def base_context(state: State, potion: str, relic: str) -> list[dict]:
    base = {
        "hand": state.hand,
        "draw": state.draw,
        "discard": state.discard,
        "energy": 3,
        "block": 0,
        "enemy_hp": state.enemy_hp,
        "armor": state.armor,
        "vulnerable": state.vulnerable,
        "weak": state.weak,
        "potion_used": state.potion_used,
        "discard_count": 0,
        "sly_count": 0,
        "first_discard": False,
        "sharp_used": False,
        "relic": relic,
    }
    out = [base]
    if state.potion_used:
        return out
    if potion == "Fire":
        ctx = base.copy()
        ctx["enemy_hp"], ctx["armor"] = deal(ctx["enemy_hp"], ctx["armor"], 18)
        ctx["potion_used"] = True
        out.append(ctx)
    elif potion == "Vulnerable":
        ctx = base.copy()
        ctx["vulnerable"] += 2
        ctx["potion_used"] = True
        out.append(ctx)
    elif potion == "SlyBrew":
        if base["hand"]:
            for ctx in discard_targets({**base, "energy": 4, "potion_used": True}):
                out.append(ctx)
    return out


def maybe_play_bash(contexts: list[dict]) -> list[dict]:
    out = list(contexts)
    for ctx in contexts:
        if "Bash" in ctx["hand"] and ctx["energy"] >= 2:
            nxt = ctx.copy()
            nxt["hand"] = remove_one(nxt["hand"], "Bash")
            nxt["energy"] -= 2
            nxt["enemy_hp"], nxt["armor"] = deal(nxt["enemy_hp"], nxt["armor"], attack_damage(8, nxt["vulnerable"]))
            nxt["vulnerable"] += 2
            nxt["discard"] = add_discard(nxt["discard"], "Bash")
            out.append(nxt)
    return out


def play_discard_engines(contexts: list[dict]) -> list[dict]:
    states = list(contexts)
    for engine in ("Survivor", "Prepared"):
        new_states = list(states)
        for ctx in states:
            if engine not in ctx["hand"] or ctx["energy"] < COST[engine] or len(ctx["hand"]) <= 1:
                continue
            base = ctx.copy()
            base["hand"] = remove_one(base["hand"], engine)
            base["energy"] -= COST[engine]
            if engine == "Survivor":
                base["block"] += 8
            for nxt in discard_targets(base):
                nxt = nxt.copy()
                nxt["discard"] = add_discard(nxt["discard"], engine)
                new_states.append(nxt)
        states = new_states
    return states


def playable_subsets(hand: tuple[str, ...], energy: int):
    cards = list(hand)
    out = {()}
    for size in range(1, len(cards) + 1):
        for pick in combinations(range(len(cards)), size):
            subset = sort_cards(cards[i] for i in pick)
            if sum(COST[c] for c in subset) <= energy:
                out.add(subset)
    maximal = []
    counts = Counter(hand)
    for subset in out:
        spent = sum(COST[c] for c in subset)
        used = Counter(subset)
        if not any(used[c] < counts[c] and spent + COST[c] <= energy for c in counts):
            maximal.append(subset)
    return maximal


def play_subset(ctx: dict, subset: tuple[str, ...]) -> dict | None:
    nxt = ctx.copy()
    for card in subset:
        if card not in nxt["hand"] or nxt["energy"] < COST[card]:
            return None
        nxt["hand"] = remove_one(nxt["hand"], card)
        nxt["energy"] -= COST[card]
        if card == "Strike":
            nxt["enemy_hp"], nxt["armor"] = deal(nxt["enemy_hp"], nxt["armor"], attack_damage(6, nxt["vulnerable"]))
        elif card == "Defend":
            nxt["block"] += 5
        elif card == "Bash":
            nxt["enemy_hp"], nxt["armor"] = deal(nxt["enemy_hp"], nxt["armor"], attack_damage(8, nxt["vulnerable"]))
            nxt["vulnerable"] += 2
        elif card == "Neutralize":
            nxt["enemy_hp"], nxt["armor"] = deal(nxt["enemy_hp"], nxt["armor"], attack_damage(3, nxt["vulnerable"]))
            nxt["weak"] += 1
        elif card == "QuickSlash":
            nxt["enemy_hp"], nxt["armor"] = deal(nxt["enemy_hp"], nxt["armor"], attack_damage(8, nxt["vulnerable"]))
        elif card == "DaggerThrow":
            nxt["enemy_hp"], nxt["armor"] = deal(nxt["enemy_hp"], nxt["armor"], attack_damage(9, nxt["vulnerable"]))
        elif card == "Backstab":
            nxt["enemy_hp"], nxt["armor"] = deal(nxt["enemy_hp"], nxt["armor"], attack_damage(6, nxt["vulnerable"]))
        elif card == "Finisher":
            nxt["enemy_hp"], nxt["armor"] = deal(nxt["enemy_hp"], nxt["armor"], attack_damage(7 + 6 * nxt["sly_count"], nxt["vulnerable"]))
        nxt["discard"] = add_discard(nxt["discard"], card)
    return nxt


def turn_options(state: State, potion: str, relic: str):
    contexts = base_context(state, potion, relic)
    contexts = maybe_play_bash(contexts)
    contexts = play_discard_engines(contexts)
    final = []
    seen = set()
    for ctx in contexts:
        for subset in playable_subsets(ctx["hand"], ctx["energy"]):
            nxt = play_subset(ctx, subset)
            if nxt is None:
                continue
            key = (
                nxt["hand"], nxt["draw"], nxt["discard"], nxt["energy"], nxt["block"],
                nxt["enemy_hp"], nxt["armor"], nxt["vulnerable"], nxt["weak"],
                nxt["potion_used"], nxt["sly_count"],
            )
            if key not in seen:
                seen.add(key)
                final.append(nxt)
    return final


def end_turn(state: State, ctx: dict, potion: str, relic: str) -> tuple[float, ...]:
    if ctx["enemy_hp"] <= 0:
        return kill_vec(state.turn)
    if state.turn >= 4:
        return fail_vec()
    incoming = 0
    armor_gain = 0
    if state.turn == 1:
        incoming = weak_damage(8, ctx["weak"])
        if ctx["sly_count"] == 0:
            armor_gain = 10
    elif state.turn == 2:
        incoming = weak_damage(14, ctx["weak"])
        if ctx["sly_count"] == 0:
            armor_gain = 14
    elif state.turn == 3:
        incoming = weak_damage(22, ctx["weak"])
    hp = state.hp - max(0, incoming - ctx["block"])
    if hp <= 0:
        return fail_vec()
    discard = sort_cards(ctx["discard"] + ctx["hand"])
    weighted = []
    for hand, draw, next_discard, prob in next_draws(ctx["draw"], discard, 5):
        nxt = State(
            state.turn + 1,
            hand,
            draw,
            next_discard,
            hp,
            ctx["enemy_hp"],
            ctx["armor"] + armor_gain,
            max(0, ctx["vulnerable"] - 1),
            max(0, ctx["weak"] - 1),
            ctx["potion_used"],
        )
        weighted.append((prob, solve(nxt, potion, relic)))
    return merge(weighted)


@lru_cache(maxsize=None)
def solve(state: State, potion: str, relic: str) -> tuple[float, ...]:
    best = None
    for ctx in turn_options(state, potion, relic):
        vec = end_turn(state, ctx, potion, relic)
        if better(vec, best):
            best = vec
    return best or fail_vec()


def result_for(deck: tuple[str, ...], potion: str, relic: str, enemy_hp: int = 106) -> tuple[float, ...]:
    weighted = []
    for hand, draw, prob in draw_outcomes(deck, 5):
        state = State(1, hand, draw, (), 22, enemy_hp, 0, 0, 0, False)
        weighted.append((prob, solve(state, potion, relic)))
    return merge(weighted)


def first_turn(vec: tuple[float, ...]) -> int | None:
    for i, p in enumerate(vec[:-1], 1):
        if p > 1e-9:
            return i
    return None


def classify(deck: tuple[str, ...], potion: str, relic: str) -> str:
    c = Counter(deck)
    if c["Backstab"] >= 2 and potion == "SlyBrew":
        return "药水狡黠快线"
    if c["Backstab"] >= 2 and relic == "SharpDice":
        return "锋利骰子狡黠爆发线"
    if c["Finisher"] and (c["Prepared"] or c["Survivor"]):
        return "狡黠终结线"
    if relic == "HollowAmulet" and c["Survivor"]:
        return "弃牌格挡稳线"
    if potion == "Vulnerable" and c["Bash"]:
        return "双易伤攻击线"
    if potion == "Fire":
        return "火焰补刀线"
    return "混合线"


def pct(x: float) -> float:
    return round(x * 100, 4)


def run(enemy_hp: int = 106) -> dict:
    decks = unique_decks()
    rows = []
    total = len(decks) * len(POTION_CN) * len(RELIC_CN)
    done = 0
    for deck in decks:
        for potion in POTION_CN:
            for relic in RELIC_CN:
                done += 1
                vec = result_for(deck, potion, relic, enemy_hp)
                row = {
                    "deck": list(deck),
                    "potion": potion,
                    "relic": relic,
                    "build_display": display_build(deck, potion, relic),
                    "family": classify(deck, potion, relic),
                    "first_turn": first_turn(vec),
                    "kill_vector": [pct(x) for x in vec],
                    "success": pct(sum(vec[:-1])),
                    "fail": pct(vec[-1]),
                }
                rows.append(row)
                if done % 250 == 0 or done == total:
                    print(f"audited {done}/{total}", flush=True)
    rows.sort(key=lambda r: (r["success"], -(r["first_turn"] or 99), r["kill_vector"][0], r["kill_vector"][1], r["kill_vector"][2]), reverse=True)
    by_turn = defaultdict(list)
    by_family = defaultdict(list)
    for row in rows:
        by_turn[str(row["first_turn"])].append(row)
        by_family[row["family"]].append(row)
    return {
        "enemy_hp": enemy_hp,
        "legal_deck_count": len(decks),
        "legal_build_count": len(rows),
        "perfect_success_count": sum(1 for r in rows if r["success"] >= 99.9999),
        "top30": rows[:30],
        "best_by_turn": {k: v[:10] for k, v in by_turn.items()},
        "best_by_family": {k: v[:10] for k, v in by_family.items()},
    }


def main() -> None:
    summary = run()
    with open("difficulty3_sly_v4_audit.json", "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)
    print("enemy_hp", summary["enemy_hp"])
    print("legal_deck_count", summary["legal_deck_count"])
    print("legal_build_count", summary["legal_build_count"])
    print("perfect_success_count", summary["perfect_success_count"])
    print("top30")
    for row in summary["top30"]:
        print(row["success"], "first", row["first_turn"], row["family"], row["kill_vector"], row["build_display"])
    print("best_by_turn")
    for turn, rows in sorted(summary["best_by_turn"].items(), key=lambda item: 99 if item[0] == "None" else int(item[0])):
        row = rows[0]
        print(turn, row["success"], row["family"], row["kill_vector"], row["build_display"])


if __name__ == "__main__":
    main()
