from __future__ import annotations

from collections import Counter, defaultdict
from dataclasses import dataclass
from functools import lru_cache
from itertools import combinations
from math import comb
import json


POOL = (
    "Bash", "Prepared", "Survivor", "Finisher",
    "Backstab", "Backstab",
    "DaggerThrow",
    "QuickFeint", "QuickFeint",
    "Neutralize",
    "Strike", "Strike", "Strike",
    "Defend", "Defend",
    "ShadowStep", "ShadowStep",
    "Feint", "Feint",
)

ORDER = (
    "Bash", "Prepared", "Survivor", "Finisher", "Backstab",
    "DaggerThrow", "QuickFeint", "Neutralize", "Strike", "Defend",
    "ShadowStep", "Feint",
)

CN = {
    "Bash": "重击",
    "Prepared": "预备（改）",
    "Survivor": "生存者（改）",
    "Finisher": "终结（改）",
    "Backstab": "背刺（改，狡黠）",
    "DaggerThrow": "匕首投掷（改，非狡黠）",
    "QuickFeint": "佯刺（改，非狡黠）",
    "Neutralize": "中和",
    "Strike": "打击",
    "Defend": "防御",
    "ShadowStep": "影步（改，狡黠）",
    "Feint": "虚刃（改，非狡黠）",
}

COST = {
    "Bash": 2,
    "Prepared": 0,
    "Survivor": 1,
    "Finisher": 1,
    "Backstab": 1,
    "DaggerThrow": 1,
    "QuickFeint": 1,
    "Neutralize": 0,
    "Strike": 1,
    "Defend": 1,
    "ShadowStep": 1,
    "Feint": 0,
}

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

SLY = {"Backstab", "ShadowStep"}
ENGINE = {"Prepared", "Survivor"}
ACTIVE_CAP = 2
DRAW = 5


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


def add_one(cards: tuple[str, ...], card: str) -> tuple[str, ...]:
    return sort_cards(cards + (card,))


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


def success(vec: tuple[float, ...]) -> float:
    return sum(vec[:-1])


def loose_upper_bound(state: State, potion: str) -> int:
    turns_left = 5 - state.turn
    if turns_left <= 0:
        return 0
    damage = {
        "Bash": 12,
        "Prepared": 0,
        "Survivor": 0,
        "Finisher": 20,
        "Backstab": 9,
        "DaggerThrow": 13,
        "QuickFeint": 12,
        "Neutralize": 4,
        "Strike": 9,
        "Defend": 0,
        "ShadowStep": 7,
        "Feint": 4,
    }
    cards = Counter(state.hand + state.draw + state.discard)
    active_hits = []
    for card, n in cards.items():
        active_hits.extend([damage[card]] * n)
    active_hits.sort(reverse=True)
    active = sum(active_hits[: ACTIVE_CAP * turns_left])
    sly = min(cards["Backstab"] + cards["ShadowStep"], turns_left) * 19
    potion_hit = 18 if (not state.potion_used and potion == "Fire") else 0
    return active + sly + potion_hit - state.armor


def better(a: tuple[float, ...], b: tuple[float, ...] | None) -> bool:
    if b is None:
        return True
    return (success(a), a[0], a[1], a[2], a[3]) > (success(b), b[0], b[1], b[2], b[3])


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


def trigger_discard(ctx: dict, card: str) -> dict:
    ctx = ctx.copy()
    ctx["hand"] = remove_one(ctx["hand"], card)
    if not ctx["first_discard"]:
        ctx["first_discard"] = True
        if ctx["relic"] == "ReturnHolster":
            ctx["energy"] += 1
        elif ctx["relic"] == "HollowAmulet":
            ctx["block"] += 6
    if card in SLY:
        base = 10 if card == "Backstab" else 7
        if card == "ShadowStep":
            ctx["block"] += 4
        if card == "Backstab" and ctx["relic"] == "SharpDice" and not ctx["sharp_used"] and ctx["block"] == 0:
            base += 2
            ctx["sharp_used"] = True
        ctx["sly_count"] += 1
        ctx["enemy_hp"], ctx["armor"] = deal(ctx["enemy_hp"], ctx["armor"], attack_damage(base, ctx["vulnerable"]))
    else:
        ctx["bad_discard"] = True
    ctx["discard"] = add_one(ctx["discard"], card)
    return ctx


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
        "first_discard": False,
        "sharp_used": False,
        "sly_count": 0,
        "active_sly": 0,
        "bad_discard": False,
        "played": 0,
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
        b = base.copy()
        b["potion_used"] = True
        for card in sorted(set(b["hand"]), key=ORDER.index):
            out.append(trigger_discard(b, card))
    return out


def maybe_play_bash(contexts: list[dict]) -> list[dict]:
    out = list(contexts)
    for ctx in contexts:
        if ctx["played"] >= ACTIVE_CAP or "Bash" not in ctx["hand"] or ctx["energy"] < 2:
            continue
        nxt = ctx.copy()
        nxt["hand"] = remove_one(nxt["hand"], "Bash")
        nxt["energy"] -= 2
        nxt["played"] += 1
        nxt["enemy_hp"], nxt["armor"] = deal(nxt["enemy_hp"], nxt["armor"], attack_damage(8, nxt["vulnerable"]))
        nxt["vulnerable"] += 2
        nxt["discard"] = add_one(nxt["discard"], "Bash")
        out.append(nxt)
    return out


def play_engine(ctx: dict, engine: str) -> list[dict]:
    if ctx["first_discard"] or ctx["played"] >= ACTIVE_CAP or engine not in ctx["hand"] or ctx["energy"] < COST[engine] or len(ctx["hand"]) <= 1:
        return []
    base = ctx.copy()
    base["hand"] = remove_one(base["hand"], engine)
    base["energy"] -= COST[engine]
    base["played"] += 1
    if engine == "Survivor":
        base["block"] += 5
    out = []
    for card in sorted(set(base["hand"]), key=ORDER.index):
        nxt = trigger_discard(base, card)
        nxt["discard"] = add_one(nxt["discard"], engine)
        out.append(nxt)
    return out


def gen_subsets(hand: tuple[str, ...], energy: int, max_cards: int):
    counts = Counter(hand)
    cards = [card for card in ORDER if counts[card] and card not in ENGINE]
    out = []

    def rec(i: int, spent: int, used: int, cur: list[str]) -> None:
        if i == len(cards):
            out.append(tuple(cur))
            return
        card = cards[i]
        for n in range(min(counts[card], max_cards - used) + 1):
            ns = spent + n * COST[card]
            if ns <= energy:
                cur.extend([card] * n)
                rec(i + 1, ns, used + n, cur)
                if n:
                    del cur[-n:]

    rec(0, 0, 0, [])
    return out


def play_subset(ctx: dict, subset: tuple[str, ...]) -> dict | None:
    nxt = ctx.copy()
    if nxt["played"] + len(subset) > ACTIVE_CAP:
        return None
    for card in subset:
        if card not in nxt["hand"] or nxt["energy"] < COST[card]:
            return None
        nxt["hand"] = remove_one(nxt["hand"], card)
        nxt["energy"] -= COST[card]
        nxt["played"] += 1
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
        elif card == "QuickFeint":
            nxt["enemy_hp"], nxt["armor"] = deal(nxt["enemy_hp"], nxt["armor"], attack_damage(8, nxt["vulnerable"]))
        elif card == "DaggerThrow":
            nxt["enemy_hp"], nxt["armor"] = deal(nxt["enemy_hp"], nxt["armor"], attack_damage(9, nxt["vulnerable"]))
        elif card == "Feint":
            nxt["enemy_hp"], nxt["armor"] = deal(nxt["enemy_hp"], nxt["armor"], attack_damage(4, nxt["vulnerable"]))
        elif card == "Backstab":
            nxt["enemy_hp"], nxt["armor"] = deal(nxt["enemy_hp"], nxt["armor"], attack_damage(6, nxt["vulnerable"]))
            nxt["active_sly"] += 1
        elif card == "ShadowStep":
            nxt["enemy_hp"], nxt["armor"] = deal(nxt["enemy_hp"], nxt["armor"], attack_damage(5, nxt["vulnerable"]))
            nxt["block"] += 3
            nxt["active_sly"] += 1
        elif card == "Finisher":
            nxt["enemy_hp"], nxt["armor"] = deal(nxt["enemy_hp"], nxt["armor"], attack_damage(8 + 6 * nxt["sly_count"], nxt["vulnerable"]))
        nxt["discard"] = add_one(nxt["discard"], card)
    return nxt


def turn_options(state: State, potion: str, relic: str):
    starts = base_context(state, potion, relic)
    pre = maybe_play_bash(starts)
    for ctx in list(pre):
        pre.extend(play_engine(ctx, "Prepared"))
        pre.extend(play_engine(ctx, "Survivor"))
    pre = maybe_play_bash(pre)
    final = []
    seen = set()
    for ctx in pre:
        for sub in gen_subsets(ctx["hand"], ctx["energy"], ACTIVE_CAP - ctx["played"]):
            nxt = play_subset(ctx, sub)
            if nxt is None:
                continue
            key = (
                nxt["hand"], nxt["draw"], nxt["discard"], nxt["energy"], nxt["block"],
                nxt["enemy_hp"], nxt["armor"], nxt["vulnerable"], nxt["weak"],
                nxt["potion_used"], nxt["sly_count"], nxt["active_sly"], nxt["bad_discard"],
            )
            if key not in seen:
                seen.add(key)
                final.append(nxt)
    return final


@lru_cache(maxsize=None)
def solve(state: State, potion: str, relic: str) -> tuple[float, ...]:
    if state.enemy_hp > loose_upper_bound(state, potion):
        return fail_vec()
    best = None
    for ctx in turn_options(state, potion, relic):
        if ctx["enemy_hp"] <= 0:
            vec = kill_vec(state.turn)
        elif state.turn >= 4:
            vec = fail_vec()
        else:
            incoming = 0
            armor_gain = 0
            if state.turn == 1:
                incoming = weak_damage(8, ctx["weak"])
                if ctx["sly_count"] == 0:
                    armor_gain = 14
            elif state.turn == 2:
                incoming = weak_damage(22, ctx["weak"])
                if ctx["sly_count"] == 0:
                    armor_gain = 20
            elif state.turn == 3:
                incoming = weak_damage(22, ctx["weak"])
                if ctx["sly_count"] == 0:
                    armor_gain = 8
            armor_gain += 8 * ctx["active_sly"]
            if ctx["bad_discard"]:
                armor_gain += 6
            hp = state.hp - max(0, incoming - ctx["block"])
            if hp <= 0:
                vec = fail_vec()
            else:
                discard = sort_cards(ctx["discard"] + ctx["hand"])
                weighted = []
                for hand, draw, next_discard, prob in next_draws(ctx["draw"], discard, DRAW):
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
                vec = merge(weighted)
        if better(vec, best):
            best = vec
    return best or fail_vec()


def legal_decks():
    out = set()
    for pick in combinations(range(len(POOL)), 8):
        deck = sort_cards(POOL[i] for i in pick)
        c = Counter(deck)
        if c["Prepared"] + c["Survivor"] == 1:
            out.add(deck)
    return sorted(out)


def result_for(deck: tuple[str, ...], potion: str, relic: str, enemy_hp: int, player_hp: int):
    weighted = []
    for hand, draw, prob in draw_outcomes(deck, DRAW):
        weighted.append((prob, solve(State(1, hand, draw, (), player_hp, enemy_hp, 0, 0, 0, False), potion, relic)))
    return merge(weighted)


def first_turn(vec: tuple[float, ...]) -> int:
    for i, p in enumerate(vec[:-1], 1):
        if p > 1e-9:
            return i
    return 0


def display_deck(deck: tuple[str, ...]) -> str:
    c = Counter(deck)
    parts = []
    for card in ORDER:
        if c[card] == 1:
            parts.append(CN[card])
        elif c[card] > 1:
            parts.append(f"{CN[card]} x{c[card]}")
    return "、".join(parts)


def family(deck: tuple[str, ...], potion: str, relic: str) -> str:
    c = Counter(deck)
    if potion == "SlyBrew" and relic == "SharpDice":
        return "药水骰子双狡黠快线"
    if relic == "SharpDice":
        return "锋利骰子双狡黠爆发线"
    if c["Finisher"] and c["ShadowStep"] and (c["Prepared"] or c["Survivor"]):
        return "双狡黠终结线"
    if c["DaggerThrow"] or c["QuickFeint"] or c["Feint"]:
        return "伪狡黠干扰线"
    if c["Survivor"] and relic == "HollowAmulet":
        return "弃牌格挡稳线"
    if c["Bash"] and potion == "Vulnerable":
        return "双易伤攻击线"
    if potion == "Fire":
        return "火焰补刀线"
    return "混合线"


def pct(x: float) -> float:
    return round(x * 100, 4)


def run(enemy_hp: int = 78, player_hp: int = 22) -> dict:
    decks = legal_decks()
    rows = []
    total = len(decks) * len(POTION_CN) * len(RELIC_CN)
    done = 0
    for deck in decks:
        for potion in POTION_CN:
            for relic in RELIC_CN:
                done += 1
                vec = result_for(deck, potion, relic, enemy_hp, player_hp)
                rows.append({
                    "success": pct(success(vec)),
                    "first_turn": first_turn(vec),
                    "family": family(deck, potion, relic),
                    "build_display": f"{display_deck(deck)}；{POTION_CN[potion]}；{RELIC_CN[relic]}",
                    "kill_vector": [pct(x) for x in vec],
                })
                if done % 10 == 0 or done == total:
                    print(f"audited {done}/{total}", flush=True)
    rows.sort(key=lambda r: (r["success"], -(r["first_turn"] or 99), r["kill_vector"][0], r["kill_vector"][1], r["kill_vector"][2]), reverse=True)
    best_by_first_turn = {}
    best_by_family = {}
    for row in rows:
        t = str(row["first_turn"])
        best_by_first_turn.setdefault(t, row)
        best_by_family.setdefault(row["family"], row)
    return {
        "enemy_hp": enemy_hp,
        "player_hp": player_hp,
        "legal_deck_count": len(decks),
        "legal_build_count": len(rows),
        "perfect_success_count": sum(1 for r in rows if r["success"] >= 99.9999),
        "top30": rows[:30],
        "best_by_first_turn": best_by_first_turn,
        "best_by_family": best_by_family,
    }


def main() -> None:
    summary = run()
    with open("difficulty3_double_sly_audit.json", "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)
    print("enemy_hp", summary["enemy_hp"], "player_hp", summary["player_hp"])
    print("legal_deck_count", summary["legal_deck_count"])
    print("legal_build_count", summary["legal_build_count"])
    print("perfect_success_count", summary["perfect_success_count"])
    print("top30")
    for row in summary["top30"]:
        print(row["success"], "first", row["first_turn"], row["family"], row["kill_vector"], row["build_display"])
    print("best_by_first_turn")
    for turn, row in sorted(summary["best_by_first_turn"].items(), key=lambda item: int(item[0])):
        print(turn, row["success"], row["family"], row["kill_vector"], row["build_display"])
    print("best_by_family")
    for name, row in summary["best_by_family"].items():
        print(name, row["success"], "first", row["first_turn"], row["kill_vector"], row["build_display"])


if __name__ == "__main__":
    main()
