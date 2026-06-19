from __future__ import annotations

from collections import Counter, defaultdict
from dataclasses import dataclass
from functools import lru_cache
from itertools import combinations
from math import comb
import json


POOL = (
    "Strike",
    "Strike",
    "Strike",
    "Defend",
    "Defend",
    "Bash",
    "Neutralize",
    "QuickSlash",
    "QuickSlash",
    "Survivor",
    "Prepared",
    "HiddenDaggers",
    "Backstab",
    "Backstab",
    "ShadowStep",
    "MementoMori",
    "ColdSnap",
)

CARD_CN = {
    "Strike": "打击",
    "Defend": "防御",
    "Bash": "重击",
    "Neutralize": "中和",
    "QuickSlash": "快斩（改）",
    "Survivor": "生存者",
    "Prepared": "预备（改）",
    "HiddenDaggers": "藏匕（改）",
    "Backstab": "背刺（改，狡黠）",
    "ShadowStep": "影步（改，狡黠）",
    "MementoMori": "死亡警句（改）",
    "ColdSnap": "寒流（改）",
    "Shiv": "小刀",
}

CARD_ORDER = (
    "Bash",
    "HiddenDaggers",
    "Prepared",
    "Survivor",
    "MementoMori",
    "Backstab",
    "ShadowStep",
    "Neutralize",
    "QuickSlash",
    "ColdSnap",
    "Strike",
    "Shiv",
    "Defend",
)

CARD_COST = {
    "Strike": 1,
    "Defend": 1,
    "Bash": 2,
    "Neutralize": 0,
    "QuickSlash": 1,
    "Survivor": 1,
    "Prepared": 0,
    "HiddenDaggers": 1,
    "Backstab": 1,
    "ShadowStep": 0,
    "MementoMori": 1,
    "ColdSnap": 1,
    "Shiv": 0,
}

ATTACKS = {
    "Strike",
    "Bash",
    "Neutralize",
    "QuickSlash",
    "Backstab",
    "MementoMori",
    "ColdSnap",
    "Shiv",
}

SLY = {"Backstab", "ShadowStep"}

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
    energy: int
    hp: int
    block: int
    enemy_hp: int
    enemy_armor: int
    vulnerable: int
    weak: int
    phase: int
    potion_used: bool
    discard_count: int
    sly_count: int
    attack_count: int
    first_discard_done: bool
    sharp_used: bool
    next_energy_bonus: int


def sort_cards(cards) -> tuple[str, ...]:
    return tuple(sorted(cards, key=lambda c: (CARD_ORDER.index(c), c)))


def set_state(state: State, **kwargs) -> State:
    data = state.__dict__.copy()
    data.update(kwargs)
    return State(**data)


def remove_one(cards: tuple[str, ...], card: str) -> tuple[str, ...]:
    out = list(cards)
    out.remove(card)
    return sort_cards(out)


def add_discard(discard: tuple[str, ...], card: str) -> tuple[str, ...]:
    if card == "Shiv":
        return discard
    return sort_cards(discard + (card,))


def attack_damage(base: int, vulnerable: int) -> int:
    return (base * 3) // 2 if vulnerable > 0 else base


def weak_damage(base: int, weak: int) -> int:
    return (base * 3) // 4 if weak > 0 else base


def deal_damage(enemy_hp: int, armor: int, amount: int) -> tuple[int, int]:
    absorbed = min(armor, amount)
    return enemy_hp - (amount - absorbed), armor - absorbed


def apply_attack(state: State, base: int) -> State:
    hp, armor = deal_damage(state.enemy_hp, state.enemy_armor, attack_damage(base, state.vulnerable))
    return set_state(state, enemy_hp=hp, enemy_armor=armor)


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


def merge(weighted: list[tuple[float, tuple[float, ...]]]) -> tuple[float, ...]:
    out = [0.0] * 5
    for weight, vec in weighted:
        for i, value in enumerate(vec):
            out[i] += weight * value
    return tuple(out)


@lru_cache(maxsize=None)
def draw_outcomes(cards: tuple[str, ...], count: int):
    cards = sort_cards(cards)
    if count <= 0:
        return (((), cards, 1.0),)
    if count >= len(cards):
        return ((cards, (), 1.0),)
    total = comb(len(cards), count)
    merged = defaultdict(float)
    for pick in combinations(range(len(cards)), count):
        pick_set = set(pick)
        hand = sort_cards(cards[i] for i in pick)
        rest = sort_cards(cards[i] for i in range(len(cards)) if i not in pick_set)
        merged[(hand, rest)] += 1 / total
    return tuple((hand, rest, prob) for (hand, rest), prob in merged.items())


@lru_cache(maxsize=None)
def next_turn_draws(draw: tuple[str, ...], discard: tuple[str, ...], count: int):
    draw = sort_cards(draw)
    discard = sort_cards(discard)
    if len(draw) >= count:
        return tuple((hand, next_draw, discard, prob) for hand, next_draw, prob in draw_outcomes(draw, count))
    fixed = draw
    need = count - len(draw)
    if not discard:
        return ((fixed, (), (), 1.0),)
    return tuple((sort_cards(fixed + hand), next_draw, (), prob) for hand, next_draw, prob in draw_outcomes(discard, need))


def unique_decks() -> list[tuple[str, ...]]:
    decks = set()
    for pick in combinations(range(len(POOL)), 8):
        decks.add(sort_cards(POOL[i] for i in pick))
    return sorted(decks)


def display_deck(deck: tuple[str, ...]) -> str:
    counts = Counter(deck)
    parts = []
    for card in CARD_ORDER:
        if card == "Shiv":
            continue
        n = counts[card]
        if n == 1:
            parts.append(CARD_CN[card])
        elif n > 1:
            parts.append(f"{CARD_CN[card]} x{n}")
    return "、".join(parts)


def display_build(deck: tuple[str, ...], potion: str, relic: str) -> str:
    return f"{display_deck(deck)}；{POTION_CN[potion]}；{RELIC_CN[relic]}"


def first_discard_relic(state: State, relic: str) -> State:
    if state.first_discard_done:
        return state
    state = set_state(state, first_discard_done=True)
    if relic == "ReturnHolster":
        return set_state(state, energy=state.energy + 1)
    if relic == "HollowAmulet":
        return set_state(state, block=state.block + 6)
    return state


def discard_one(state: State, card: str, relic: str) -> State:
    state = set_state(
        state,
        hand=remove_one(state.hand, card),
        discard_count=state.discard_count + 1,
    )
    if card == "Backstab":
        bonus = 0
        if relic == "SharpDice" and not state.sharp_used:
            bonus = 5
            state = set_state(state, sharp_used=True)
        state = set_state(state, sly_count=state.sly_count + 1, attack_count=state.attack_count + 1)
        state = apply_attack(state, 12 + bonus)
        state = set_state(state, discard=add_discard(state.discard, card))
    elif card == "ShadowStep":
        state = set_state(state, sly_count=state.sly_count + 1, block=state.block + 9, discard=add_discard(state.discard, card))
    else:
        state = set_state(state, discard=add_discard(state.discard, card))
    return first_discard_relic(state, relic)


def discard_options(state: State, count: int, relic: str):
    if count <= 0 or not state.hand:
        return (state,)
    out = []
    hand = list(state.hand)
    seen = set()
    max_count = min(count, len(hand))
    for size in range(1, max_count + 1):
        for pick in combinations(range(len(hand)), size):
            cards = sort_cards(hand[i] for i in pick)
            if cards in seen:
                continue
            seen.add(cards)
            cur = state
            ok = True
            for card in cards:
                if card not in cur.hand:
                    ok = False
                    break
                cur = discard_one(cur, card, relic)
            if ok:
                out.append(cur)
    return tuple(out)


def play_card(state: State, card: str, relic: str) -> tuple[State, ...]:
    if card not in state.hand or state.energy < CARD_COST[card]:
        return ()
    state = set_state(
        state,
        hand=remove_one(state.hand, card),
        energy=state.energy - CARD_COST[card],
        attack_count=state.attack_count + (1 if card in ATTACKS else 0),
        phase=CARD_ORDER.index(card) + 1,
    )

    def done(s: State) -> tuple[State, ...]:
        return (set_state(s, discard=add_discard(s.discard, card)),)

    if card == "Strike":
        return done(apply_attack(state, 6))
    if card == "Defend":
        return done(set_state(state, block=state.block + 5))
    if card == "Bash":
        s = apply_attack(state, 8)
        return done(set_state(s, vulnerable=s.vulnerable + 2))
    if card == "Neutralize":
        s = apply_attack(state, 3)
        return done(set_state(s, weak=s.weak + 1))
    if card == "QuickSlash":
        return done(apply_attack(state, 8))
    if card == "Survivor":
        s = set_state(state, block=state.block + 8)
        return tuple(set_state(x, discard=add_discard(x.discard, card)) for x in discard_options(s, 1, relic))
    if card == "Prepared":
        s = set_state(state, next_energy_bonus=state.next_energy_bonus + 1)
        return tuple(set_state(x, discard=add_discard(x.discard, card)) for x in discard_options(s, 1, relic))
    if card == "HiddenDaggers":
        out = []
        for s in discard_options(state, 2, relic):
            added = s.discard_count - state.discard_count
            out.append(set_state(s, hand=sort_cards(s.hand + ("Shiv",) * added), discard=add_discard(s.discard, card)))
        return tuple(out)
    if card == "Backstab":
        return done(apply_attack(state, 6))
    if card == "ShadowStep":
        return done(set_state(state, block=state.block + 3))
    if card == "MementoMori":
        return done(apply_attack(state, 7 + 4 * state.discard_count))
    if card == "ColdSnap":
        return done(set_state(apply_attack(state, 6), block=state.block + 4))
    if card == "Shiv":
        return (apply_attack(state, 4),)
    raise ValueError(card)


def end_turn(state: State, potion: str, relic: str) -> tuple[float, ...]:
    if state.enemy_hp <= 0:
        return kill_vec(state.turn)
    if state.turn >= 4:
        return fail_vec()

    incoming = 0
    armor_gain = 0
    if state.turn == 1:
        incoming = weak_damage(8, state.weak)
        if state.sly_count == 0:
            armor_gain = 8
    elif state.turn == 2:
        incoming = weak_damage(14, state.weak)
        if state.sly_count == 0:
            armor_gain = 12
    elif state.turn == 3:
        incoming = weak_damage(22, state.weak)

    hp_after = state.hp - max(0, incoming - state.block)
    if hp_after <= 0:
        return fail_vec()

    discard_after = sort_cards(c for c in state.discard + state.hand if c != "Shiv")
    weighted = []
    for hand, draw, discard, prob in next_turn_draws(state.draw, discard_after, 5):
        nxt = State(
            turn=state.turn + 1,
            hand=hand,
            draw=draw,
            discard=discard,
            energy=3 + state.next_energy_bonus,
            hp=hp_after,
            block=0,
            enemy_hp=state.enemy_hp,
            enemy_armor=state.enemy_armor + armor_gain,
            vulnerable=max(0, state.vulnerable - 1),
            weak=max(0, state.weak - 1),
            phase=0,
            potion_used=state.potion_used,
            discard_count=0,
            sly_count=0,
            attack_count=0,
            first_discard_done=False,
            sharp_used=False,
            next_energy_bonus=0,
        )
        weighted.append((prob, value(nxt, potion, relic)))
    return merge(weighted)


@lru_cache(maxsize=None)
def value(state: State, potion: str, relic: str) -> tuple[float, ...]:
    if state.enemy_hp <= 0:
        return kill_vec(state.turn)
    best = end_turn(state, potion, relic)

    if not state.potion_used and state.phase == 0:
        if potion == "Fire":
            hp, armor = deal_damage(state.enemy_hp, state.enemy_armor, 20)
            cand = value(set_state(state, enemy_hp=hp, enemy_armor=armor, potion_used=True), potion, relic)
            if better(cand, best):
                best = cand
        elif potion == "Vulnerable":
            cand = value(set_state(state, vulnerable=state.vulnerable + 2, potion_used=True), potion, relic)
            if better(cand, best):
                best = cand
        elif potion == "SlyBrew" and state.hand:
            weighted = []
            for s in discard_options(set_state(state, potion_used=True, energy=state.energy + 1), 1, relic):
                weighted.append((1.0, value(s, potion, relic)))
            cand = merge(weighted)
            if better(cand, best):
                best = cand

    for card in sorted(set(state.hand), key=lambda c: CARD_ORDER.index(c)):
        card_phase = CARD_ORDER.index(card)
        if card_phase < state.phase:
            continue
        if state.energy < CARD_COST[card]:
            continue
        weighted = [(1.0, value(s, potion, relic)) for s in play_card(state, card, relic)]
        if weighted:
            cand = merge(weighted)
            if better(cand, best):
                best = cand
    return best


def result_for(deck: tuple[str, ...], potion: str, relic: str) -> tuple[float, ...]:
    value.cache_clear()
    weighted = []
    for hand, draw, prob in draw_outcomes(deck, 5):
        state = State(
            turn=1,
            hand=hand,
            draw=draw,
            discard=(),
            energy=3,
            hp=22,
            block=0,
            enemy_hp=118,
            enemy_armor=0,
            vulnerable=0,
            weak=0,
            phase=0,
            potion_used=False,
            discard_count=0,
            sly_count=0,
            attack_count=0,
            first_discard_done=False,
            sharp_used=False,
            next_energy_bonus=0,
        )
        weighted.append((prob, value(state, potion, relic)))
    return merge(weighted)


def first_turn(vec: tuple[float, ...]) -> int | None:
    for i, p in enumerate(vec[:-1], 1):
        if p > 1e-9:
            return i
    return None


def family(deck: tuple[str, ...], potion: str, relic: str) -> str:
    c = Counter(deck)
    if c["Backstab"] >= 2 and potion == "SlyBrew":
        return "药水狡黠快线"
    if c["Backstab"] >= 2 and relic == "SharpDice":
        return "锋利骰子爆发线"
    if c["HiddenDaggers"] and c["MementoMori"]:
        return "弃牌计数线"
    if c["ShadowStep"] and relic == "HollowAmulet":
        return "狡黠格挡稳线"
    if c["Bash"] and potion == "Vulnerable":
        return "破甲易伤线"
    if potion == "Fire":
        return "火焰补刀线"
    return "混合线"


def pct(x: float) -> float:
    return round(x * 100, 4)


def run() -> dict:
    decks = unique_decks()
    rows = []
    total = len(decks) * len(POTION_CN) * len(RELIC_CN)
    done = 0
    for deck in decks:
        for potion in POTION_CN:
            for relic in RELIC_CN:
                done += 1
                vec = result_for(deck, potion, relic)
                row = {
                    "deck": list(deck),
                    "potion": potion,
                    "relic": relic,
                    "build_display": display_build(deck, potion, relic),
                    "family": family(deck, potion, relic),
                    "first_turn": first_turn(vec),
                    "kill_vector": [pct(x) for x in vec],
                    "success": pct(sum(vec[:-1])),
                    "fail": pct(vec[-1]),
                }
                rows.append(row)
                if done % 100 == 0 or done == total:
                    print(f"audited {done}/{total}", flush=True)
    rows.sort(key=lambda r: (r["success"], -(r["first_turn"] or 99), r["kill_vector"][0], r["kill_vector"][1], r["kill_vector"][2]), reverse=True)
    by_turn = defaultdict(list)
    by_family = defaultdict(list)
    for row in rows:
        by_turn[str(row["first_turn"])].append(row)
        by_family[row["family"]].append(row)
    return {
        "legal_deck_count": len(decks),
        "legal_build_count": len(rows),
        "perfect_success_count": sum(1 for r in rows if r["success"] >= 99.9999),
        "top30": rows[:30],
        "best_by_turn": {k: v[:10] for k, v in by_turn.items()},
        "best_by_family": {k: v[:10] for k, v in by_family.items()},
    }


def main() -> None:
    summary = run()
    with open("difficulty3_sly_v2_audit.json", "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=2)
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
