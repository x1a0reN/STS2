from __future__ import annotations

from dataclasses import dataclass
from functools import lru_cache
from itertools import combinations, product
from math import comb


CARD_COST = {
    "Strike": 1,
    "Perfected Strike": 2,
    "Uppercut": 2,
    "Bash": 2,
    "Iron Wave": 1,
    "Defend": 1,
}


@dataclass(frozen=True)
class Variant:
    name: str
    card_pool: tuple[str, ...]
    potions: tuple[str | None, ...]
    relics: tuple[str | None, ...]
    enemy_hp: int
    player_hp: int = 8
    draw_per_turn: int = 5
    max_turn: int = 3
    min_deck_size: int = 7
    max_deck_size: int = 9


def deck_signature(deck: tuple[str, ...]) -> tuple[str, ...]:
    return tuple(sorted(deck))


def strike_count(deck: tuple[str, ...]) -> int:
    return sum(1 for card in deck if "Strike" in card)


def vuln_damage(base: int, vulnerable: int) -> int:
    return (base * 3) // 2 if vulnerable > 0 else base


def unique_decks(card_pool: tuple[str, ...], min_size: int, max_size: int) -> list[tuple[str, ...]]:
    decks: set[tuple[str, ...]] = set()
    for size in range(min_size, max_size + 1):
        for pick in combinations(range(len(card_pool)), size):
            decks.add(deck_signature(tuple(card_pool[i] for i in pick)))
    return sorted(decks)


def exact_case_result(
    deck: tuple[str, ...],
    relic: str | None,
    potion: str | None,
    enemy_hp: int,
    player_hp: int,
    draw_per_turn: int,
    max_turn: int,
) -> tuple[float, float, float, float]:
    deck = tuple(deck)
    sc = strike_count(deck)
    turn_one_energy = 4 if relic == "Lantern" else 3
    start_block = 10 if relic == "Anchor" else 0

    hand_size = min(draw_per_turn, len(deck))
    total = comb(len(deck), hand_size)
    initial_hands: dict[tuple[str, ...], float] = {}
    for pick in combinations(range(len(deck)), hand_size):
        hand = tuple(sorted(deck[i] for i in pick))
        initial_hands[hand] = initial_hands.get(hand, 0.0) + 1 / total

    def next_hand_distribution(hand: tuple[str, ...], turn: int) -> dict[tuple[str, ...], float]:
        if turn == 1:
            deck_list = list(deck)
            hand_list = list(hand)
            remaining = deck_list.copy()
            for card in hand_list:
                remaining.remove(card)
            need = max(0, draw_per_turn - len(remaining))
            if need == 0:
                return {tuple(sorted(remaining[:draw_per_turn])): 1.0}
            total2 = comb(len(hand_list), need)
            out: dict[tuple[str, ...], float] = {}
            for pick in combinations(range(len(hand_list)), need):
                redraw = [hand_list[i] for i in pick]
                next_hand = tuple(sorted(remaining + redraw))
                out[next_hand] = out.get(next_hand, 0.0) + 1 / total2
            return out

        if turn == 2:
            hand_size2 = min(draw_per_turn, len(deck))
            total2 = comb(len(deck), hand_size2)
            out: dict[tuple[str, ...], float] = {}
            for pick in combinations(range(len(deck)), hand_size2):
                next_hand = tuple(sorted(deck[i] for i in pick))
                out[next_hand] = out.get(next_hand, 0.0) + 1 / total2
            return out

        return {}

    def enumerate_action_states(
        hand: tuple[str, ...],
        energy: int,
        base_strength: int,
        starting_block: int,
        cur_enemy_hp: int,
        vulnerable: int,
        used_potion: bool,
    ) -> list[tuple[tuple[str, ...], int, int, int, int, int, bool]]:
        seen: set[tuple[tuple[str, ...], int, int, int, int, int, bool]] = set()
        out: list[tuple[tuple[str, ...], int, int, int, int, int, bool]] = []

        def rec(
            cur_hand: tuple[str, ...],
            cur_energy: int,
            cur_strength: int,
            cur_block: int,
            cur_enemy_hp2: int,
            cur_vulnerable: int,
            cur_used_potion2: bool,
        ) -> None:
            state = (
                tuple(sorted(cur_hand)),
                cur_energy,
                cur_strength,
                cur_block,
                cur_enemy_hp2,
                cur_vulnerable,
                cur_used_potion2,
            )
            if state in seen:
                return
            seen.add(state)
            out.append(state)

            if not cur_used_potion2 and potion:
                if potion == "Fire Potion":
                    rec(cur_hand, cur_energy, cur_strength, cur_block, cur_enemy_hp2 - 20, cur_vulnerable, True)
                elif potion == "Strength Potion":
                    rec(cur_hand, cur_energy, cur_strength + 2, cur_block, cur_enemy_hp2, cur_vulnerable, True)
                elif potion == "Block Potion":
                    rec(cur_hand, cur_energy, cur_strength, cur_block + 12, cur_enemy_hp2, cur_vulnerable, True)

            for idx, card in enumerate(cur_hand):
                cost = CARD_COST[card]
                if cost > cur_energy:
                    continue
                next_hand = list(cur_hand)
                next_hand.pop(idx)
                next_energy = cur_energy - cost
                next_strength = cur_strength
                next_block = cur_block
                next_enemy_hp3 = cur_enemy_hp2
                next_vulnerable = cur_vulnerable

                if card == "Strike":
                    next_enemy_hp3 -= vuln_damage(6 + next_strength, next_vulnerable)
                elif card == "Perfected Strike":
                    next_enemy_hp3 -= vuln_damage(6 + 2 * sc + next_strength, next_vulnerable)
                elif card == "Uppercut":
                    next_enemy_hp3 -= vuln_damage(13 + next_strength, next_vulnerable)
                    next_vulnerable += 1
                elif card == "Bash":
                    next_enemy_hp3 -= vuln_damage(8 + next_strength, next_vulnerable)
                    next_vulnerable += 2
                elif card == "Iron Wave":
                    next_block += 5
                    next_enemy_hp3 -= vuln_damage(5 + next_strength, next_vulnerable)
                elif card == "Defend":
                    next_block += 5

                rec(
                    tuple(next_hand),
                    next_energy,
                    next_strength,
                    next_block,
                    next_enemy_hp3,
                    next_vulnerable,
                    cur_used_potion2,
                )

        rec(hand, energy, base_strength, starting_block, cur_enemy_hp, vulnerable, used_potion)
        return out

    @lru_cache(maxsize=None)
    def value(
        turn: int,
        hand: tuple[str, ...],
        hp: int,
        cur_enemy_hp: int,
        vulnerable: int,
        strength: int,
        used_potion: bool,
    ) -> tuple[float, float, float, float]:
        best: tuple[float, float, float, float] | None = None

        for _, _, next_strength, block, next_enemy_hp, next_vulnerable, next_used_potion in enumerate_action_states(
            hand=hand,
            energy=turn_one_energy if turn == 1 else 3,
            base_strength=strength,
            starting_block=start_block if turn == 1 else 0,
            cur_enemy_hp=cur_enemy_hp,
            vulnerable=vulnerable,
            used_potion=used_potion,
        ):
            if next_enemy_hp <= 0:
                vector = [0.0, 0.0, 0.0, 0.0]
                vector[turn - 1] = 1.0
                candidate = tuple(vector)
            else:
                enemy_damage = 0 if turn == 1 else 9 if turn == 2 else 11
                hp_after = hp - max(0, enemy_damage - block)
                if turn >= max_turn or hp_after <= 0:
                    candidate = (0.0, 0.0, 0.0, 1.0)
                else:
                    post_vulnerable = max(0, next_vulnerable - 1)
                    merged = [0.0, 0.0, 0.0, 0.0]
                    for next_hand, prob in next_hand_distribution(hand, turn).items():
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

    total_out = [0.0, 0.0, 0.0, 0.0]
    for hand, prob in initial_hands.items():
        result = value(1, hand, player_hp, enemy_hp, 0, 0, False)
        for idx in range(4):
            total_out[idx] += prob * result[idx]
    return tuple(total_out)


def audit_variant(variant: Variant) -> dict[str, object]:
    rows: list[dict[str, object]] = []
    stable_routes: list[dict[str, object]] = []
    top_route: dict[str, object] | None = None

    for deck in unique_decks(variant.card_pool, variant.min_deck_size, variant.max_deck_size):
        for relic, potion in product(variant.relics, variant.potions):
            exact = exact_case_result(
                deck=deck,
                relic=relic,
                potion=potion,
                enemy_hp=variant.enemy_hp,
                player_hp=variant.player_hp,
                draw_per_turn=variant.draw_per_turn,
                max_turn=variant.max_turn,
            )
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
            if exact[3] < 1e-12:
                stable_routes.append(row)
            if top_route is None or (row["t1"], row["t2"], row["t3"]) > (top_route["t1"], top_route["t2"], top_route["t3"]):
                top_route = row

    return {
        "variant": variant.name,
        "case_count": len(rows),
        "stable_count": len(stable_routes),
        "top_route": top_route,
        "stable_routes": stable_routes[:10],
    }


def main() -> None:
    variants = [
        Variant(
            name="no_fire_no_lantern_hp54",
            card_pool=("Strike", "Strike", "Strike", "Perfected Strike", "Bash", "Iron Wave", "Defend", "Defend", "Defend", "Defend"),
            potions=(None, "Strength Potion", "Block Potion"),
            relics=(None, "Anchor"),
            enemy_hp=54,
        ),
        Variant(
            name="no_fire_no_lantern_hp56",
            card_pool=("Strike", "Strike", "Strike", "Perfected Strike", "Bash", "Iron Wave", "Defend", "Defend", "Defend", "Defend"),
            potions=(None, "Strength Potion", "Block Potion"),
            relics=(None, "Anchor"),
            enemy_hp=56,
        ),
        Variant(
            name="no_fire_no_lantern_uppercut_hp54",
            card_pool=("Strike", "Strike", "Strike", "Perfected Strike", "Uppercut", "Iron Wave", "Defend", "Defend", "Defend", "Defend"),
            potions=(None, "Strength Potion", "Block Potion"),
            relics=(None, "Anchor"),
            enemy_hp=54,
        ),
        Variant(
            name="no_fire_no_lantern_uppercut_hp56",
            card_pool=("Strike", "Strike", "Strike", "Perfected Strike", "Uppercut", "Iron Wave", "Defend", "Defend", "Defend", "Defend"),
            potions=(None, "Strength Potion", "Block Potion"),
            relics=(None, "Anchor"),
            enemy_hp=56,
        ),
    ]

    for variant in variants:
        result = audit_variant(variant)
        print(result["variant"])
        print("case_count", result["case_count"])
        print("stable_count", result["stable_count"])
        print("top_route", result["top_route"])
        print("stable_routes", result["stable_routes"])
        print()


if __name__ == "__main__":
    main()
