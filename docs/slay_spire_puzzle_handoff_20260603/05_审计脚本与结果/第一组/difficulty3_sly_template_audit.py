from __future__ import annotations

from collections import Counter, defaultdict
from functools import lru_cache
from itertools import combinations
from math import comb
import json
import os


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

ORDER = ("Bash", "Survivor", "Prepared", "Finisher", "Backstab", "DaggerThrow", "QuickSlash", "Neutralize", "Strike", "Defend")
CN = {
    "Strike": "打击", "Defend": "防御", "Bash": "重击", "Neutralize": "中和",
    "QuickSlash": "快斩（改）", "DaggerThrow": "投掷匕首（改）", "Survivor": "生存者",
    "Prepared": "预备（改）", "Backstab": "背刺（改，狡黠）", "Finisher": "终结（改）",
}
COST = {"Strike": 1, "Defend": 1, "Bash": 2, "Neutralize": 0, "QuickSlash": 1, "DaggerThrow": 1, "Survivor": 1, "Prepared": 0, "Backstab": 1, "Finisher": 1}
POTION_CN = {"SlyBrew": "狡黠药水", "Vulnerable": "破甲药水", "Fire": "火焰药水"}
RELIC_CN = {"SharpDice": "锋利骰子", "ReturnHolster": "折返皮套", "HollowAmulet": "空心护符"}


def sort_cards(cards):
    return tuple(sorted(cards, key=lambda c: (ORDER.index(c), c)))


def remove_one(cards, card):
    out = list(cards)
    out.remove(card)
    return sort_cards(out)


def display_deck(deck):
    c = Counter(deck)
    return "、".join(f"{CN[k]} x{c[k]}" if c[k] > 1 else CN[k] for k in ORDER if c[k])


def attack(base, vuln):
    return (base * 3) // 2 if vuln > 0 else base


def weak_damage(base, weak):
    return (base * 3) // 4 if weak > 0 else base


def deal(hp, armor, amount):
    blocked = min(armor, amount)
    return hp - (amount - blocked), armor - blocked


@lru_cache(maxsize=None)
def draw_outcomes(cards, n):
    cards = sort_cards(cards)
    if n >= len(cards):
        return [(cards, (), 1.0)]
    total = comb(len(cards), n)
    merged = defaultdict(float)
    for pick in combinations(range(len(cards)), n):
        s = set(pick)
        hand = sort_cards(cards[i] for i in pick)
        rest = sort_cards(cards[i] for i in range(len(cards)) if i not in s)
        merged[(hand, rest)] += 1 / total
    return [(h, r, p) for (h, r), p in merged.items()]


@lru_cache(maxsize=None)
def next_draws(draw, discard, n):
    draw = sort_cards(draw)
    discard = sort_cards(discard)
    if len(draw) >= n:
        return [(h, r, discard, p) for h, r, p in draw_outcomes(draw, n)]
    fixed = draw
    need = n - len(draw)
    if not discard:
        return [(fixed, (), (), 1.0)]
    return [(sort_cards(fixed + h), r, (), p) for h, r, p in draw_outcomes(discard, need)]


def discard_card(ctx, card):
    ctx["hand"] = remove_one(ctx["hand"], card)
    if not ctx["discarded"]:
        ctx["discarded"] = True
        if ctx["relic"] == "ReturnHolster":
            ctx["energy"] += 1
        elif ctx["relic"] == "HollowAmulet":
            ctx["block"] += 6
    if card == "Backstab":
        dmg = 12 + (6 if ctx["relic"] == "SharpDice" and not ctx["sharp"] else 0)
        ctx["sharp"] = ctx["sharp"] or ctx["relic"] == "SharpDice"
        ctx["sly"] += 1
        ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], attack(dmg, ctx["vuln"]))
    ctx["discard"].append(card)


def play_card(ctx, card):
    if card not in ctx["hand"] or ctx["energy"] < COST[card]:
        return False
    ctx["hand"] = remove_one(ctx["hand"], card)
    ctx["energy"] -= COST[card]
    if card == "Strike":
        ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], attack(6, ctx["vuln"]))
    elif card == "Defend":
        ctx["block"] += 5
    elif card == "Bash":
        ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], attack(8, ctx["vuln"]))
        ctx["vuln"] += 2
    elif card == "Neutralize":
        ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], attack(3, ctx["vuln"]))
        ctx["weak"] += 1
    elif card == "QuickSlash":
        ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], attack(8, ctx["vuln"]))
    elif card == "DaggerThrow":
        ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], attack(9, ctx["vuln"]))
    elif card == "Backstab":
        ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], attack(6, ctx["vuln"]))
    elif card == "Finisher":
        ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], attack(7 + 6 * ctx["sly"], ctx["vuln"]))
    ctx["discard"].append(card)
    return True


def choose_discard(ctx, plan):
    prefs = {
        "fast": ("Backstab", "Strike", "Defend", "QuickSlash"),
        "main": ("Backstab", "Finisher", "Strike", "Defend"),
        "stable": ("Backstab", "Strike", "QuickSlash", "Defend"),
        "attack": ("Defend", "Strike", "Backstab"),
        "slow": ("Strike", "QuickSlash", "Defend", "Backstab"),
    }[plan]
    for card in prefs:
        if card in ctx["hand"]:
            return card
    return ctx["hand"][0] if ctx["hand"] else None


def run_turn(state, potion, relic, plan):
    turn, hand, draw, discard, hp, enemy, armor, vuln, weak, potion_used = state
    ctx = {
        "hand": hand, "draw": draw, "discard": list(discard), "energy": 3, "block": 0,
        "enemy": enemy, "armor": armor, "vuln": vuln, "weak": weak, "potion_used": potion_used,
        "sly": 0, "discarded": False, "sharp": False, "relic": relic,
    }
    if not potion_used:
        if potion == "Vulnerable" and plan in {"main", "attack"}:
            ctx["vuln"] += 2; ctx["potion_used"] = True
        elif potion == "SlyBrew" and plan in {"fast", "main"}:
            target = choose_discard(ctx, plan)
            if target:
                ctx["energy"] += 1; ctx["potion_used"] = True; discard_card(ctx, target)
        elif potion == "Fire" and (turn >= 3 or ctx["enemy"] <= 40):
            ctx["enemy"], ctx["armor"] = deal(ctx["enemy"], ctx["armor"], 18); ctx["potion_used"] = True

    if plan in {"main", "attack"}:
        play_card(ctx, "Bash")

    engines = ("Prepared", "Survivor") if plan in {"fast", "main"} else ("Survivor", "Prepared")
    for engine in engines:
        if engine in ctx["hand"] and ctx["energy"] >= COST[engine] and len(ctx["hand"]) > 1:
            if play_card(ctx, engine):
                target = choose_discard(ctx, plan)
                if target:
                    discard_card(ctx, target)

    priorities = {
        "fast": ("Finisher", "Backstab", "DaggerThrow", "QuickSlash", "Neutralize", "Strike", "Defend", "Bash"),
        "main": ("Finisher", "Backstab", "DaggerThrow", "QuickSlash", "Neutralize", "Strike", "Defend"),
        "stable": ("Finisher", "Backstab", "Neutralize", "DaggerThrow", "QuickSlash", "Strike", "Defend", "Bash"),
        "attack": ("Backstab", "DaggerThrow", "QuickSlash", "Strike", "Finisher", "Neutralize", "Defend"),
        "slow": ("Defend", "Neutralize", "Finisher", "Backstab", "DaggerThrow", "QuickSlash", "Strike", "Bash"),
    }[plan]
    changed = True
    while changed:
        changed = False
        for card in priorities:
            if play_card(ctx, card):
                changed = True
                if ctx["enemy"] <= 0:
                    return [(1.0, ("kill", turn))]

    if ctx["enemy"] <= 0:
        return [(1.0, ("kill", turn))]
    if turn >= 4:
        return [(1.0, ("fail",))]

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
        return [(1.0, ("fail",))]
    discard2 = sort_cards(tuple(ctx["discard"]) + ctx["hand"])
    out = []
    for hand2, draw2, discard3, p in next_draws(ctx["draw"], discard2, 5):
        out.append((p, (turn + 1, hand2, draw2, discard3, hp2, ctx["enemy"], ctx["armor"] + armor_gain, max(0, ctx["vuln"] - 1), max(0, ctx["weak"] - 1), ctx["potion_used"])))
    return out


def simulate(deck, potion, relic, plan, enemy_hp):
    vec = [0.0] * 5
    for hand, draw, p in draw_outcomes(deck, 5):
        state = (1, hand, draw, (), 22, enemy_hp, 0, 0, 0, False)
        sub = eval_state(state, potion, relic, plan)
        for i, value in enumerate(sub):
            vec[i] += p * value
    return tuple(vec)


@lru_cache(maxsize=None)
def eval_state(state, potion, relic, plan):
    vec = [0.0] * 5
    for prob, nxt in run_turn(state, potion, relic, plan):
        if nxt[0] == "kill":
            vec[nxt[1] - 1] += prob
        elif nxt[0] == "fail":
            vec[-1] += prob
        else:
            sub = eval_state(nxt, potion, relic, plan)
            for i, value in enumerate(sub):
                vec[i] += prob * value
    return tuple(vec)


def unique_decks():
    decks = set()
    for pick in combinations(range(len(POOL)), 8):
        decks.add(sort_cards(POOL[i] for i in pick))
    return sorted(decks)


def first_turn(vec):
    for i, p in enumerate(vec[:-1], 1):
        if p > 1e-9:
            return i
    return None


def pct(x):
    return round(x * 100, 4)


def classify(deck, potion, relic, plan):
    c = Counter(deck)
    if plan == "fast" and potion == "SlyBrew" and c["Backstab"] >= 2:
        return "药水狡黠快线"
    if relic == "SharpDice" and c["Backstab"] >= 2:
        return "锋利骰子狡黠爆发线"
    if c["Finisher"] and (c["Prepared"] or c["Survivor"]):
        return "狡黠终结线"
    if plan == "stable":
        return "格挡稳线"
    if potion == "Vulnerable":
        return "易伤攻击线"
    return "混合线"


def main():
    enemy_hp = 78
    rows = []
    plans = ("fast", "main", "stable")
    decks = unique_decks()
    max_builds = int(os.environ.get("D3_MAX_BUILDS", "0") or "0")
    total = len(decks) * 9 * len(plans)
    done = 0
    build_done = 0
    for deck in decks:
        for potion in POTION_CN:
            for relic in RELIC_CN:
                build_done += 1
                best = None
                best_plan = None
                best_vec = None
                for plan in plans:
                    done += 1
                    vec = simulate(deck, potion, relic, plan, enemy_hp)
                    score = (sum(vec[:-1]), vec[0], vec[1], vec[2], vec[3])
                    if best is None or score > best:
                        best = score; best_plan = plan; best_vec = vec
                    if done % 1000 == 0:
                        print(f"audited {done}/{total}", flush=True)
                rows.append({
                    "deck": list(deck),
                    "potion": potion,
                    "relic": relic,
                    "plan": best_plan,
                    "build_display": f"{display_deck(deck)}；{POTION_CN[potion]}；{RELIC_CN[relic]}",
                    "family": classify(deck, potion, relic, best_plan),
                    "first_turn": first_turn(best_vec),
                    "kill_vector": [pct(x) for x in best_vec],
                    "success": pct(sum(best_vec[:-1])),
                    "fail": pct(best_vec[-1]),
                })
                if build_done % 250 == 0:
                    with open("difficulty3_sly_template_audit.partial.json", "w", encoding="utf-8") as f:
                        json.dump({"enemy_hp": enemy_hp, "completed_builds": build_done, "rows": rows}, f, ensure_ascii=False, indent=2)
                if max_builds and build_done >= max_builds:
                    break
            if max_builds and build_done >= max_builds:
                break
        if max_builds and build_done >= max_builds:
            break
    rows.sort(key=lambda r: (r["success"], -(r["first_turn"] or 99), r["kill_vector"][0], r["kill_vector"][1], r["kill_vector"][2]), reverse=True)
    by_turn = defaultdict(list)
    by_family = defaultdict(list)
    for row in rows:
        by_turn[str(row["first_turn"])].append(row)
        by_family[row["family"]].append(row)
    summary = {
        "enemy_hp": enemy_hp,
        "legal_deck_count": len(decks),
        "legal_build_count": len(rows),
        "strategy_templates": list(plans),
        "perfect_success_count": sum(1 for r in rows if r["success"] >= 99.9999),
        "top30": rows[:30],
        "best_by_turn": {k: v[:10] for k, v in by_turn.items()},
        "best_by_family": {k: v[:10] for k, v in by_family.items()},
    }
    with open("difficulty3_sly_template_audit.json", "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)
    print("legal_deck_count", summary["legal_deck_count"])
    print("legal_build_count", summary["legal_build_count"])
    print("perfect_success_count", summary["perfect_success_count"])
    print("top30")
    for row in summary["top30"]:
        print(row["success"], "first", row["first_turn"], row["plan"], row["family"], row["kill_vector"], row["build_display"])
    print("best_by_turn")
    for turn, vals in sorted(summary["best_by_turn"].items(), key=lambda item: 99 if item[0] == "None" else int(item[0])):
        r = vals[0]
        print(turn, r["success"], r["plan"], r["family"], r["kill_vector"], r["build_display"])


if __name__ == "__main__":
    main()
