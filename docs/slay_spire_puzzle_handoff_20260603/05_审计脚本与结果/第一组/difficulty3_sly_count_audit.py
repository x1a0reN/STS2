from __future__ import annotations

from collections import defaultdict
from functools import lru_cache
from math import comb
import json
import os


# 0 Bash, 1 Prepared, 2 Survivor, 3 Finisher, 4 Backstab,
# 5 DaggerThrow, 6 QuickSlash, 7 Neutralize, 8 Strike, 9 Defend
N = 10
BASH, PREPARED, SURVIVOR, FINISHER, BACKSTAB, DAGGER, QUICK, NEUTRALIZE, STRIKE, DEFEND = range(N)

POOL = (1, 1, 1, 1, 2, 1, 2, 1, 3, 2)
COST = (2, 0, 1, 1, 1, 1, 1, 0, 1, 1)
CN = ("重击", "预备（改）", "生存者", "终结（改）", "背刺（改，狡黠）", "投掷匕首（改）", "快斩（改）", "中和", "打击", "防御")
POTION_CN = ("狡黠药水", "破甲药水", "火焰药水")
RELIC_CN = ("锋利骰子", "折返皮套", "空心护符")
SLY_BREW, VULN_POTION, FIRE_POTION = range(3)
SHARP_DICE, RETURN_HOLSTER, HOLLOW_AMULET = range(3)


def add(a, b):
    return tuple(x + y for x, y in zip(a, b))


def sub_one(counts, card):
    lst = list(counts)
    lst[card] -= 1
    return tuple(lst)


def add_one(counts, card):
    lst = list(counts)
    lst[card] += 1
    return tuple(lst)


def total(counts):
    return sum(counts)


def attack_damage(base, vuln):
    return (base * 3) // 2 if vuln > 0 else base


def weak_damage(base, weak):
    return (base * 3) // 4 if weak > 0 else base


def deal(enemy, armor, amount):
    blocked = min(armor, amount)
    return enemy - (amount - blocked), armor - blocked


def kill_vec(turn):
    out = [0.0] * 5
    out[turn - 1] = 1.0
    return tuple(out)


FAIL = (0.0, 0.0, 0.0, 0.0, 1.0)

UB_ACTIVE_DAMAGE = (12, 0, 0, 18, 9, 14, 12, 4, 9, 0)


def better(a, b):
    if b is None:
        return True
    return (sum(a[:-1]), a[0], a[1], a[2], a[3]) > (sum(b[:-1]), b[0], b[1], b[2], b[3])


def merge(items):
    out = [0.0] * 5
    for w, vec in items:
        for i, v in enumerate(vec):
            out[i] += w * v
    return tuple(out)


def loose_damage_upper_bound(turn, hand, draw, discard, armor, potion_used, potion, relic):
    cards = add(add(hand, draw), discard)
    turns_left = 5 - turn
    if turns_left <= 0:
        return 0
    active_pool = []
    for card, count in enumerate(cards):
        active_pool.extend([UB_ACTIVE_DAMAGE[card]] * count)
    active_pool.sort(reverse=True)
    active = sum(active_pool[: max(0, 2 * turns_left)])
    sly = 0
    if cards[BACKSTAB] > 0:
        per = 20 if relic == SHARP_DICE else 16
        sly = min(cards[BACKSTAB], turns_left) * per
    potion_bonus = 18 if (not potion_used and potion == FIRE_POTION) else 0
    # This is intentionally generous: assumes all relevant hits are vulnerable and ignores energy constraints.
    return active + sly + potion_bonus - armor


@lru_cache(maxsize=None)
def draw_count_outcomes(cards, n):
    m = total(cards)
    if n >= m:
        return ((cards, (0,) * N, 1.0),)
    denom = comb(m, n)
    out = []
    pick = [0] * N

    def rec(i, left, ways):
        if i == N:
            if left == 0:
                hand = tuple(pick)
                rest = tuple(cards[j] - hand[j] for j in range(N))
                out.append((hand, rest, ways / denom))
            return
        max_take = min(cards[i], left)
        for k in range(max_take + 1):
            pick[i] = k
            rec(i + 1, left - k, ways * comb(cards[i], k))
        pick[i] = 0

    rec(0, n, 1)
    return tuple(out)


@lru_cache(maxsize=None)
def next_draws(draw, discard, n):
    if total(draw) >= n:
        return tuple((hand, rest, discard, p) for hand, rest, p in draw_count_outcomes(draw, n))
    fixed = draw
    need = n - total(draw)
    if total(discard) == 0:
        return ((fixed, (0,) * N, (0,) * N, 1.0),)
    return tuple((add(fixed, hand), rest, (0,) * N, p) for hand, rest, p in draw_count_outcomes(discard, need))


def deck_choices():
    out = []
    cur = [0] * N

    def rec(i, left):
        if i == N:
            if left == 0:
                out.append(tuple(cur))
            return
        for k in range(min(POOL[i], left) + 1):
            cur[i] = k
            rec(i + 1, left - k)
        cur[i] = 0

    rec(0, 8)
    return out


def display_deck(deck):
    parts = []
    for i, n in enumerate(deck):
        if n == 1:
            parts.append(CN[i])
        elif n > 1:
            parts.append(f"{CN[i]} x{n}")
    return "、".join(parts)


def discard_card(ctx, card):
    hand, draw, discard, energy, block, enemy, armor, vuln, weak, potion_used, sly, first_discard, sharp_used, played = ctx
    hand = sub_one(hand, card)
    if not first_discard:
        first_discard = 1
        if ctx_relic[0] == RETURN_HOLSTER:
            energy += 1
        elif ctx_relic[0] == HOLLOW_AMULET:
            block += 6
    if card == BACKSTAB:
        dmg = 10
        if ctx_relic[0] == SHARP_DICE and not sharp_used:
            dmg += 3
            sharp_used = 1
        sly += 1
        enemy, armor = deal(enemy, armor, attack_damage(dmg, vuln))
    discard = add_one(discard, card)
    return (hand, draw, discard, energy, block, enemy, armor, vuln, weak, potion_used, sly, first_discard, sharp_used, played)


# Mutable single-item holder to avoid carrying relic through every inner helper argument.
ctx_relic = [0]


def play_bash(ctx):
    hand, draw, discard, energy, block, enemy, armor, vuln, weak, potion_used, sly, first_discard, sharp_used, played = ctx
    if hand[BASH] <= 0 or energy < 2 or played >= 2:
        return None
    hand = sub_one(hand, BASH)
    energy -= 2
    enemy, armor = deal(enemy, armor, attack_damage(8, vuln))
    vuln += 2
    discard = add_one(discard, BASH)
    return (hand, draw, discard, energy, block, enemy, armor, vuln, weak, potion_used, sly, first_discard, sharp_used, played + 1)


def play_engine(ctx, engine):
    hand, draw, discard, energy, block, enemy, armor, vuln, weak, potion_used, sly, first_discard, sharp_used, played = ctx
    if first_discard or hand[engine] <= 0 or energy < COST[engine] or total(hand) <= 1 or played >= 2:
        return []
    base_hand = sub_one(hand, engine)
    base_energy = energy - COST[engine]
    base_block = block + (8 if engine == SURVIVOR else 0)
    base = (base_hand, draw, discard, base_energy, base_block, enemy, armor, vuln, weak, potion_used, sly, first_discard, sharp_used, played + 1)
    out = []
    for card, n in enumerate(base_hand):
        if n > 0:
            nxt = discard_card(base, card)
            h, d, disc, e, b, en, ar, vu, we, pu, sl, fd, sh, pl = nxt
            out.append((h, d, add_one(disc, engine), e, b, en, ar, vu, we, pu, sl, fd, sh, pl))
    return out


@lru_cache(maxsize=None)
def normal_subsets(hand, energy, allow_bash, max_cards):
    banned = {PREPARED, SURVIVOR}
    if not allow_bash:
        banned.add(BASH)
    cur = [0] * N
    out = []

    def rec(i, spent):
        if i == N:
            subset = tuple(cur)
            if sum(subset) > max_cards:
                return
            # Keep only energy-maximal subsets.
            for c in range(N):
                if c not in banned and cur[c] < hand[c] and spent + COST[c] <= energy and sum(subset) < max_cards:
                    return
            out.append(subset)
            return
        if i in banned:
            cur[i] = 0
            rec(i + 1, spent)
            return
        for k in range(hand[i] + 1):
            cost = spent + k * COST[i]
            if cost <= energy:
                cur[i] = k
                rec(i + 1, cost)
        cur[i] = 0

    rec(0, 0)
    return tuple(out)


def play_subset(ctx, subset):
    hand, draw, discard, energy, block, enemy, armor, vuln, weak, potion_used, sly, first_discard, sharp_used, played = ctx
    if played + sum(subset) > 2:
        return None
    if any(subset[i] > hand[i] for i in range(N)):
        return None
    spent = sum(subset[i] * COST[i] for i in range(N))
    if spent > energy:
        return None
    for card, n in enumerate(subset):
        for _ in range(n):
            hand = sub_one(hand, card)
            energy -= COST[card]
            if card == STRIKE:
                enemy, armor = deal(enemy, armor, attack_damage(6, vuln))
            elif card == DEFEND:
                block += 5
            elif card == BASH:
                enemy, armor = deal(enemy, armor, attack_damage(8, vuln))
                vuln += 2
            elif card == NEUTRALIZE:
                enemy, armor = deal(enemy, armor, attack_damage(3, vuln))
                weak += 1
            elif card == QUICK:
                enemy, armor = deal(enemy, armor, attack_damage(8, vuln))
            elif card == DAGGER:
                enemy, armor = deal(enemy, armor, attack_damage(9, vuln))
            elif card == BACKSTAB:
                enemy, armor = deal(enemy, armor, attack_damage(6, vuln))
            elif card == FINISHER:
                enemy, armor = deal(enemy, armor, attack_damage(7 + 5 * sly, vuln))
            else:
                return None
            discard = add_one(discard, card)
    return (hand, draw, discard, energy, block, enemy, armor, vuln, weak, potion_used, sly, first_discard, sharp_used, played + sum(subset))


def potion_contexts(base, potion):
    hand, draw, discard, energy, block, enemy, armor, vuln, weak, potion_used, sly, first_discard, sharp_used, played = base
    out = [base]
    if potion_used:
        return out
    if potion == FIRE_POTION:
        en, ar = deal(enemy, armor, 18)
        out.append((hand, draw, discard, energy, block, en, ar, vuln, weak, 1, sly, first_discard, sharp_used, played))
    elif potion == VULN_POTION:
        out.append((hand, draw, discard, energy, block, enemy, armor, vuln + 2, weak, 1, sly, first_discard, sharp_used, played))
    elif potion == SLY_BREW and total(hand) > 0:
        brew = (hand, draw, discard, energy, block, enemy, armor, vuln, weak, 1, sly, first_discard, sharp_used, played)
        for card, n in enumerate(hand):
            if n > 0:
                out.append(discard_card(brew, card))
    return out


def dedupe(contexts):
    return list(dict.fromkeys(contexts))


@lru_cache(maxsize=None)
def end_contexts(turn, hand, draw, discard, hp, enemy, armor, vuln, weak, potion_used, potion, relic):
    ctx_relic[0] = relic
    base = (hand, draw, discard, 3, 0, enemy, armor, vuln, weak, int(potion_used), 0, 0, 0, 0)
    contexts = potion_contexts(base, potion)
    endings = []
    engine_orders = ((), (PREPARED,), (SURVIVOR,))
    for ctx0 in contexts:
        pre_options = [ctx0]
        pre = play_bash(ctx0)
        if pre is not None:
            pre_options.append(pre)
        for ctx1 in pre_options:
            for order in engine_orders:
                states = [ctx1]
                valid = True
                for engine in order:
                    nxt_states = []
                    for st in states:
                        nxt_states.extend(play_engine(st, engine))
                    if not nxt_states:
                        valid = False
                        break
                    states = nxt_states
                if not valid:
                    continue
                for ctx2 in states:
                    post_options = [ctx2]
                    if ctx2[0][BASH] > 0:
                        post = play_bash(ctx2)
                        if post is not None:
                            post_options.append(post)
                    for ctx3 in post_options:
                        allow_bash = ctx3[0][BASH] > 0
                        max_cards = 2 - ctx3[13]
                        for subset in normal_subsets(ctx3[0], ctx3[3], allow_bash, max_cards):
                            end = play_subset(ctx3, subset)
                            if end is not None:
                                endings.append(end)
    return tuple(dedupe(endings))


@lru_cache(maxsize=None)
def solve(turn, hand, draw, discard, hp, enemy, armor, vuln, weak, potion_used, potion, relic):
    ctx_relic[0] = relic
    if enemy > loose_damage_upper_bound(turn, hand, draw, discard, armor, potion_used, potion, relic):
        return FAIL
    best = None
    for ctx in end_contexts(turn, hand, draw, discard, hp, enemy, armor, vuln, weak, potion_used, potion, relic):
        h, dr, disc, energy, block, en, ar, vu, we, pu, sly, first_discard, sharp_used, played = ctx
        if en <= 0:
            candidate = kill_vec(turn)
        elif turn >= 4:
            candidate = FAIL
        else:
            incoming = 0
            armor_gain = 0
            if turn == 1:
                incoming = weak_damage(8, we)
                if sly == 0:
                    armor_gain = 10
            elif turn == 2:
                incoming = weak_damage(14, we)
                if sly == 0:
                    armor_gain = 14
            elif turn == 3:
                incoming = weak_damage(22, we)
            hp2 = hp - max(0, incoming - block)
            if hp2 <= 0:
                candidate = FAIL
            else:
                discard2 = add(disc, h)
                weighted = []
                for hand2, draw2, discard3, prob in next_draws(dr, discard2, 5):
                    future = solve(turn + 1, hand2, draw2, discard3, hp2, en, ar + armor_gain, max(0, vu - 1), max(0, we - 1), pu, potion, relic)
                    weighted.append((prob, future))
                candidate = merge(weighted)
        if better(candidate, best):
            best = candidate
    return best or FAIL


def result_for(deck, potion, relic, enemy_hp):
    ctx_relic[0] = relic
    weighted = []
    for hand, draw, prob in draw_count_outcomes(deck, 5):
        weighted.append((prob, solve(1, hand, draw, (0,) * N, 22, enemy_hp, 0, 0, 0, 0, potion, relic)))
    return merge(weighted)


def first_turn(vec):
    for i, p in enumerate(vec[:-1], 1):
        if p > 1e-9:
            return i
    return None


def classify(deck, potion, relic):
    if deck[BACKSTAB] >= 2 and potion == SLY_BREW and relic == SHARP_DICE:
        return "药水骰子狡黠快线"
    if deck[BACKSTAB] >= 2 and relic == SHARP_DICE:
        return "锋利骰子狡黠爆发线"
    if deck[FINISHER] and (deck[PREPARED] or deck[SURVIVOR]):
        return "狡黠终结线"
    if deck[SURVIVOR] and relic == HOLLOW_AMULET:
        return "弃牌格挡稳线"
    if deck[BASH] and potion == VULN_POTION:
        return "双易伤攻击线"
    if potion == FIRE_POTION:
        return "火焰补刀线"
    return "混合线"


def pct(x):
    return round(x * 100, 4)


def run(enemy_hp=78):
    decks = deck_choices()
    rows = []
    total_builds = len(decks) * 9
    max_builds = int(os.environ.get("D3_MAX_BUILDS", "0") or "0")
    done = 0
    for deck in decks:
        for potion in range(3):
            for relic in range(3):
                done += 1
                vec = result_for(deck, potion, relic, enemy_hp)
                row = {
                    "deck": list(deck),
                    "potion": potion,
                    "relic": relic,
                    "build_display": f"{display_deck(deck)}；{POTION_CN[potion]}；{RELIC_CN[relic]}",
                    "family": classify(deck, potion, relic),
                    "first_turn": first_turn(vec),
                    "kill_vector": [pct(x) for x in vec],
                    "success": pct(sum(vec[:-1])),
                    "fail": pct(vec[-1]),
                }
                rows.append(row)
                if done % 10 == 0 or done == total_builds:
                    print(f"audited {done}/{total_builds}", flush=True)
                if max_builds and done >= max_builds:
                    break
            if max_builds and done >= max_builds:
                break
        if max_builds and done >= max_builds:
            break
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
            "end_contexts": end_contexts.cache_info()._asdict(),
        },
    }


def main():
    summary = run()
    with open("difficulty3_sly_count_audit.json", "w", encoding="utf-8") as f:
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
