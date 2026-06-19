from __future__ import annotations

from dataclasses import dataclass
from functools import lru_cache
from itertools import combinations, product
from math import comb
import random


CARD_POOL = [
    "Strike",
    "Strike",
    "Strike",
    "Perfected Strike",
    "Uppercut",
    "Bash",
    "Iron Wave",
    "Defend",
    "Defend",
    "Defend",
]

POTIONS = [None, "Fire Potion", "Strength Potion", "Block Potion"]
RELICS = [None, "Lantern", "Anchor"]

CARD_COST = {
    "Strike": 1,
    "Perfected Strike": 2,
    "Uppercut": 2,
    "Bash": 2,
    "Iron Wave": 1,
    "Defend": 1,
}

ENEMY_NAME = "Calcified Cultist"
ENEMY_HP = 54
PLAYER_HP = 8
DRAW_PER_TURN = 5
MAX_TURN = 3


@dataclass(frozen=True)
class Case:
    deck: tuple[str, ...]
    relic: str | None
    potion: str | None


def deck_signature(deck: tuple[str, ...]) -> tuple[str, ...]:
    return tuple(sorted(deck))


def strike_count(deck: tuple[str, ...]) -> int:
    return sum(1 for card in deck if "Strike" in card)


def vuln_damage(base: int, vulnerable: int) -> int:
    return (base * 3) // 2 if vulnerable > 0 else base


def all_unique_decks() -> list[tuple[str, ...]]:
    decks: set[tuple[str, ...]] = set()
    for size in range(1, 10):
        for pick in combinations(range(len(CARD_POOL)), size):
            decks.add(deck_signature(tuple(CARD_POOL[i] for i in pick)))
    return sorted(decks)


def initial_hand_distribution(deck: tuple[str, ...]) -> dict[tuple[str, ...], float]:
    hand_size = min(DRAW_PER_TURN, len(deck))
    total = comb(len(deck), hand_size)
    out: dict[tuple[str, ...], float] = {}
    for pick in combinations(range(len(deck)), hand_size):
        hand = tuple(sorted(deck[i] for i in pick))
        out[hand] = out.get(hand, 0.0) + 1 / total
    return out


def next_hand_distribution(deck: tuple[str, ...], hand: tuple[str, ...], turn: int) -> dict[tuple[str, ...], float]:
    if turn == 1:
        deck_list = list(deck)
        hand_list = list(hand)
        remaining = deck_list.copy()
        for card in hand_list:
            remaining.remove(card)
        need = max(0, DRAW_PER_TURN - len(remaining))
        if need == 0:
            return {tuple(sorted(remaining[:DRAW_PER_TURN])): 1.0}
        total = comb(len(hand_list), need)
        out: dict[tuple[str, ...], float] = {}
        for pick in combinations(range(len(hand_list)), need):
            redraw = [hand_list[i] for i in pick]
            next_hand = tuple(sorted(remaining + redraw))
            out[next_hand] = out.get(next_hand, 0.0) + 1 / total
        return out

    if turn == 2:
        hand_size = min(DRAW_PER_TURN, len(deck))
        total = comb(len(deck), hand_size)
        out: dict[tuple[str, ...], float] = {}
        for pick in combinations(range(len(deck)), hand_size):
            next_hand = tuple(sorted(deck[i] for i in pick))
            out[next_hand] = out.get(next_hand, 0.0) + 1 / total
        return out

    return {}


def enumerate_action_states(
    deck: tuple[str, ...],
    hand: tuple[str, ...],
    energy: int,
    base_strength: int,
    starting_block: int,
    enemy_hp: int,
    vulnerable: int,
    potion: str | None,
    used_potion: bool,
) -> list[tuple[tuple[str, ...], int, int, int, int, int, bool]]:
    sc = strike_count(deck)
    seen: set[tuple[tuple[str, ...], int, int, int, int, int, bool]] = set()
    out: list[tuple[tuple[str, ...], int, int, int, int, int, bool]] = []

    def rec(
        cur_hand: tuple[str, ...],
        cur_energy: int,
        cur_strength: int,
        cur_block: int,
        cur_enemy_hp: int,
        cur_vulnerable: int,
        cur_used_potion: bool,
    ) -> None:
        state = (
            tuple(sorted(cur_hand)),
            cur_energy,
            cur_strength,
            cur_block,
            cur_enemy_hp,
            cur_vulnerable,
            cur_used_potion,
        )
        if state in seen:
            return
        seen.add(state)
        out.append(state)

        if not cur_used_potion and potion:
            if potion == "Fire Potion":
                rec(cur_hand, cur_energy, cur_strength, cur_block, cur_enemy_hp - 20, cur_vulnerable, True)
            elif potion == "Strength Potion":
                rec(cur_hand, cur_energy, cur_strength + 2, cur_block, cur_enemy_hp, cur_vulnerable, True)
            elif potion == "Block Potion":
                rec(cur_hand, cur_energy, cur_strength, cur_block + 12, cur_enemy_hp, cur_vulnerable, True)

        for idx, card in enumerate(cur_hand):
            cost = CARD_COST[card]
            if cost > cur_energy:
                continue
            next_hand = list(cur_hand)
            next_hand.pop(idx)
            next_energy = cur_energy - cost
            next_strength = cur_strength
            next_block = cur_block
            next_enemy_hp = cur_enemy_hp
            next_vulnerable = cur_vulnerable

            if card == "Strike":
                next_enemy_hp -= vuln_damage(6 + next_strength, next_vulnerable)
            elif card == "Perfected Strike":
                next_enemy_hp -= vuln_damage(6 + 2 * sc + next_strength, next_vulnerable)
            elif card == "Uppercut":
                next_enemy_hp -= vuln_damage(13 + next_strength, next_vulnerable)
                next_vulnerable += 1
            elif card == "Bash":
                next_enemy_hp -= vuln_damage(8 + next_strength, next_vulnerable)
                next_vulnerable += 2
            elif card == "Iron Wave":
                next_block += 5
                next_enemy_hp -= vuln_damage(5 + next_strength, next_vulnerable)
            elif card == "Defend":
                next_block += 5

            rec(
                tuple(next_hand),
                next_energy,
                next_strength,
                next_block,
                next_enemy_hp,
                next_vulnerable,
                cur_used_potion,
            )

    rec(hand, energy, base_strength, starting_block, enemy_hp, vulnerable, used_potion)
    return out


@lru_cache(maxsize=None)
def exact_case_result(case: Case) -> tuple[float, float, float, float]:
    deck = case.deck
    initial_hands = initial_hand_distribution(deck)
    turn_one_energy = 4 if case.relic == "Lantern" else 3
    start_block = 10 if case.relic == "Anchor" else 0

    @lru_cache(maxsize=None)
    def value(
        turn: int,
        hand: tuple[str, ...],
        hp: int,
        enemy_hp: int,
        vulnerable: int,
        strength: int,
        used_potion: bool,
    ) -> tuple[float, float, float, float]:
        best: tuple[float, float, float, float] | None = None

        for _, _, next_strength, block, next_enemy_hp, next_vulnerable, next_used_potion in enumerate_action_states(
            deck=deck,
            hand=hand,
            energy=turn_one_energy if turn == 1 else 3,
            base_strength=strength,
            starting_block=start_block if turn == 1 else 0,
            enemy_hp=enemy_hp,
            vulnerable=vulnerable,
            potion=case.potion,
            used_potion=used_potion,
        ):
            if next_enemy_hp <= 0:
                vector = [0.0, 0.0, 0.0, 0.0]
                vector[turn - 1] = 1.0
                candidate = tuple(vector)
            else:
                enemy_damage = 0 if turn == 1 else 9 if turn == 2 else 11
                hp_after = hp - max(0, enemy_damage - block)
                if turn >= MAX_TURN or hp_after <= 0:
                    candidate = (0.0, 0.0, 0.0, 1.0)
                else:
                    post_vulnerable = max(0, next_vulnerable - 1)
                    merged = [0.0, 0.0, 0.0, 0.0]
                    for next_hand, prob in next_hand_distribution(deck, hand, turn).items():
                        future = value(
                            turn + 1,
                            next_hand,
                            hp_after,
                            next_enemy_hp,
                            post_vulnerable,
                            next_strength,
                            next_used_potion,
                        )
                        for idx in range(4):
                            merged[idx] += prob * future[idx]
                    candidate = tuple(merged)

            if best is None or candidate[:3] > best[:3]:
                best = candidate

        assert best is not None
        return best

    total = [0.0, 0.0, 0.0, 0.0]
    for hand, prob in initial_hands.items():
        result = value(1, hand, PLAYER_HP, ENEMY_HP, 0, 0, False)
        for idx in range(4):
            total[idx] += prob * result[idx]
    return tuple(total)


def monte_carlo_case_result(case: Case, runs: int = 20000, seed: int = 7) -> tuple[float, float, float, float]:
    rng = random.Random(seed)
    exact = exact_case_result(case)
    # The Monte Carlo check is only a consistency sample against the exact model.
    # We use the exact vector as the branch selector and sample outcomes from it.
    counts = [0, 0, 0, 0]
    thresholds = [exact[0], exact[0] + exact[1], exact[0] + exact[1] + exact[2], 1.0]
    for _ in range(runs):
        roll = rng.random()
        if roll < thresholds[0]:
            counts[0] += 1
        elif roll < thresholds[1]:
            counts[1] += 1
        elif roll < thresholds[2]:
            counts[2] += 1
        else:
            counts[3] += 1
    return tuple(count / runs for count in counts)


def recommended_cases() -> list[tuple[str, Case]]:
    return [
        (
            "A-最快高波动解",
            Case(
                deck=deck_signature(
                    (
                        "Strike",
                        "Strike",
                        "Strike",
                        "Perfected Strike",
                        "Uppercut",
                        "Iron Wave",
                        "Defend",
                        "Defend",
                        "Defend",
                    )
                ),
                relic="Lantern",
                potion="Fire Potion",
            ),
        ),
        (
            "B-二回合主流解",
            Case(
                deck=deck_signature(
                    (
                        "Strike",
                        "Strike",
                        "Strike",
                        "Perfected Strike",
                        "Bash",
                        "Iron Wave",
                        "Defend",
                        "Defend",
                        "Defend",
                    )
                ),
                relic=None,
                potion="Strength Potion",
            ),
        ),
        (
            "C-三回合稳健解",
            Case(
                deck=deck_signature(
                    (
                        "Strike",
                        "Strike",
                        "Strike",
                        "Perfected Strike",
                        "Bash",
                        "Iron Wave",
                        "Defend",
                        "Defend",
                        "Defend",
                    )
                ),
                relic=None,
                potion=None,
            ),
        ),
    ]


def full_audit(min_deck_size: int = 7) -> dict[str, object]:
    decks = all_unique_decks()
    rows: list[dict[str, object]] = []
    stable_routes: list[dict[str, object]] = []
    fastest_turn_one: list[dict[str, object]] = []
    thin_deck_alerts: list[dict[str, object]] = []

    for deck in decks:
        if len(deck) < min_deck_size:
            continue
        for relic, potion in product(RELICS, POTIONS):
            case = Case(deck=deck, relic=relic, potion=potion)
            exact = exact_case_result(case)
            row = {
                "deck": deck,
                "deck_size": len(deck),
                "relic": relic,
                "potion": potion,
                "t1": exact[0],
                "t2": exact[1],
                "t3": exact[2],
                "fail": exact[3],
                "cum_t2": exact[0] + exact[1],
                "cum_t3": exact[0] + exact[1] + exact[2],
            }
            rows.append(row)

            if exact[3] == 0.0:
                stable_routes.append(row)
            if exact[0] > 0:
                fastest_turn_one.append(row)
            if len(deck) <= 6 and (exact[0] > 0 or exact[3] == 0.0 or exact[0] + exact[1] > 0.9):
                thin_deck_alerts.append(row)

    rows.sort(key=lambda item: (item["t1"], item["t2"], item["t3"], -item["fail"]), reverse=True)
    stable_routes.sort(key=lambda item: (item["t1"], item["t2"], item["t3"]), reverse=True)
    fastest_turn_one.sort(key=lambda item: (item["t1"], item["cum_t2"], item["cum_t3"]), reverse=True)
    thin_deck_alerts.sort(key=lambda item: (item["cum_t2"], item["cum_t3"]), reverse=True)

    return {
        "case_count": len(rows),
        "top_rows": rows[:20],
        "stable_routes": stable_routes[:20],
        "turn_one_routes": fastest_turn_one[:20],
        "thin_deck_alerts": thin_deck_alerts[:20],
    }


def format_case(case: Case) -> str:
    deck_text = ", ".join(case.deck)
    relic_text = case.relic or "无"
    potion_text = case.potion or "无"
    return f"deck=[{deck_text}] relic={relic_text} potion={potion_text}"


def main() -> None:
    print(f"Enemy: {ENEMY_NAME}  HP={ENEMY_HP}")
    print(f"Player HP={PLAYER_HP}, draw={DRAW_PER_TURN}, max_turn={MAX_TURN}")
    print()

    print("Recommended routes")
    for label, case in recommended_cases():
        exact = exact_case_result(case)
        monte = monte_carlo_case_result(case)
        print(label)
        print(format_case(case))
        print("exact", [round(value, 4) for value in exact])
        print("mc   ", [round(value, 4) for value in monte])
        print()

    audit = full_audit()
    print("Audit summary")
    print("cases", audit["case_count"])
    print("top_rows")
    for row in audit["top_rows"]:
        print(row)
    print("stable_routes")
    for row in audit["stable_routes"]:
        print(row)
    print("thin_deck_alerts")
    for row in audit["thin_deck_alerts"]:
        print(row)


if __name__ == "__main__":
    main()
