from __future__ import annotations

from collections import Counter, defaultdict
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
    "Bash", "Prepared", "Survivor", "Finisher", "Backstab",
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

ATTACKS = {"Strike", "Bash", "Neutralize", "QuickSlash", "DaggerThrow", "Backstab", "Finisher"}
ENGINES = ("Prepared", "Survivor")


def sort_cards(cards) -> tuple[str, ...]:
    return tuple(sorted(cards, key=lambda c: (ORDER.index(c), c)))


def remove_one(cards: tuple[str, ...], card: str) -> tuple[str, ...]:
    out = list(cards)
    out.remove(card)
    return sort_cards(out)


def add_card(cards: tuple[str, ...], card: str) -> tuple[str, ...]:
    return sort_cards(cards + (card,))


def attack_damage(base: int, vuln: int) -> int:
    return (base * 3) // 2 if vuln > 0 else base


def weak_damage(base: int, weak: int) -> int:
    return (base * 3) // 4 if weak > 0 else base


def deal(enemy: int, armor: int, amount: int) -> tuple[int, int]:
    blocked = min(armor, amount)
    return enemy - (amount - blocked), armor - blocked


def kill_vec(turn: int) -> tuple[float, ...]:
    out = [0.0] * 5
    out[turn - 1] = 1.0
    return tuple(out)


FAIL_VEC = (0.0, 0.0, 0.0, 0.0, 1.0)


def better(a: tuple[float, ...], b: tuple[float, ...] | None) -> bool:
    if b is None:
        return True
    return (sum(a[:-1]), a[0], a[1], a[2], a[3]) > (sum(b[:-1]), b[0], b[1], b[2], b[3])


def merge(items: list[tuple[float, tuple[float, ...]]]) -> tuple[float, ...]:
    out = [0.0] * 5
    for weight, vec in items:
        for idx, value in enumerate(vec):
            out[idx] += weight * value
    return tuple(out)


@lru_cache(maxsize=None)
def draw_outcomes(cards: tuple[str, ...], n: int):
    cards = sort_cards(cards)
    if n >= len(cards):
        return ((cards, (), 1.0),)
    total = comb(len(cards), n)
    merged = defaultdict(float)
    for pick in combinations(range(len(cards)), n):
        picked = set(pick)
        hand = sort_cards(cards[i] for i in pick)
        rest = sort_cards(cards[i] for i in range(len(cards)) if i not in picked)
        merged[(hand, rest)] += 1 / total
    return tuple((hand, rest, prob) for (hand, rest), prob in merged.items())


@lru_cache(maxsize=None)
def next_draws(draw: tuple[str, ...], discard: tuple[str, ...], n: int):
    draw = sort_cards(draw)
    discard = sort_cards(discard)
    if len(draw) >= n:
        return tuple((hand, rest, discard, prob) for hand, rest, prob in draw_outcomes(draw, n))
    fixed = draw
    need = n - len(draw)
    if not discard:
        return ((fixed, (), (), 1.0),)
    return tuple((sort_cards(fixed + hand), rest, (), prob) for hand, rest, prob in draw_outcomes(discard, need))


def unique_decks() -> list[tuple[str, ...]]:
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


def ctx_key(ctx: dict) -> tuple:
    return (
        ctx["hand"], ctx["draw"], ctx["discard"], ctx["energy"], ctx["block"],
        ctx["enemy"], ctx["armor"], ctx["vuln"], ctx["weak"], ctx["potion_used"],
        ctx["sly"], ctx["first_discard"], ctx["sharp_used"],
    )


def dedupe(contexts: list[dict]) -> list[dict]:
    seen = set()
    out = []
    for ctx in contexts:
        key = ctx_key(ctx)
        if key not in seen:
            seen.add(key)
            out.append(ctx)
    return out


def trigger_first_discard(ctx: dict) -> None:
    if ctx["first_discard"]:
        return
    ctx["first_discard"] = True
    if ctx["relic"] == "ReturnHolster":
        ctx["energy"] += 1
    elif ctx["relic"] == "HollowAmulet":
        ctx["block"] += 6


def discard_card(ctx: dict, card: str) -> dict:
    ctx = ctx.copy()
    ctx["hand"] = remove_one(ctx["hand"], card)
    trigger_first_discard(ctx)
    if card == "Backstab":
        dmg = 12
        if ctx["relic"] == "SharpDice" and not ctx["sharp_used"]:
            dmg += 6
            ctx["sharp_used"] = True
        ctx["sly"] += 1
        ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], attack_damage(dmg, ctx["vuln"]))
    ctx["discard"] = add_card(ctx["discard"], card)
    return ctx


def play_bash(ctx: dict) -> dict | None:
    if "Bash" not in ctx["hand"] or ctx["energy"] < 2:
        return None
    ctx = ctx.copy()
    ctx["hand"] = remove_one(ctx["hand"], "Bash")
    ctx["energy"] -= 2
    ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], attack_damage(8, ctx["vuln"]))
    ctx["vuln"] += 2
    ctx["discard"] = add_card(ctx["discard"], "Bash")
    return ctx


def play_engine(ctx: dict, engine: str) -> list[dict]:
    if ctx["first_discard"]:
        return []
    if engine not in ctx["hand"] or ctx["energy"] < COST[engine] or len(ctx["hand"]) <= 1:
        return []
    base = ctx.copy()
    base["hand"] = remove_one(base["hand"], engine)
    base["energy"] -= COST[engine]
    if engine == "Survivor":
        base["block"] += 8
    out = []
    for target in sorted(set(base["hand"]), key=lambda c: ORDER.index(c)):
        nxt = discard_card(base, target)
        nxt["discard"] = add_card(nxt["discard"], engine)
        out.append(nxt)
    return out


def play_normal_subset(ctx: dict, subset: tuple[str, ...]) -> dict | None:
    if sum(COST[c] for c in subset) > ctx["energy"]:
        return None
    ctx = ctx.copy()
    for card in subset:
        if card not in ctx["hand"]:
            return None
        ctx["hand"] = remove_one(ctx["hand"], card)
        ctx["energy"] -= COST[card]
        if card == "Strike":
            ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], attack_damage(6, ctx["vuln"]))
        elif card == "Defend":
            ctx["block"] += 5
        elif card == "Neutralize":
            ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], attack_damage(3, ctx["vuln"]))
            ctx["weak"] += 1
        elif card == "QuickSlash":
            ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], attack_damage(8, ctx["vuln"]))
        elif card == "DaggerThrow":
            ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], attack_damage(9, ctx["vuln"]))
        elif card == "Backstab":
            ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], attack_damage(6, ctx["vuln"]))
        elif card == "Finisher":
            ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], attack_damage(7 + 6 * ctx["sly"], ctx["vuln"]))
        else:
            return None
        ctx["discard"] = add_card(ctx["discard"], card)
    return ctx


@lru_cache(maxsize=None)
def playable_normal_subsets(hand: tuple[str, ...], energy: int, bash_available: bool):
    banned = set(ENGINES)
    if not bash_available:
        banned.add("Bash")
    cards = [c for c in hand if c not in banned]
    out = {()}
    for size in range(1, len(cards) + 1):
        for pick in combinations(range(len(cards)), size):
            subset = tuple(cards[i] for i in pick)
            subset = sort_cards(subset)
            if sum(COST[c] for c in subset) <= energy:
                out.add(subset)
    # Only keep energy-maximal subsets; playing extra available cards has no drawback in this ruleset.
    maximal = []
    counts = Counter(cards)
    for subset in out:
        used = Counter(subset)
        spent = sum(COST[c] for c in subset)
        if not any(used[c] < counts[c] and spent + COST[c] <= energy for c in counts):
            maximal.append(subset)
    return tuple(maximal)


def potion_contexts(base: dict, potion: str) -> list[dict]:
    out = [base]
    if base["potion_used"]:
        return out
    if potion == "Fire":
        ctx = base.copy()
        ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], 18)
        ctx["potion_used"] = True
        out.append(ctx)
    elif potion == "Vulnerable":
        ctx = base.copy()
        ctx["vuln"] += 2
        ctx["potion_used"] = True
        out.append(ctx)
    elif potion == "SlyBrew" and base["hand"]:
        brew = base.copy()
        brew["energy"] += 1
        brew["potion_used"] = True
        for target in sorted(set(brew["hand"]), key=lambda c: ORDER.index(c)):
            out.append(discard_card(brew, target))
    return out


@lru_cache(maxsize=None)
def turn_end_contexts(
    turn: int,
    hand: tuple[str, ...],
    draw: tuple[str, ...],
    discard: tuple[str, ...],
    hp: int,
    enemy: int,
    armor: int,
    vuln: int,
    weak: int,
    potion_used: bool,
    potion: str,
    relic: str,
):
    base = {
        "hand": hand, "draw": draw, "discard": discard,
        "energy": 3, "block": 0,
        "enemy": enemy, "armor": armor,
        "vuln": vuln, "weak": weak,
        "potion_used": potion_used,
        "sly": 0, "first_discard": False, "sharp_used": False,
        "relic": relic,
    }

    contexts = potion_contexts(base, potion)
    final = []
    engine_orders = ((), ("Prepared",), ("Survivor",))
    for ctx0 in contexts:
        pre_bash_options = [ctx0]
        pre = play_bash(ctx0)
        if pre is not None:
            pre_bash_options.append(pre)
        for ctx1 in pre_bash_options:
            for order in engine_orders:
                states = [ctx1]
                ok = True
                for engine in order:
                    next_states = []
                    for st in states:
                        next_states.extend(play_engine(st, engine))
                    if not next_states:
                        ok = False
                        break
                    states = next_states
                if not ok:
                    continue
                for ctx2 in states:
                    post_options = [ctx2]
                    if "Bash" in ctx2["hand"]:
                        post = play_bash(ctx2)
                        if post is not None:
                            post_options.append(post)
                    for ctx3 in post_options:
                        bash_available = "Bash" in ctx3["hand"]
                        for subset in playable_normal_subsets(ctx3["hand"], ctx3["energy"], bash_available):
                            nxt = play_normal_subset(ctx3, subset)
                            if nxt is not None:
                                final.append(nxt)
    return tuple(ctx_key(ctx) + (ctx["relic"],) for ctx in dedupe(final))


def unpack_end_context(key: tuple) -> dict:
    return {
        "hand": key[0], "draw": key[1], "discard": key[2], "energy": key[3], "block": key[4],
        "enemy": key[5], "armor": key[6], "vuln": key[7], "weak": key[8], "potion_used": key[9],
        "sly": key[10], "first_discard": key[11], "sharp_used": key[12], "relic": key[13],
    }


@lru_cache(maxsize=None)
def solve(
    turn: int,
    hand: tuple[str, ...],
    draw: tuple[str, ...],
    discard: tuple[str, ...],
    hp: int,
    enemy: int,
    armor: int,
    vuln: int,
    weak: int,
    potion_used: bool,
    potion: str,
    relic: str,
) -> tuple[float, ...]:
    best = None
    endpoints = turn_end_contexts(turn, hand, draw, discard, hp, enemy, armor, vuln, weak, potion_used, potion, relic)
    for key in endpoints:
        ctx = unpack_end_context(key)
        if ctx["enemy"] <= 0:
            candidate = kill_vec(turn)
        elif turn >= 4:
            candidate = FAIL_VEC
        else:
            incoming = 0
            armor_gain = 0
            if turn == 1:
                incoming = weak_damage(8, ctx["weak"])
                if ctx["sly"] == 0:
                    armor_gain = 10
            elif turn == 2:
                incoming = weak_damage(14, ctx["weak"])
                if ctx["sly"] == 0:
                    armor_gain = 14
            elif turn == 3:
                incoming = weak_damage(22, ctx["weak"])
            hp2 = hp - max(0, incoming - ctx["block"])
            if hp2 <= 0:
                candidate = FAIL_VEC
            else:
                discard2 = sort_cards(ctx["discard"] + ctx["hand"])
                weighted = []
                for hand2, draw2, discard3, prob in next_draws(ctx["draw"], discard2, 5):
                    future = solve(
                        turn + 1, hand2, draw2, discard3, hp2, ctx["enemy"], ctx["armor"] + armor_gain,
                        max(0, ctx["vuln"] - 1), max(0, ctx["weak"] - 1), ctx["potion_used"], potion, relic,
                    )
                    weighted.append((prob, future))
                candidate = merge(weighted)
        if better(candidate, best):
            best = candidate
    return best or FAIL_VEC


def result_for(deck: tuple[str, ...], potion: str, relic: str, enemy_hp: int) -> tuple[float, ...]:
    weighted = []
    for hand, draw, prob in draw_outcomes(deck, 5):
        future = solve(1, hand, draw, (), 22, enemy_hp, 0, 0, 0, False, potion, relic)
        weighted.append((prob, future))
    return merge(weighted)


def first_turn(vec: tuple[float, ...]) -> int | None:
    for idx, value in enumerate(vec[:-1], 1):
        if value > 1e-9:
            return idx
    return None


def classify(deck: tuple[str, ...], potion: str, relic: str) -> str:
    c = Counter(deck)
    if c["Backstab"] >= 2 and potion == "SlyBrew" and relic == "SharpDice":
        return "药水骰子狡黠快线"
    if c["Backstab"] >= 2 and relic == "SharpDice":
        return "锋利骰子狡黠爆发线"
    if c["Finisher"] and (c["Prepared"] or c["Survivor"]):
        return "狡黠终结线"
    if c["Survivor"] and relic == "HollowAmulet":
        return "弃牌格挡稳线"
    if c["Bash"] and potion == "Vulnerable":
        return "双易伤攻击线"
    if potion == "Fire":
        return "火焰补刀线"
    return "混合线"


def pct(x: float) -> float:
    return round(x * 100, 4)


def run(enemy_hp: int = 78) -> dict:
    rows = []
    decks = unique_decks()
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
        "cache": {
            "solve": solve.cache_info()._asdict(),
            "turn_end_contexts": turn_end_contexts.cache_info()._asdict(),
        },
    }


def main() -> None:
    summary = run()
    with open("difficulty3_sly_exact_audit.json", "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)
    print("enemy_hp", summary["enemy_hp"])
    print("legal_deck_count", summary["legal_deck_count"])
    print("legal_build_count", summary["legal_build_count"])
    print("perfect_success_count", summary["perfect_success_count"])
    print("top30")
    for row in summary["top30"]:
        print(row["success"], "first", row["first_turn"], row["family"], row["kill_vector"], row["build_display"])
    print("best_by_turn")
    for turn, vals in sorted(summary["best_by_turn"].items(), key=lambda item: 99 if item[0] == "None" else int(item[0])):
        row = vals[0]
        print(turn, row["success"], row["family"], row["kill_vector"], row["build_display"])
    print("cache", summary["cache"])


if __name__ == "__main__":
    main()
