from __future__ import annotations

from collections import Counter, defaultdict
from dataclasses import dataclass
from functools import lru_cache
from itertools import combinations
from math import comb
import json


CARDS = (
    "Strike",
    "Strike",
    "Strike",
    "Defend",
    "Defend",
    "Bash",
    "Neutralize",
    "QuickSlash",
    "QuickSlash",
    "Acrobatics",
    "Survivor",
    "DaggerThrow",
    "Backstab",
    "Backstab",
    "ShadowStep",
    "Prepared",
    "BallLightning",
    "ColdSnap",
)

CARD_CN = {
    "Strike": "打击",
    "Defend": "防御",
    "Bash": "重击",
    "Neutralize": "中和",
    "QuickSlash": "快斩（改）",
    "Acrobatics": "杂技",
    "Survivor": "生存者",
    "DaggerThrow": "投掷匕首（改）",
    "Backstab": "背刺（改，狡黠）",
    "ShadowStep": "影步（狡黠）",
    "Prepared": "预备",
    "BallLightning": "球状闪电（改）",
    "ColdSnap": "寒流（改）",
}

CARD_ORDER = (
    "Bash",
    "Acrobatics",
    "Survivor",
    "Prepared",
    "Backstab",
    "ShadowStep",
    "DaggerThrow",
    "QuickSlash",
    "BallLightning",
    "ColdSnap",
    "Neutralize",
    "Strike",
    "Defend",
)

CARD_COST = {
    "Strike": 1,
    "Defend": 1,
    "Bash": 2,
    "Neutralize": 0,
    "QuickSlash": 1,
    "Acrobatics": 1,
    "Survivor": 1,
    "DaggerThrow": 1,
    "Backstab": 1,
    "ShadowStep": 0,
    "Prepared": 0,
    "BallLightning": 1,
    "ColdSnap": 1,
}

POTION_CN = {
    "Fire": "火焰药水",
    "Vulnerable": "破甲药水",
    "Gambler": "赌徒药水",
}

RELIC_CN = {
    "SharpDice": "锋利骰子",
    "ReturnHolster": "折返皮套",
    "HollowAmulet": "空心护符",
}

ATTACKS = {
    "Strike",
    "Bash",
    "Neutralize",
    "QuickSlash",
    "DaggerThrow",
    "Backstab",
    "BallLightning",
    "ColdSnap",
}

SLY = {"Backstab", "ShadowStep"}


@dataclass(frozen=True)
class TurnState:
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
    potion_used: bool
    any_discard: bool
    attack_count: int
    first_discard_done: bool
    sharp_used: bool
    next_energy_bonus: int
    next_draw_bonus: int


def sort_cards(cards: tuple[str, ...] | list[str]) -> tuple[str, ...]:
    return tuple(sorted(cards))


def display_deck(deck: tuple[str, ...]) -> str:
    counts = Counter(deck)
    parts = []
    for card in CARD_ORDER:
        n = counts[card]
        if n == 1:
            parts.append(CARD_CN[card])
        elif n > 1:
            parts.append(f"{CARD_CN[card]} x{n}")
    return "、".join(parts)


def display_build(deck: tuple[str, ...], potion: str, relic: str) -> str:
    return f"{display_deck(deck)}；{POTION_CN[potion]}；{RELIC_CN[relic]}"


def unique_decks() -> list[tuple[str, ...]]:
    decks = set()
    for pick in combinations(range(len(CARDS)), 8):
        decks.add(sort_cards([CARDS[i] for i in pick]))
    return sorted(decks)


def remove_one(cards: tuple[str, ...], card: str) -> tuple[str, ...]:
    out = list(cards)
    out.remove(card)
    return sort_cards(out)


def add_card(cards: tuple[str, ...], card: str) -> tuple[str, ...]:
    return sort_cards(cards + (card,))


def attack_damage(base: int, vulnerable: int) -> int:
    return (base * 3) // 2 if vulnerable > 0 else base


def weak_damage(base: int, weak: int) -> int:
    return (base * 3) // 4 if weak > 0 else base


def deal_damage(enemy_hp: int, armor: int, amount: int) -> tuple[int, int]:
    absorbed = min(armor, amount)
    return enemy_hp - (amount - absorbed), armor - absorbed


@lru_cache(maxsize=None)
def draw_outcomes(cards: tuple[str, ...], count: int) -> tuple[tuple[tuple[str, ...], tuple[str, ...], float], ...]:
    cards = sort_cards(cards)
    if count <= 0:
        return (((), cards, 1.0),)
    if not cards:
        return (((), (), 1.0),)
    if count >= len(cards):
        return ((cards, (), 1.0),)
    total = comb(len(cards), count)
    merged: dict[tuple[tuple[str, ...], tuple[str, ...]], float] = defaultdict(float)
    for pick in combinations(range(len(cards)), count):
        pick_set = set(pick)
        hand = sort_cards([cards[i] for i in pick])
        rest = sort_cards([cards[i] for i in range(len(cards)) if i not in pick_set])
        merged[(hand, rest)] += 1 / total
    return tuple((hand, rest, prob) for (hand, rest), prob in merged.items())


@lru_cache(maxsize=None)
def midturn_draws(draw: tuple[str, ...], discard: tuple[str, ...], count: int) -> tuple[tuple[tuple[str, ...], tuple[str, ...], tuple[str, ...], float], ...]:
    draw = sort_cards(draw)
    discard = sort_cards(discard)
    if count <= 0:
        return (((), draw, discard, 1.0),)
    if len(draw) >= count:
        return tuple((hand, next_draw, discard, prob) for hand, next_draw, prob in draw_outcomes(draw, count))
    fixed = draw
    need = count - len(draw)
    if not discard:
        return ((fixed, (), (), 1.0),)
    return tuple((sort_cards(fixed + hand), next_draw, (), prob) for hand, next_draw, prob in draw_outcomes(discard, need))


@lru_cache(maxsize=None)
def next_turn_draws(draw: tuple[str, ...], discard: tuple[str, ...], count: int) -> tuple[tuple[tuple[str, ...], tuple[str, ...], tuple[str, ...], float], ...]:
    return midturn_draws(draw, discard, count)


def merge_weighted(items: list[tuple[float, tuple[float, ...]]]) -> tuple[float, ...]:
    out = [0.0] * 5
    for weight, vec in items:
        for idx, value in enumerate(vec):
            out[idx] += weight * value
    return tuple(out)


def better(a: tuple[float, ...], b: tuple[float, ...] | None) -> bool:
    if b is None:
        return True
    return (sum(a[:-1]), a[0], a[1], a[2], a[3]) > (sum(b[:-1]), b[0], b[1], b[2], b[3])


def kill_vec(turn: int) -> tuple[float, ...]:
    out = [0.0] * 5
    out[turn - 1] = 1.0
    return tuple(out)


def fail_vec() -> tuple[float, ...]:
    return (0.0, 0.0, 0.0, 0.0, 1.0)


def add_attack_count(card: str, state: TurnState) -> int:
    return state.attack_count + (1 if card in ATTACKS else 0)


def apply_attack(state: TurnState, base: int) -> TurnState:
    enemy_hp, armor = deal_damage(state.enemy_hp, state.enemy_armor, attack_damage(base, state.vulnerable))
    return TurnState(
        state.turn,
        state.hand,
        state.draw,
        state.discard,
        state.energy,
        state.hp,
        state.block,
        enemy_hp,
        armor,
        state.vulnerable,
        state.weak,
        state.potion_used,
        state.any_discard,
        state.attack_count,
        state.first_discard_done,
        state.sharp_used,
        state.next_energy_bonus,
    )


def set_state(state: TurnState, **kwargs) -> TurnState:
    data = state.__dict__.copy()
    data.update(kwargs)
    return TurnState(**data)


def after_first_discard_relic(state: TurnState, relic: str) -> tuple[tuple[float, TurnState], ...]:
    if state.first_discard_done:
        return ((1.0, state),)
    state = set_state(state, first_discard_done=True)
    if relic == "HollowAmulet":
        return ((1.0, set_state(state, block=state.block + 5)),)
    if relic == "ReturnHolster":
        return ((1.0, set_state(state, next_draw_bonus=state.next_draw_bonus + 1)),)
    return ((1.0, state),)


def play_sly_from_discard(state: TurnState, card: str, relic: str) -> tuple[tuple[float, TurnState], ...]:
    if card == "Backstab":
        bonus = 0
        sharp_used = state.sharp_used
        if relic == "SharpDice" and not state.sharp_used:
            bonus = 4
            sharp_used = True
        state = set_state(state, sharp_used=sharp_used, attack_count=state.attack_count + 1)
        state = apply_attack(state, 10 + bonus)
        state = set_state(state, discard=add_card(state.discard, card))
        return ((1.0, state),)
    if card == "ShadowStep":
        state = set_state(state, block=state.block + 4, discard=add_card(state.discard, card))
        return ((1.0, state),)
    raise ValueError(card)


def discard_card(state: TurnState, card: str, relic: str) -> tuple[tuple[float, TurnState], ...]:
    state = set_state(state, hand=remove_one(state.hand, card), any_discard=True)
    if card in SLY:
        sly_states = play_sly_from_discard(state, card, relic)
    else:
        sly_states = ((1.0, set_state(state, discard=add_card(state.discard, card))),)
    out = []
    for p1, sly_state in sly_states:
        for p2, relic_state in after_first_discard_relic(sly_state, relic):
            out.append((p1 * p2, relic_state))
    return tuple(out)


def discard_cards_ordered(state: TurnState, cards: tuple[str, ...], relic: str) -> tuple[tuple[float, TurnState], ...]:
    states = ((1.0, state),)
    for card in cards:
        next_states = []
        for p, cur in states:
            if card not in cur.hand:
                continue
            for p2, nxt in discard_card(cur, card, relic):
                next_states.append((p * p2, nxt))
        states = tuple(next_states)
    return states


def play_card_outcomes(state: TurnState, card: str, relic: str) -> tuple[tuple[float, TurnState], ...]:
    if state.energy < CARD_COST[card] or card not in state.hand:
        return ()
    base = set_state(
        state,
        hand=remove_one(state.hand, card),
        energy=state.energy - CARD_COST[card],
        attack_count=add_attack_count(card, state),
    )

    def done(s: TurnState) -> tuple[tuple[float, TurnState], ...]:
        return ((1.0, set_state(s, discard=add_card(s.discard, card))),)

    if card == "Strike":
        return done(apply_attack(base, 6))
    if card == "Defend":
        return done(set_state(base, block=base.block + 5))
    if card == "Bash":
        s = apply_attack(base, 8)
        return done(set_state(s, vulnerable=s.vulnerable + 2))
    if card == "Neutralize":
        s = apply_attack(base, 3)
        return done(set_state(s, weak=s.weak + 1))
    if card == "QuickSlash":
        return done(apply_attack(base, 8))
    if card == "DaggerThrow":
        return done(apply_attack(base, 9))
    if card == "Backstab":
        return done(apply_attack(base, 7))
    if card == "ShadowStep":
        return done(set_state(base, block=base.block + 4))
    if card == "BallLightning":
        return done(apply_attack(base, 7))
    if card == "ColdSnap":
        return done(set_state(apply_attack(base, 6), block=base.block + 4))
    if card == "Acrobatics":
        s = set_state(base, next_draw_bonus=base.next_draw_bonus + 2)
        if not s.hand:
            return done(s)
        out = []
        for discard_target in sorted(set(s.hand)):
            for p2, ds in discard_card(s, discard_target, relic):
                out.append((p2, set_state(ds, discard=add_card(ds.discard, card))))
        return tuple(out)
    if card == "Survivor":
        s = set_state(base, block=base.block + 8)
        if not s.hand:
            return done(s)
        out = []
        for discard_target in sorted(set(s.hand)):
            for p2, ds in discard_card(s, discard_target, relic):
                out.append((p2, set_state(ds, discard=add_card(ds.discard, card))))
        return tuple(out)
    if card == "Prepared":
        s = set_state(base, next_energy_bonus=base.next_energy_bonus + 1)
        if not s.hand:
            return done(s)
        out = []
        for discard_target in sorted(set(s.hand)):
            for p2, ds in discard_card(s, discard_target, relic):
                out.append((p2, set_state(ds, discard=add_card(ds.discard, card))))
        return tuple(out)
    raise ValueError(card)


def end_turn_value(state: TurnState, potion: str, relic: str) -> tuple[float, ...]:
    if state.enemy_hp <= 0:
        return kill_vec(state.turn)
    if state.turn >= 4:
        return fail_vec()
    incoming = 0
    armor_gain = 0
    if state.turn == 1 and not state.any_discard:
        armor_gain = 10
    elif state.turn == 2:
        incoming = weak_damage(12, state.weak)
        if state.attack_count >= 2:
            armor_gain = 8
    elif state.turn == 3:
        incoming = weak_damage(20, state.weak)

    hp_after = state.hp - max(0, incoming - state.block)
    if hp_after <= 0:
        return fail_vec()
    discard_after = sort_cards(state.discard + state.hand)
    weighted = []
    for next_hand, next_draw, next_discard, prob in next_turn_draws(state.draw, discard_after, 5 + state.next_draw_bonus):
        next_state = TurnState(
            turn=state.turn + 1,
            hand=next_hand,
            draw=next_draw,
            discard=next_discard,
            energy=3 + state.next_energy_bonus,
            hp=hp_after,
            block=0,
            enemy_hp=state.enemy_hp,
            enemy_armor=state.enemy_armor + armor_gain,
            vulnerable=max(0, state.vulnerable - 1),
            weak=max(0, state.weak - 1),
            potion_used=state.potion_used,
            any_discard=False,
            attack_count=0,
            first_discard_done=False,
            sharp_used=False,
            next_energy_bonus=0,
            next_draw_bonus=0,
        )
        weighted.append((prob, state_value(next_state, potion, relic)))
    return merge_weighted(weighted)


@lru_cache(maxsize=None)
def state_value(state: TurnState, potion: str, relic: str) -> tuple[float, ...]:
    if state.enemy_hp <= 0:
        return kill_vec(state.turn)
    best = end_turn_value(state, potion, relic)

    if not state.potion_used:
        if potion == "Fire":
            hp, armor = deal_damage(state.enemy_hp, state.enemy_armor, 20)
            cand = state_value(set_state(state, enemy_hp=hp, enemy_armor=armor, potion_used=True), potion, relic)
            if better(cand, best):
                best = cand
        elif potion == "Vulnerable":
            cand = state_value(set_state(state, vulnerable=state.vulnerable + 2, potion_used=True), potion, relic)
            if better(cand, best):
                best = cand
        elif potion == "Gambler" and state.hand:
            for discard_target in sorted(set(state.hand)):
                weighted = []
                start = set_state(state, potion_used=True, energy=state.energy + 1)
                for prob, next_state in discard_card(start, discard_target, relic):
                    weighted.append((prob, state_value(next_state, potion, relic)))
                cand = merge_weighted(weighted)
                if better(cand, best):
                    best = cand

    for card in sorted(set(state.hand), key=CARD_ORDER.index):
        if state.energy < CARD_COST[card]:
            continue
        weighted = []
        for prob, next_state in play_card_outcomes(state, card, relic):
            weighted.append((prob, state_value(next_state, potion, relic)))
        if weighted:
            cand = merge_weighted(weighted)
            if better(cand, best):
                best = cand
    return best


def initial_result(deck: tuple[str, ...], potion: str, relic: str) -> tuple[float, ...]:
    state_value.cache_clear()
    weighted = []
    for hand, draw, prob in draw_outcomes(deck, 5):
        state = TurnState(
            turn=1,
            hand=hand,
            draw=draw,
            discard=(),
            energy=3,
            hp=18,
            block=0,
            enemy_hp=132,
            enemy_armor=0,
            vulnerable=0,
            weak=0,
            potion_used=False,
            any_discard=False,
            attack_count=0,
            first_discard_done=False,
            sharp_used=False,
            next_energy_bonus=0,
            next_draw_bonus=0,
        )
        weighted.append((prob, state_value(state, potion, relic)))
    return merge_weighted(weighted)


def first_kill_turn(vec: tuple[float, ...]) -> int | None:
    for idx, value in enumerate(vec[:-1], 1):
        if value > 1e-9:
            return idx
    return None


def classify(deck: tuple[str, ...], potion: str, relic: str) -> str:
    c = Counter(deck)
    if potion == "Gambler" and c["Backstab"] >= 2:
        return "赌徒狡黠快线"
    if relic == "SharpDice" and c["Backstab"] >= 2:
        return "锋利骰子弃牌爆发线"
    if relic == "ReturnHolster" and (c["Acrobatics"] or c["ShadowStep"]):
        return "折返抽牌循环线"
    if relic == "HollowAmulet" and (c["Survivor"] or c["Defend"] >= 2):
        return "空心护符生存线"
    if potion == "Vulnerable" and c["Bash"]:
        return "易伤叠加线"
    if potion == "Fire":
        return "火焰补刀线"
    return "混合线"


def pct(value: float) -> float:
    return round(value * 100, 4)


def run() -> dict:
    rows = []
    decks = unique_decks()
    total = len(decks) * 9
    done = 0
    for deck in decks:
        for potion in POTION_CN:
            for relic in RELIC_CN:
                done += 1
                result = initial_result(deck, potion, relic)
                row = {
                    "deck": list(deck),
                    "potion": potion,
                    "relic": relic,
                    "build_display": display_build(deck, potion, relic),
                    "first_turn": first_kill_turn(result),
                    "kill_vector": [pct(x) for x in result],
                    "success": pct(sum(result[:-1])),
                    "fail": pct(result[-1]),
                    "family": classify(deck, potion, relic),
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
        "perfect_success_count": sum(1 for row in rows if row["success"] >= 99.9999),
        "top30": rows[:30],
        "best_by_turn": {turn: vals[:10] for turn, vals in by_turn.items()},
        "best_by_family": {family: vals[:10] for family, vals in by_family.items()},
    }


def main() -> None:
    summary = run()
    with open("difficulty3_sly_audit.json", "w", encoding="utf-8") as f:
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
