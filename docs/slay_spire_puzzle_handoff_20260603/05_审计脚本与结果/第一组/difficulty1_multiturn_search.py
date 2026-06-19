from __future__ import annotations

import argparse
import json
from collections import Counter
from dataclasses import dataclass
from functools import lru_cache
from itertools import combinations
from math import comb


CARD_COST = {
    "Anger": 0,
    "Armaments": 1,
    "Battle Trance": 0,
    "Bloodletting": 0,
    "Burning Pact": 1,
    "Clash": 0,
    "Rage": 0,
    "Body Slam": 1,
    "Thunderclap": 1,
    "Strike": 1,
    "Grapple": 1,
    "Headbutt": 1,
    "Pommel Strike": 1,
    "Shrug It Off": 1,
    "Second Wind": 1,
    "Sword Boomerang": 1,
    "Twin Strike": 1,
    "Hemokinesis": 1,
    "Bludgeon": 3,
    "Perfected Strike": 2,
    "Uppercut": 2,
    "Bash": 2,
    "Clothesline": 2,
    "Iron Wave": 1,
    "Defend": 1,
}

ATTACK_CARDS = {
    "Anger",
    "Clash",
    "Body Slam",
    "Thunderclap",
    "Strike",
    "Grapple",
    "Headbutt",
    "Pommel Strike",
    "Sword Boomerang",
    "Twin Strike",
    "Hemokinesis",
    "Bludgeon",
    "Perfected Strike",
    "Uppercut",
    "Bash",
    "Clothesline",
    "Iron Wave",
}

UPGRADE_MAP = {
    "Armaments": "Armaments+",
    "Bash": "Bash+",
    "Defend": "Defend+",
    "Grapple": "Grapple+",
    "Hemokinesis": "Hemokinesis+",
    "Second Wind": "Second Wind+",
    "Strike": "Strike+",
}


@dataclass(frozen=True)
class SearchVariant:
    name: str
    card_pool: tuple[str, ...]
    enemy_hp: int
    player_hp: int
    enemy_damage_by_turn: tuple[int, ...]
    min_deck_size: int = 7
    max_deck_size: int = 9
    draw_per_turn: int = 5


@dataclass(frozen=True)
class ActionState:
    hand: tuple[str, ...]
    can_draw: bool
    rage: int
    grapple: int
    player_hp: int
    enemy_hp: int
    vulnerable: int
    weak: int
    block: int
    draw_fixed: tuple[str, ...]
    draw_bag: tuple[str, ...]
    discard_pile: tuple[str, ...]
    actions: tuple[str, ...]


def deck_signature(deck: tuple[str, ...]) -> tuple[str, ...]:
    return tuple(sorted(deck))


def unique_decks(card_pool: tuple[str, ...], min_size: int, max_size: int) -> list[tuple[str, ...]]:
    decks: set[tuple[str, ...]] = set()
    for size in range(min_size, max_size + 1):
        for pick in combinations(range(len(card_pool)), size):
            decks.add(deck_signature(tuple(card_pool[i] for i in pick)))
    return sorted(decks)


def strike_count(deck: tuple[str, ...]) -> int:
    return sum(1 for card in deck if "Strike" in card)


def card_base_name(card: str) -> str:
    return card[:-1] if card.endswith("+") else card


def upgrade_card(card: str) -> str:
    return UPGRADE_MAP.get(card, card)


def vuln_damage(base: int, vulnerable: int) -> int:
    return (base * 3) // 2 if vulnerable > 0 else base


def weak_damage(base: int, weak: int) -> int:
    return (base * 3) // 4 if weak > 0 else base


def hand_all_attacks(hand: tuple[str, ...]) -> bool:
    return all(card_base_name(card) in ATTACK_CARDS for card in hand)


def add_to_discard(discard_pile: tuple[str, ...], card: str) -> tuple[str, ...]:
    return tuple(sorted(discard_pile + (card,)))


def remove_one_card(cards: tuple[str, ...], target: str) -> tuple[str, ...]:
    remaining = list(cards)
    remaining.remove(target)
    return tuple(sorted(remaining))


def replace_one_card(cards: tuple[str, ...], target: str, replacement: str) -> tuple[str, ...]:
    remaining = list(cards)
    remaining.remove(target)
    remaining.append(replacement)
    return tuple(sorted(remaining))


@lru_cache(maxsize=None)
def draw_outcomes(cards: tuple[str, ...], count: int) -> tuple[tuple[tuple[str, ...], tuple[str, ...], float], ...]:
    if count == 0:
        return (((), tuple(sorted(cards)), 1.0),)

    total = comb(len(cards), count)
    merged: dict[tuple[tuple[str, ...], tuple[str, ...]], float] = {}
    for pick in combinations(range(len(cards)), count):
        chosen = tuple(sorted(cards[i] for i in pick))
        pick_set = set(pick)
        remaining = tuple(sorted(cards[i] for i in range(len(cards)) if i not in pick_set))
        key = (chosen, remaining)
        merged[key] = merged.get(key, 0.0) + 1 / total
    return tuple((chosen, remaining, prob) for (chosen, remaining), prob in merged.items())


@lru_cache(maxsize=None)
def midturn_draw_states(
    draw_fixed: tuple[str, ...],
    draw_bag: tuple[str, ...],
    discard_pile: tuple[str, ...],
    count: int,
) -> tuple[tuple[tuple[str, ...], tuple[str, ...], tuple[str, ...], tuple[str, ...], float], ...]:
    if count == 0:
        return (((), draw_fixed, draw_bag, discard_pile, 1.0),)
    if len(draw_fixed) > 0:
        tail_states = midturn_draw_states(draw_fixed[1:], draw_bag, discard_pile, count - 1)
        return tuple(
            (tuple(sorted((draw_fixed[0],) + drawn_cards)), next_draw_fixed, next_draw_bag, next_discard_pile, prob)
            for drawn_cards, next_draw_fixed, next_draw_bag, next_discard_pile, prob in tail_states
        )
    if len(draw_bag) > 0:
        merged: dict[tuple[tuple[str, ...], tuple[str, ...], tuple[str, ...], tuple[str, ...]], float] = {}
        for hand1, next_draw_bag1, prob1 in draw_outcomes(draw_bag, 1):
            for drawn_cards, next_draw_fixed, next_draw_bag2, next_discard_pile, prob2 in midturn_draw_states((), next_draw_bag1, discard_pile, count - 1):
                key = (tuple(sorted(hand1 + drawn_cards)), next_draw_fixed, next_draw_bag2, next_discard_pile)
                merged[key] = merged.get(key, 0.0) + prob1 * prob2
        return tuple((drawn_cards, next_draw_fixed, next_draw_bag, next_discard_pile, prob) for (drawn_cards, next_draw_fixed, next_draw_bag, next_discard_pile), prob in merged.items())
    if len(discard_pile) > 0:
        return midturn_draw_states((), discard_pile, (), count)
    return (((), (), (), (), 1.0),)


@lru_cache(maxsize=None)
def next_turn_draw_states(
    draw_fixed: tuple[str, ...],
    draw_bag: tuple[str, ...],
    discard_pile: tuple[str, ...],
    hand_size: int,
) -> tuple[tuple[tuple[str, ...], tuple[str, ...], tuple[str, ...], tuple[str, ...], float], ...]:
    draw_count = len(draw_fixed) + len(draw_bag)
    if draw_count >= hand_size:
        fixed_take = min(len(draw_fixed), hand_size)
        fixed_cards = draw_fixed[:fixed_take]
        next_fixed = draw_fixed[fixed_take:]
        need = hand_size - fixed_take
        if need == 0:
            return ((tuple(sorted(fixed_cards)), next_fixed, draw_bag, discard_pile, 1.0),)
        return tuple(
            (tuple(sorted(fixed_cards + hand)), (), next_draw_bag, discard_pile, prob)
            for hand, next_draw_bag, prob in draw_outcomes(draw_bag, need)
        )

    fixed_cards = draw_fixed
    bag_cards = draw_bag
    need = hand_size - draw_count
    out: list[tuple[tuple[str, ...], tuple[str, ...], tuple[str, ...], tuple[str, ...], float]] = []
    for redraw, next_draw_bag, prob in draw_outcomes(discard_pile, need):
        hand = tuple(sorted(fixed_cards + bag_cards + redraw))
        out.append((hand, (), next_draw_bag, (), prob))
    return tuple(out)


def format_action_label(card: str, drawn_cards: tuple[str, ...] = ()) -> str:
    if drawn_cards:
        return f"{card}[draw {', '.join(drawn_cards)}]"
    return card


def format_burning_pact_label(exhausted_card: str, drawn_cards: tuple[str, ...] = ()) -> str:
    if drawn_cards:
        return f"Burning Pact[exhaust {exhausted_card}; draw {', '.join(drawn_cards)}]"
    return f"Burning Pact[exhaust {exhausted_card}]"


def format_second_wind_label(exhausted_cards: tuple[str, ...]) -> str:
    if exhausted_cards:
        return f"Second Wind[exhaust {', '.join(exhausted_cards)}]"
    return "Second Wind"


def deck_display(deck: tuple[str, ...]) -> str:
    counts = Counter(deck)
    parts = []
    for card in sorted(counts):
        count = counts[card]
        parts.append(f"{card} x{count}" if count > 1 else card)
    return ", ".join(parts)


def expected_kill_turn(result: tuple[float, ...]) -> float | None:
    success = sum(result[:-1])
    if success <= 0.0:
        return None
    total = 0.0
    for idx, prob in enumerate(result[:-1], start=1):
        total += idx * prob
    return total / success


def variant_catalog() -> list[SearchVariant]:
    return [
        SearchVariant(
            name="six_strike_def3_perfected_hp50_php8_turn4",
            card_pool=("Strike", "Strike", "Strike", "Strike", "Strike", "Strike", "Defend", "Defend", "Defend", "Perfected Strike"),
            enemy_hp=50,
            player_hp=8,
            enemy_damage_by_turn=(0, 9, 11, 13),
        ),
        SearchVariant(
            name="five_strike_def3_hp50_php8_turn4",
            card_pool=("Strike", "Strike", "Strike", "Strike", "Strike", "Perfected Strike", "Bash", "Defend", "Defend", "Defend"),
            enemy_hp=50,
            player_hp=8,
            enemy_damage_by_turn=(0, 9, 11, 13),
        ),
        SearchVariant(
            name="four_strike_bash_hp50_php8_turn4",
            card_pool=("Strike", "Strike", "Strike", "Strike", "Perfected Strike", "Bash", "Defend", "Defend", "Defend", "Defend"),
            enemy_hp=50,
            player_hp=8,
            enemy_damage_by_turn=(0, 9, 11, 13),
        ),
        SearchVariant(
            name="mixed_hp48_php8_turn4",
            card_pool=("Strike", "Strike", "Strike", "Strike", "Perfected Strike", "Bash", "Uppercut", "Defend", "Defend", "Defend"),
            enemy_hp=48,
            player_hp=8,
            enemy_damage_by_turn=(0, 9, 11, 13),
        ),
        SearchVariant(
            name="mixed_hp50_php10_turn4",
            card_pool=("Strike", "Strike", "Strike", "Strike", "Perfected Strike", "Bash", "Uppercut", "Defend", "Defend", "Defend"),
            enemy_hp=50,
            player_hp=10,
            enemy_damage_by_turn=(0, 9, 11, 13),
        ),
    ]


def exact_multiturn_result(deck: tuple[str, ...], variant: SearchVariant) -> tuple[float, ...]:
    deck = tuple(deck)
    sc = strike_count(deck)
    max_turn = len(variant.enemy_damage_by_turn)
    hand_size = min(variant.draw_per_turn, len(deck))

    @lru_cache(maxsize=None)
    def end_turn_next_states(
        hand: tuple[str, ...],
        draw_fixed: tuple[str, ...],
        draw_bag: tuple[str, ...],
        discard_pile: tuple[str, ...],
    ) -> tuple[tuple[tuple[str, ...], tuple[str, ...], tuple[str, ...], tuple[str, ...], float], ...]:
        discard_after_turn = tuple(sorted(discard_pile + hand))
        return next_turn_draw_states(draw_fixed, draw_bag, discard_after_turn, hand_size)

    @lru_cache(maxsize=None)
    def resolve_turn_end(
        turn: int,
        hand: tuple[str, ...],
        draw_fixed: tuple[str, ...],
        draw_bag: tuple[str, ...],
        discard_pile: tuple[str, ...],
        can_draw: bool,
        hp: int,
        cur_enemy_hp: int,
        vulnerable: int,
        weak: int,
        block: int,
    ) -> tuple[float, ...]:
        vec = [0.0] * (max_turn + 1)
        if hp <= 0:
            vec[-1] = 1.0
            return tuple(vec)
        if cur_enemy_hp <= 0:
            vec[turn - 1] = 1.0
            return tuple(vec)

        enemy_damage = weak_damage(variant.enemy_damage_by_turn[turn - 1], weak)
        hp_after = hp - max(0, enemy_damage - block)
        if turn >= max_turn or hp_after <= 0:
            vec[-1] = 1.0
            return tuple(vec)

        merged = [0.0] * (max_turn + 1)
        post_vulnerable = max(0, vulnerable - 1)
        post_weak = max(0, weak - 1)
        for next_hand, next_draw_fixed, next_draw_bag, next_discard_pile, prob in end_turn_next_states(hand, draw_fixed, draw_bag, discard_pile):
            future = value(
                turn + 1,
                next_hand,
                next_draw_fixed,
                next_draw_bag,
                next_discard_pile,
                True,
                hp_after,
                cur_enemy_hp,
                post_vulnerable,
                post_weak,
            )
            for idx in range(max_turn + 1):
                merged[idx] += prob * future[idx]
        return tuple(merged)

    @lru_cache(maxsize=None)
    def in_turn_best(
        turn: int,
        hand: tuple[str, ...],
        draw_fixed: tuple[str, ...],
        draw_bag: tuple[str, ...],
        discard_pile: tuple[str, ...],
        can_draw: bool,
        rage: int,
        grapple: int,
        energy: int,
        hp: int,
        cur_enemy_hp: int,
        vulnerable: int,
        weak: int,
        block: int,
    ) -> tuple[float, ...]:
        best = resolve_turn_end(turn, hand, draw_fixed, draw_bag, discard_pile, can_draw, hp, cur_enemy_hp, vulnerable, weak, block)
        if hp <= 0 or cur_enemy_hp <= 0:
            return best

        def expected_result(
            outcomes: tuple[tuple[float, tuple[str, ...], tuple[str, ...], tuple[str, ...], tuple[str, ...], bool, int, int, int, int, int, int, int, int], ...],
        ) -> tuple[float, ...]:
            merged = [0.0] * (max_turn + 1)
            for prob, next_hand, next_draw_fixed, next_draw_bag, next_discard_pile, next_can_draw, next_rage, next_grapple, next_energy, next_hp, next_enemy_hp, next_vulnerable, next_weak, next_block in outcomes:
                future = in_turn_best(
                    turn,
                    next_hand,
                    next_draw_fixed,
                    next_draw_bag,
                    next_discard_pile,
                    next_can_draw,
                    next_rage,
                    next_grapple,
                    next_energy,
                    next_hp,
                    next_enemy_hp,
                    next_vulnerable,
                    next_weak,
                    next_block,
                )
                for idx in range(max_turn + 1):
                    merged[idx] += prob * future[idx]
            return tuple(merged)

        for idx, card in enumerate(hand):
            base_card = card_base_name(card)
            upgraded = card != base_card
            cost = CARD_COST[base_card]
            if cost > energy:
                continue
            if base_card == "Clash" and not hand_all_attacks(hand):
                continue

            next_hand_list = list(hand)
            next_hand_list.pop(idx)
            if base_card == "Burning Pact" and not next_hand_list:
                continue

            next_energy = energy - cost
            next_hp = hp
            next_enemy_hp = cur_enemy_hp
            next_vulnerable = vulnerable
            next_weak = weak
            next_block = block
            next_draw_fixed = draw_fixed
            next_draw_bag = draw_bag
            next_discard_pile = discard_pile
            next_can_draw = can_draw
            next_rage = rage
            next_grapple = grapple
            block_gained = 0

            if base_card == "Armaments":
                next_block += 5
                block_gained += 5
            elif base_card == "Battle Trance":
                pass
            elif base_card == "Anger":
                next_enemy_hp -= vuln_damage(6, next_vulnerable)
            elif base_card == "Bloodletting":
                next_hp -= 3
                next_energy += 2
            elif base_card == "Burning Pact":
                pass
            elif base_card == "Rage":
                next_rage += 1
            elif base_card == "Clash":
                next_enemy_hp -= vuln_damage(14, next_vulnerable)
            elif base_card == "Body Slam":
                next_enemy_hp -= vuln_damage(next_block, next_vulnerable)
            elif base_card == "Thunderclap":
                next_enemy_hp -= vuln_damage(4, next_vulnerable)
                next_vulnerable += 1
            elif base_card == "Strike":
                next_enemy_hp -= vuln_damage(9 if upgraded else 6, next_vulnerable)
            elif base_card == "Grapple":
                next_enemy_hp -= vuln_damage(9 if upgraded else 7, next_vulnerable)
                next_grapple += 7 if upgraded else 5
            elif base_card == "Headbutt":
                next_enemy_hp -= vuln_damage(9, next_vulnerable)
            elif base_card == "Pommel Strike":
                next_enemy_hp -= vuln_damage(9, next_vulnerable)
            elif base_card == "Shrug It Off":
                next_block += 8
                block_gained += 8
            elif base_card == "Second Wind":
                pass
            elif base_card == "Sword Boomerang":
                next_enemy_hp -= vuln_damage(3, next_vulnerable)
                next_enemy_hp -= vuln_damage(3, next_vulnerable)
                next_enemy_hp -= vuln_damage(3, next_vulnerable)
            elif base_card == "Twin Strike":
                next_enemy_hp -= vuln_damage(5, next_vulnerable)
                next_enemy_hp -= vuln_damage(5, next_vulnerable)
            elif base_card == "Hemokinesis":
                next_hp -= 2
                next_enemy_hp -= vuln_damage(19 if upgraded else 14, next_vulnerable)
            elif base_card == "Bludgeon":
                next_enemy_hp -= vuln_damage(32, next_vulnerable)
            elif base_card == "Perfected Strike":
                next_enemy_hp -= vuln_damage(6 + 2 * sc, next_vulnerable)
            elif base_card == "Uppercut":
                next_enemy_hp -= vuln_damage(13, next_vulnerable)
                next_vulnerable += 1
            elif base_card == "Bash":
                next_enemy_hp -= vuln_damage(10 if upgraded else 8, next_vulnerable)
                next_vulnerable += 3 if upgraded else 2
            elif base_card == "Clothesline":
                next_enemy_hp -= vuln_damage(12, next_vulnerable)
                next_weak += 2
            elif base_card == "Iron Wave":
                next_block += 5
                block_gained += 5
                next_enemy_hp -= vuln_damage(5, next_vulnerable)
            elif base_card == "Defend":
                gain = 8 if upgraded else 5
                next_block += gain
                block_gained += gain

            if block_gained > 0 and grapple > 0:
                next_enemy_hp -= vuln_damage(grapple, next_vulnerable)
            if base_card in ATTACK_CARDS:
                next_block += 3 * rage

            next_hand = tuple(next_hand_list)
            candidate_results: list[tuple[float, ...]] = []

            if base_card == "Armaments":
                candidate_targets = tuple(sorted({target for target in next_hand if upgrade_card(target) != target}))
                if not candidate_targets:
                    candidate_results.append(
                        in_turn_best(
                            turn,
                            next_hand,
                            next_draw_fixed,
                            next_draw_bag,
                            add_to_discard(next_discard_pile, card),
                            next_can_draw,
                            next_rage,
                            next_grapple,
                            next_energy,
                            next_hp,
                            next_enemy_hp,
                            next_vulnerable,
                            next_weak,
                            next_block,
                        )
                    )
                else:
                    for target in candidate_targets:
                        candidate_results.append(
                            in_turn_best(
                                turn,
                                replace_one_card(next_hand, target, upgrade_card(target)),
                                next_draw_fixed,
                                next_draw_bag,
                                add_to_discard(next_discard_pile, card),
                                next_can_draw,
                                next_rage,
                                next_grapple,
                                next_energy,
                                next_hp,
                                next_enemy_hp,
                                next_vulnerable,
                                next_weak,
                                next_block,
                            )
                        )
            elif base_card == "Headbutt":
                candidate_targets = tuple(sorted(set(next_discard_pile)))
                if not candidate_targets:
                    candidate_results.append(
                        in_turn_best(
                            turn,
                            next_hand,
                            next_draw_fixed,
                            next_draw_bag,
                            add_to_discard(next_discard_pile, card),
                            next_can_draw,
                            next_rage,
                            next_grapple,
                            next_energy,
                            next_hp,
                            next_enemy_hp,
                            next_vulnerable,
                            next_weak,
                            next_block,
                        )
                    )
                else:
                    for target in candidate_targets:
                        candidate_results.append(
                            in_turn_best(
                                turn,
                                next_hand,
                                (target,) + next_draw_fixed,
                                next_draw_bag,
                                add_to_discard(remove_one_card(next_discard_pile, target), card),
                                next_can_draw,
                                next_rage,
                                next_grapple,
                                next_energy,
                                next_hp,
                                next_enemy_hp,
                                next_vulnerable,
                                next_weak,
                                next_block,
                            )
                        )
            elif base_card in ("Battle Trance", "Pommel Strike", "Shrug It Off"):
                draw_count = 3 if base_card == "Battle Trance" else 1
                draw_states = (
                    (((), next_draw_fixed, next_draw_bag, next_discard_pile, 1.0),)
                    if not next_can_draw
                    else midturn_draw_states(next_draw_fixed, next_draw_bag, next_discard_pile, draw_count)
                )
                final_can_draw = False if base_card == "Battle Trance" else next_can_draw
                outcomes = tuple(
                    (
                        draw_prob,
                        tuple(sorted(next_hand + drawn_cards)),
                        drawn_next_draw_fixed,
                        drawn_next_draw_bag,
                        add_to_discard(drawn_next_discard_pile, card),
                        final_can_draw,
                        next_rage,
                        next_grapple,
                        next_energy,
                        next_hp,
                        next_enemy_hp,
                        next_vulnerable,
                        next_weak,
                        next_block,
                    )
                    for drawn_cards, drawn_next_draw_fixed, drawn_next_draw_bag, drawn_next_discard_pile, draw_prob in draw_states
                    if draw_prob > 0.0
                )
                candidate_results.append(expected_result(outcomes))
            elif base_card == "Second Wind":
                exhausted_cards = tuple(sorted(c for c in next_hand if card_base_name(c) not in ATTACK_CARDS))
                hand_after_exhaust = tuple(sorted(c for c in next_hand if card_base_name(c) in ATTACK_CARDS))
                exhaust_gain = (7 if upgraded else 5) * len(exhausted_cards)
                final_block = next_block + exhaust_gain
                final_enemy_hp = next_enemy_hp
                if exhaust_gain > 0 and grapple > 0:
                    final_enemy_hp -= vuln_damage(grapple, next_vulnerable)
                candidate_results.append(
                    in_turn_best(
                        turn,
                        hand_after_exhaust,
                        next_draw_fixed,
                        next_draw_bag,
                        add_to_discard(next_discard_pile, card),
                        next_can_draw,
                        next_rage,
                        next_grapple,
                        next_energy,
                        next_hp,
                        final_enemy_hp,
                        next_vulnerable,
                        next_weak,
                        final_block,
                    )
                )
            elif base_card == "Burning Pact":
                candidate_targets = tuple(sorted(set(next_hand)))
                for target in candidate_targets:
                    hand_after_exhaust = remove_one_card(next_hand, target)
                    draw_states = (
                        (((), next_draw_fixed, next_draw_bag, next_discard_pile, 1.0),)
                        if not next_can_draw
                        else midturn_draw_states(next_draw_fixed, next_draw_bag, next_discard_pile, 2)
                    )
                    outcomes = tuple(
                        (
                            draw_prob,
                            tuple(sorted(hand_after_exhaust + drawn_cards)),
                            drawn_next_draw_fixed,
                            drawn_next_draw_bag,
                            add_to_discard(drawn_next_discard_pile, card),
                            next_can_draw,
                            next_rage,
                            next_grapple,
                            next_energy,
                            next_hp,
                            next_enemy_hp,
                            next_vulnerable,
                            next_weak,
                            next_block,
                        )
                        for drawn_cards, drawn_next_draw_fixed, drawn_next_draw_bag, drawn_next_discard_pile, draw_prob in draw_states
                        if draw_prob > 0.0
                    )
                    candidate_results.append(expected_result(outcomes))
            else:
                discard_after_play = add_to_discard(next_discard_pile, card)
                if base_card == "Anger":
                    discard_after_play = add_to_discard(discard_after_play, "Anger")
                candidate_results.append(
                    in_turn_best(
                        turn,
                        next_hand,
                        next_draw_fixed,
                        next_draw_bag,
                        discard_after_play,
                        next_can_draw,
                        next_rage,
                        next_grapple,
                        next_energy,
                        next_hp,
                        next_enemy_hp,
                        next_vulnerable,
                        next_weak,
                        next_block,
                    )
                )

            for candidate in candidate_results:
                if candidate[:-1] > best[:-1]:
                    best = candidate

        return best

    @lru_cache(maxsize=None)
    def value(
        turn: int,
        hand: tuple[str, ...],
        draw_fixed: tuple[str, ...],
        draw_bag: tuple[str, ...],
        discard_pile: tuple[str, ...],
        can_draw: bool,
        hp: int,
        cur_enemy_hp: int,
        vulnerable: int,
        weak: int,
    ) -> tuple[float, ...]:
        return in_turn_best(turn, hand, draw_fixed, draw_bag, discard_pile, can_draw, 0, 0, 3, hp, cur_enemy_hp, vulnerable, weak, 0)

    total_out = [0.0] * (max_turn + 1)
    for hand, remaining_draw_bag, prob in draw_outcomes(deck, hand_size):
        result = value(1, hand, (), remaining_draw_bag, (), True, variant.player_hp, variant.enemy_hp, 0, 0)
        for idx in range(max_turn + 1):
            total_out[idx] += prob * result[idx]
    return tuple(total_out)


def deck_turn_report(deck: tuple[str, ...], variant: SearchVariant) -> dict[str, object]:
    deck = tuple(sorted(deck))
    sc = strike_count(deck)
    max_turn = len(variant.enemy_damage_by_turn)
    hand_size = min(variant.draw_per_turn, len(deck))

    @lru_cache(maxsize=None)
    def enumerate_action_states(
        hand: tuple[str, ...],
        draw_fixed: tuple[str, ...],
        draw_bag: tuple[str, ...],
        discard_pile: tuple[str, ...],
        can_draw: bool,
        hp: int,
        cur_enemy_hp: int,
        vulnerable: int,
        weak: int,
    ) -> tuple[ActionState, ...]:
        seen: set[tuple[tuple[str, ...], tuple[str, ...], tuple[str, ...], tuple[str, ...], bool, int, int, int, int, int, int, int]] = set()
        out: list[ActionState] = []

        def rec(
            cur_hand: tuple[str, ...],
            cur_draw_fixed: tuple[str, ...],
            cur_draw_bag: tuple[str, ...],
            cur_discard_pile: tuple[str, ...],
            cur_can_draw: bool,
            cur_rage: int,
            cur_grapple: int,
            cur_energy: int,
            cur_player_hp: int,
            enemy_hp: int,
            cur_vulnerable: int,
            cur_weak: int,
            cur_block: int,
            actions: tuple[str, ...],
        ) -> None:
            state = (
                tuple(sorted(cur_hand)),
                cur_draw_fixed,
                cur_draw_bag,
                cur_discard_pile,
                cur_can_draw,
                cur_rage,
                cur_grapple,
                cur_energy,
                cur_player_hp,
                enemy_hp,
                cur_vulnerable,
                cur_weak,
                cur_block,
            )
            if state in seen:
                return
            seen.add(state)
            out.append(
                ActionState(
                    hand=tuple(sorted(cur_hand)),
                    can_draw=cur_can_draw,
                    rage=cur_rage,
                    grapple=cur_grapple,
                    player_hp=cur_player_hp,
                    enemy_hp=enemy_hp,
                    vulnerable=cur_vulnerable,
                    weak=cur_weak,
                    block=cur_block,
                    draw_fixed=cur_draw_fixed,
                    draw_bag=cur_draw_bag,
                    discard_pile=cur_discard_pile,
                    actions=actions,
                )
            )
            if cur_player_hp <= 0 or enemy_hp <= 0:
                return

            for idx, card in enumerate(cur_hand):
                base_card = card_base_name(card)
                upgraded = card != base_card
                cost = CARD_COST[base_card]
                if cost > cur_energy:
                    continue
                if base_card == "Clash" and not hand_all_attacks(cur_hand):
                    continue
                next_hand = list(cur_hand)
                next_hand.pop(idx)
                if base_card == "Burning Pact" and not next_hand:
                    continue
                next_energy = cur_energy - cost
                next_player_hp = cur_player_hp
                next_enemy_hp = enemy_hp
                next_vulnerable = cur_vulnerable
                next_weak = cur_weak
                next_block = cur_block
                next_draw_fixed = cur_draw_fixed
                next_draw_bag = cur_draw_bag
                next_discard_pile = cur_discard_pile
                next_can_draw = cur_can_draw
                next_rage = cur_rage
                next_grapple = cur_grapple
                block_gained = 0

                if base_card == "Armaments":
                    next_block += 5
                    block_gained += 5
                elif base_card == "Battle Trance":
                    pass
                elif base_card == "Anger":
                    next_enemy_hp -= vuln_damage(6, next_vulnerable)
                elif base_card == "Bloodletting":
                    next_player_hp -= 3
                    next_energy += 2
                elif base_card == "Burning Pact":
                    pass
                elif base_card == "Rage":
                    next_rage += 1
                elif base_card == "Clash":
                    next_enemy_hp -= vuln_damage(14, next_vulnerable)
                elif base_card == "Body Slam":
                    next_enemy_hp -= vuln_damage(next_block, next_vulnerable)
                elif base_card == "Thunderclap":
                    next_enemy_hp -= vuln_damage(4, next_vulnerable)
                    next_vulnerable += 1
                elif base_card == "Strike":
                    next_enemy_hp -= vuln_damage(9 if upgraded else 6, next_vulnerable)
                elif base_card == "Grapple":
                    next_enemy_hp -= vuln_damage(9 if upgraded else 7, next_vulnerable)
                    next_grapple += 7 if upgraded else 5
                elif base_card == "Headbutt":
                    next_enemy_hp -= vuln_damage(9, next_vulnerable)
                elif base_card == "Pommel Strike":
                    next_enemy_hp -= vuln_damage(9, next_vulnerable)
                elif base_card == "Shrug It Off":
                    next_block += 8
                    block_gained += 8
                elif base_card == "Second Wind":
                    pass
                elif base_card == "Sword Boomerang":
                    next_enemy_hp -= vuln_damage(3, next_vulnerable)
                    next_enemy_hp -= vuln_damage(3, next_vulnerable)
                    next_enemy_hp -= vuln_damage(3, next_vulnerable)
                elif base_card == "Twin Strike":
                    next_enemy_hp -= vuln_damage(5, next_vulnerable)
                    next_enemy_hp -= vuln_damage(5, next_vulnerable)
                elif base_card == "Hemokinesis":
                    next_player_hp -= 2
                    next_enemy_hp -= vuln_damage(19 if upgraded else 14, next_vulnerable)
                elif base_card == "Bludgeon":
                    next_enemy_hp -= vuln_damage(32, next_vulnerable)
                elif base_card == "Perfected Strike":
                    next_enemy_hp -= vuln_damage(6 + 2 * sc, next_vulnerable)
                elif base_card == "Uppercut":
                    next_enemy_hp -= vuln_damage(13, next_vulnerable)
                    next_vulnerable += 1
                elif base_card == "Bash":
                    next_enemy_hp -= vuln_damage(10 if upgraded else 8, next_vulnerable)
                    next_vulnerable += 3 if upgraded else 2
                elif base_card == "Clothesline":
                    next_enemy_hp -= vuln_damage(12, next_vulnerable)
                    next_weak += 2
                elif base_card == "Iron Wave":
                    next_block += 5
                    block_gained += 5
                    next_enemy_hp -= vuln_damage(5, next_vulnerable)
                elif base_card == "Defend":
                    gain = 8 if upgraded else 5
                    next_block += gain
                    block_gained += gain

                if block_gained > 0 and cur_grapple > 0:
                    next_enemy_hp -= vuln_damage(cur_grapple, next_vulnerable)

                if base_card in ATTACK_CARDS:
                    next_block += 3 * cur_rage

                if base_card == "Armaments":
                    candidate_targets = tuple(sorted({target for target in next_hand if upgrade_card(target) != target}))
                    if not candidate_targets:
                        rec(
                            tuple(next_hand),
                            next_draw_fixed,
                            next_draw_bag,
                            add_to_discard(next_discard_pile, card),
                            next_can_draw,
                            next_rage,
                            next_grapple,
                            next_energy,
                            next_player_hp,
                            next_enemy_hp,
                            next_vulnerable,
                            next_weak,
                            next_block,
                            actions + (format_action_label(card),),
                        )
                        continue
                    for target in candidate_targets:
                        rec(
                            replace_one_card(tuple(next_hand), target, upgrade_card(target)),
                            next_draw_fixed,
                            next_draw_bag,
                            add_to_discard(next_discard_pile, card),
                            next_can_draw,
                            next_rage,
                            next_grapple,
                            next_energy,
                            next_player_hp,
                            next_enemy_hp,
                            next_vulnerable,
                            next_weak,
                            next_block,
                            actions + (f"{card}[upgrade {target}]",),
                        )
                elif base_card == "Headbutt":
                    candidate_targets = tuple(sorted(set(next_discard_pile)))
                    if not candidate_targets:
                        rec(
                            tuple(next_hand),
                            next_draw_fixed,
                            next_draw_bag,
                            add_to_discard(next_discard_pile, card),
                            next_can_draw,
                            next_rage,
                            next_grapple,
                            next_energy,
                            next_player_hp,
                            next_enemy_hp,
                            next_vulnerable,
                            next_weak,
                            next_block,
                            actions + (format_action_label(card),),
                        )
                        continue
                    for target in candidate_targets:
                        rec(
                            tuple(next_hand),
                            (target,) + next_draw_fixed,
                            next_draw_bag,
                            add_to_discard(remove_one_card(next_discard_pile, target), card),
                            next_can_draw,
                            next_rage,
                            next_grapple,
                            next_energy,
                            next_player_hp,
                            next_enemy_hp,
                            next_vulnerable,
                            next_weak,
                            next_block,
                            actions + (f"{card}[top {target}]",),
                        )
                elif base_card in ("Battle Trance", "Pommel Strike", "Shrug It Off"):
                    draw_count = 3 if base_card == "Battle Trance" else 1
                    draw_states = (
                        (((), next_draw_fixed, next_draw_bag, next_discard_pile, 1.0),)
                        if not next_can_draw
                        else midturn_draw_states(next_draw_fixed, next_draw_bag, next_discard_pile, draw_count)
                    )
                    final_can_draw = False if base_card == "Battle Trance" else next_can_draw
                    for drawn_cards, drawn_next_draw_fixed, drawn_next_draw_bag, drawn_next_discard_pile, draw_prob in draw_states:
                        if draw_prob == 0.0:
                            continue
                        rec(
                            tuple(sorted(tuple(next_hand) + drawn_cards)),
                            drawn_next_draw_fixed,
                            drawn_next_draw_bag,
                            add_to_discard(drawn_next_discard_pile, card),
                            final_can_draw,
                            next_rage,
                            next_grapple,
                            next_energy,
                            next_player_hp,
                            next_enemy_hp,
                            next_vulnerable,
                                next_weak,
                                next_block,
                                actions + (format_action_label(card, drawn_cards),),
                            )
                elif base_card == "Second Wind":
                    exhausted_cards = tuple(sorted(card for card in next_hand if card_base_name(card) not in ATTACK_CARDS))
                    hand_after_exhaust = tuple(sorted(card for card in next_hand if card_base_name(card) in ATTACK_CARDS))
                    exhaust_gain = (7 if upgraded else 5) * len(exhausted_cards)
                    final_block = next_block + exhaust_gain
                    final_enemy_hp = next_enemy_hp
                    if exhaust_gain > 0 and cur_grapple > 0:
                        final_enemy_hp -= vuln_damage(cur_grapple, next_vulnerable)
                    rec(
                        hand_after_exhaust,
                        next_draw_fixed,
                        next_draw_bag,
                        add_to_discard(next_discard_pile, card),
                        next_can_draw,
                        next_rage,
                        next_grapple,
                        next_energy,
                        next_player_hp,
                        final_enemy_hp,
                        next_vulnerable,
                        next_weak,
                        final_block,
                        actions + (format_second_wind_label(exhausted_cards),),
                    )
                elif base_card == "Burning Pact":
                    candidate_targets = tuple(sorted(set(next_hand)))
                    for target in candidate_targets:
                        hand_after_exhaust = remove_one_card(tuple(next_hand), target)
                        draw_states = (
                            (((), next_draw_fixed, next_draw_bag, next_discard_pile, 1.0),)
                            if not next_can_draw
                            else midturn_draw_states(next_draw_fixed, next_draw_bag, next_discard_pile, 2)
                        )
                        for drawn_cards, drawn_next_draw_fixed, drawn_next_draw_bag, drawn_next_discard_pile, draw_prob in draw_states:
                            if draw_prob == 0.0:
                                continue
                            rec(
                                tuple(sorted(hand_after_exhaust + drawn_cards)),
                                drawn_next_draw_fixed,
                                drawn_next_draw_bag,
                                add_to_discard(drawn_next_discard_pile, card),
                                next_can_draw,
                                next_rage,
                                next_grapple,
                                next_energy,
                                next_player_hp,
                                next_enemy_hp,
                                next_vulnerable,
                                next_weak,
                                next_block,
                                actions + (format_burning_pact_label(target, drawn_cards),),
                            )
                else:
                    discard_after_play = add_to_discard(next_discard_pile, card)
                    if base_card == "Anger":
                        discard_after_play = add_to_discard(discard_after_play, "Anger")
                    rec(
                        tuple(next_hand),
                        next_draw_fixed,
                        next_draw_bag,
                        discard_after_play,
                        next_can_draw,
                        next_rage,
                        next_grapple,
                        next_energy,
                        next_player_hp,
                        next_enemy_hp,
                        next_vulnerable,
                        next_weak,
                        next_block,
                        actions + (format_action_label(card),),
                    )

        rec(hand, draw_fixed, draw_bag, discard_pile, can_draw, 0, 0, 3, hp, cur_enemy_hp, vulnerable, weak, 0, ())
        return tuple(out)

    @lru_cache(maxsize=None)
    def end_turn_next_states(
        hand: tuple[str, ...],
        draw_fixed: tuple[str, ...],
        draw_bag: tuple[str, ...],
        discard_pile: tuple[str, ...],
    ) -> tuple[tuple[tuple[str, ...], tuple[str, ...], tuple[str, ...], tuple[str, ...], float], ...]:
        discard_after_turn = tuple(sorted(discard_pile + hand))
        return next_turn_draw_states(draw_fixed, draw_bag, discard_after_turn, hand_size)

    @lru_cache(maxsize=None)
    def value(
        turn: int,
        hand: tuple[str, ...],
        draw_fixed: tuple[str, ...],
        draw_bag: tuple[str, ...],
        discard_pile: tuple[str, ...],
        can_draw: bool,
        hp: int,
        cur_enemy_hp: int,
        vulnerable: int,
        weak: int,
    ) -> tuple[tuple[float, ...], ActionState]:
        best: tuple[float, ...] | None = None
        best_action: ActionState | None = None

        for action_state in enumerate_action_states(hand, draw_fixed, draw_bag, discard_pile, can_draw, hp, cur_enemy_hp, vulnerable, weak):
            next_enemy_hp = action_state.enemy_hp
            next_vulnerable = action_state.vulnerable
            next_weak = action_state.weak
            block = action_state.block

            if action_state.player_hp <= 0:
                vec = [0.0] * (max_turn + 1)
                vec[-1] = 1.0
                candidate = tuple(vec)
            elif next_enemy_hp <= 0:
                vec = [0.0] * (max_turn + 1)
                vec[turn - 1] = 1.0
                candidate = tuple(vec)
            else:
                enemy_damage = weak_damage(variant.enemy_damage_by_turn[turn - 1], next_weak)
                hp_after = action_state.player_hp - max(0, enemy_damage - block)
                if turn >= max_turn or hp_after <= 0:
                    vec = [0.0] * (max_turn + 1)
                    vec[-1] = 1.0
                    candidate = tuple(vec)
                else:
                    merged = [0.0] * (max_turn + 1)
                    for next_hand, next_draw_fixed, next_draw_bag, next_discard_pile, prob in end_turn_next_states(
                        action_state.hand,
                        action_state.draw_fixed,
                        action_state.draw_bag,
                        action_state.discard_pile,
                    ):
                        future, _ = value(
                            turn + 1,
                            next_hand,
                            next_draw_fixed,
                            next_draw_bag,
                            next_discard_pile,
                            True,
                            hp_after,
                            next_enemy_hp,
                            max(0, next_vulnerable - 1),
                            max(0, next_weak - 1),
                        )
                        for idx in range(max_turn + 1):
                            merged[idx] += prob * future[idx]
                    candidate = tuple(merged)

            if best is None or candidate[:-1] > best[:-1]:
                best = candidate
                best_action = action_state

        assert best is not None
        assert best_action is not None
        return best, best_action

    frontier = [
        (1, hand, (), remaining_draw_bag, (), True, variant.player_hp, variant.enemy_hp, 0, 0, prob)
        for hand, remaining_draw_bag, prob in draw_outcomes(deck, hand_size)
    ]
    exact_result = exact_multiturn_result(deck, variant)
    turn_rows: list[dict[str, object]] = []
    cumulative_kill = 0.0

    for turn in range(1, max_turn + 1):
        if not frontier:
            break

        start_groups: dict[tuple[int, int, int, int], float] = {}
        chosen_action_groups: dict[tuple[tuple[int, int, int, int], tuple[str, ...], int, int], float] = {}
        next_groups: dict[tuple[int, int, int, int], float] = {}
        turn_kill = 0.0
        turn_fail = 0.0
        next_frontier: list[tuple[int, tuple[str, ...], tuple[str, ...], tuple[str, ...], tuple[str, ...], bool, int, int, int, int, float]] = []

        for _, hand, draw_fixed, draw_bag, discard_pile, can_draw, hp, enemy_hp, vulnerable, weak, prob in frontier:
            start_key = (hp, enemy_hp, vulnerable, weak)
            start_groups[start_key] = start_groups.get(start_key, 0.0) + prob

            future_vec, best_action = value(turn, hand, draw_fixed, draw_bag, discard_pile, can_draw, hp, enemy_hp, vulnerable, weak)
            action_key = (
                start_key,
                best_action.actions,
                best_action.player_hp,
                best_action.enemy_hp,
                best_action.block,
            )
            chosen_action_groups[action_key] = chosen_action_groups.get(action_key, 0.0) + prob

            if best_action.player_hp <= 0:
                turn_fail += prob
                continue

            if best_action.enemy_hp <= 0:
                turn_kill += prob
                continue

            enemy_damage = weak_damage(variant.enemy_damage_by_turn[turn - 1], best_action.weak)
            hp_after = best_action.player_hp - max(0, enemy_damage - best_action.block)
            if turn >= max_turn or hp_after <= 0:
                turn_fail += prob
                continue

            post_vulnerable = max(0, best_action.vulnerable - 1)
            post_weak = max(0, best_action.weak - 1)
            for next_hand, next_draw_fixed, next_draw_bag, next_discard_pile, next_prob in end_turn_next_states(
                best_action.hand,
                best_action.draw_fixed,
                best_action.draw_bag,
                best_action.discard_pile,
            ):
                merged_prob = prob * next_prob
                next_key = (hp_after, best_action.enemy_hp, post_vulnerable, post_weak)
                next_groups[next_key] = next_groups.get(next_key, 0.0) + merged_prob
                next_frontier.append(
                    (
                        turn + 1,
                        next_hand,
                        next_draw_fixed,
                        next_draw_bag,
                        next_discard_pile,
                        True,
                        hp_after,
                        best_action.enemy_hp,
                        post_vulnerable,
                        post_weak,
                        merged_prob,
                    )
                )

        cumulative_kill += turn_kill
        turn_rows.append(
            {
                "turn": turn,
                "start_mass": sum(start_groups.values()),
                "exact_kill_prob": turn_kill,
                "cumulative_kill_prob": cumulative_kill,
                "fail_prob_after_action": turn_fail,
                "start_state_groups": [
                    {
                        "player_hp": key[0],
                        "enemy_hp": key[1],
                        "vulnerable": key[2],
                        "weak": key[3],
                        "prob": value,
                    }
                    for key, value in sorted(start_groups.items(), key=lambda item: (-item[1], item[0]))
                ],
                "chosen_action_groups": [
                    {
                        "start_state": {
                            "player_hp": key[0][0],
                            "enemy_hp": key[0][1],
                            "vulnerable": key[0][2],
                            "weak": key[0][3],
                        },
                        "actions": list(key[1]),
                        "end_player_hp_before_enemy": key[2],
                        "end_enemy_hp": key[3],
                        "block": key[4],
                        "prob": value,
                    }
                    for key, value in sorted(chosen_action_groups.items(), key=lambda item: (-item[1], item[0][0], item[0][1]))
                ],
                "next_state_groups": [
                    {
                        "player_hp": key[0],
                        "enemy_hp": key[1],
                        "vulnerable": key[2],
                        "weak": key[3],
                        "prob": value,
                    }
                    for key, value in sorted(next_groups.items(), key=lambda item: (-item[1], item[0]))
                ],
            }
        )
        frontier = next_frontier

    return {
        "variant": variant.name,
        "deck": list(deck),
        "deck_display": deck_display(deck),
        "exact_result": list(exact_result),
        "expected_kill_turn_if_success": expected_kill_turn(exact_result),
        "turn_rows": turn_rows,
    }


def deck_turn_report_precise(deck: tuple[str, ...], variant: SearchVariant) -> dict[str, object]:
    deck = tuple(sorted(deck))
    sc = strike_count(deck)
    max_turn = len(variant.enemy_damage_by_turn)
    hand_size = min(variant.draw_per_turn, len(deck))

    def serialize_leaf(
        hand: tuple[str, ...],
        can_draw: bool,
        rage: int,
        grapple: int,
        player_hp: int,
        enemy_hp: int,
        vulnerable: int,
        weak: int,
        block: int,
        draw_fixed: tuple[str, ...],
        draw_bag: tuple[str, ...],
        discard_pile: tuple[str, ...],
        actions: tuple[str, ...],
    ) -> tuple[tuple[str, ...], bool, int, int, int, int, int, int, int, tuple[str, ...], tuple[str, ...], tuple[str, ...], tuple[str, ...]]:
        return (
            hand,
            can_draw,
            rage,
            grapple,
            player_hp,
            enemy_hp,
            vulnerable,
            weak,
            block,
            draw_fixed,
            draw_bag,
            discard_pile,
            actions,
        )

    def deserialize_leaf(
        raw: tuple[tuple[str, ...], bool, int, int, int, int, int, int, int, tuple[str, ...], tuple[str, ...], tuple[str, ...], tuple[str, ...]],
    ) -> ActionState:
        hand, can_draw, rage, grapple, player_hp, enemy_hp, vulnerable, weak, block, draw_fixed, draw_bag, discard_pile, actions = raw
        return ActionState(
            hand=hand,
            can_draw=can_draw,
            rage=rage,
            grapple=grapple,
            player_hp=player_hp,
            enemy_hp=enemy_hp,
            vulnerable=vulnerable,
            weak=weak,
            block=block,
            draw_fixed=draw_fixed,
            draw_bag=draw_bag,
            discard_pile=discard_pile,
            actions=actions,
        )

    @lru_cache(maxsize=None)
    def end_turn_next_states(
        hand: tuple[str, ...],
        draw_fixed: tuple[str, ...],
        draw_bag: tuple[str, ...],
        discard_pile: tuple[str, ...],
    ) -> tuple[tuple[tuple[str, ...], tuple[str, ...], tuple[str, ...], tuple[str, ...], float], ...]:
        discard_after_turn = tuple(sorted(discard_pile + hand))
        return next_turn_draw_states(draw_fixed, draw_bag, discard_after_turn, hand_size)

    @lru_cache(maxsize=None)
    def resolve_turn_end(
        turn: int,
        hand: tuple[str, ...],
        draw_fixed: tuple[str, ...],
        draw_bag: tuple[str, ...],
        discard_pile: tuple[str, ...],
        can_draw: bool,
        hp: int,
        cur_enemy_hp: int,
        vulnerable: int,
        weak: int,
        block: int,
    ) -> tuple[float, ...]:
        vec = [0.0] * (max_turn + 1)
        if hp <= 0:
            vec[-1] = 1.0
            return tuple(vec)
        if cur_enemy_hp <= 0:
            vec[turn - 1] = 1.0
            return tuple(vec)

        enemy_damage = weak_damage(variant.enemy_damage_by_turn[turn - 1], weak)
        hp_after = hp - max(0, enemy_damage - block)
        if turn >= max_turn or hp_after <= 0:
            vec[-1] = 1.0
            return tuple(vec)

        merged = [0.0] * (max_turn + 1)
        post_vulnerable = max(0, vulnerable - 1)
        post_weak = max(0, weak - 1)
        for next_hand, next_draw_fixed, next_draw_bag, next_discard_pile, prob in end_turn_next_states(hand, draw_fixed, draw_bag, discard_pile):
            future, _ = value(
                turn + 1,
                next_hand,
                next_draw_fixed,
                next_draw_bag,
                next_discard_pile,
                True,
                hp_after,
                cur_enemy_hp,
                post_vulnerable,
                post_weak,
            )
            for idx in range(max_turn + 1):
                merged[idx] += prob * future[idx]
        return tuple(merged)

    @lru_cache(maxsize=None)
    def action_options(
        hand: tuple[str, ...],
        draw_fixed: tuple[str, ...],
        draw_bag: tuple[str, ...],
        discard_pile: tuple[str, ...],
        can_draw: bool,
        rage: int,
        grapple: int,
        energy: int,
        hp: int,
        cur_enemy_hp: int,
        vulnerable: int,
        weak: int,
        block: int,
    ) -> tuple[
        tuple[
            tuple[
                float,
                str,
                tuple[str, ...],
                tuple[str, ...],
                tuple[str, ...],
                tuple[str, ...],
                bool,
                int,
                int,
                int,
                int,
                int,
                int,
                int,
                int,
            ],
            ...,
        ],
        ...,
    ]:
        options = []
        for idx, card in enumerate(hand):
            base_card = card_base_name(card)
            upgraded = card != base_card
            cost = CARD_COST[base_card]
            if cost > energy:
                continue
            if base_card == "Clash" and not hand_all_attacks(hand):
                continue

            next_hand_list = list(hand)
            next_hand_list.pop(idx)
            if base_card == "Burning Pact" and not next_hand_list:
                continue

            next_energy = energy - cost
            next_hp = hp
            next_enemy_hp = cur_enemy_hp
            next_vulnerable = vulnerable
            next_weak = weak
            next_block = block
            next_draw_fixed = draw_fixed
            next_draw_bag = draw_bag
            next_discard_pile = discard_pile
            next_can_draw = can_draw
            next_rage = rage
            next_grapple = grapple
            block_gained = 0

            if base_card == "Armaments":
                next_block += 5
                block_gained += 5
            elif base_card == "Battle Trance":
                pass
            elif base_card == "Anger":
                next_enemy_hp -= vuln_damage(6, next_vulnerable)
            elif base_card == "Bloodletting":
                next_hp -= 3
                next_energy += 2
            elif base_card == "Burning Pact":
                pass
            elif base_card == "Rage":
                next_rage += 1
            elif base_card == "Clash":
                next_enemy_hp -= vuln_damage(14, next_vulnerable)
            elif base_card == "Body Slam":
                next_enemy_hp -= vuln_damage(next_block, next_vulnerable)
            elif base_card == "Thunderclap":
                next_enemy_hp -= vuln_damage(4, next_vulnerable)
                next_vulnerable += 1
            elif base_card == "Strike":
                next_enemy_hp -= vuln_damage(9 if upgraded else 6, next_vulnerable)
            elif base_card == "Grapple":
                next_enemy_hp -= vuln_damage(9 if upgraded else 7, next_vulnerable)
                next_grapple += 7 if upgraded else 5
            elif base_card == "Headbutt":
                next_enemy_hp -= vuln_damage(9, next_vulnerable)
            elif base_card == "Pommel Strike":
                next_enemy_hp -= vuln_damage(9, next_vulnerable)
            elif base_card == "Shrug It Off":
                next_block += 8
                block_gained += 8
            elif base_card == "Second Wind":
                pass
            elif base_card == "Sword Boomerang":
                next_enemy_hp -= vuln_damage(3, next_vulnerable)
                next_enemy_hp -= vuln_damage(3, next_vulnerable)
                next_enemy_hp -= vuln_damage(3, next_vulnerable)
            elif base_card == "Twin Strike":
                next_enemy_hp -= vuln_damage(5, next_vulnerable)
                next_enemy_hp -= vuln_damage(5, next_vulnerable)
            elif base_card == "Hemokinesis":
                next_hp -= 2
                next_enemy_hp -= vuln_damage(19 if upgraded else 14, next_vulnerable)
            elif base_card == "Bludgeon":
                next_enemy_hp -= vuln_damage(32, next_vulnerable)
            elif base_card == "Perfected Strike":
                next_enemy_hp -= vuln_damage(6 + 2 * sc, next_vulnerable)
            elif base_card == "Uppercut":
                next_enemy_hp -= vuln_damage(13, next_vulnerable)
                next_vulnerable += 1
            elif base_card == "Bash":
                next_enemy_hp -= vuln_damage(10 if upgraded else 8, next_vulnerable)
                next_vulnerable += 3 if upgraded else 2
            elif base_card == "Clothesline":
                next_enemy_hp -= vuln_damage(12, next_vulnerable)
                next_weak += 2
            elif base_card == "Iron Wave":
                next_block += 5
                block_gained += 5
                next_enemy_hp -= vuln_damage(5, next_vulnerable)
            elif base_card == "Defend":
                gain = 8 if upgraded else 5
                next_block += gain
                block_gained += gain

            if block_gained > 0 and grapple > 0:
                next_enemy_hp -= vuln_damage(grapple, next_vulnerable)
            if base_card in ATTACK_CARDS:
                next_block += 3 * rage

            next_hand = tuple(next_hand_list)

            if base_card == "Armaments":
                candidate_targets = tuple(sorted({target for target in next_hand if upgrade_card(target) != target}))
                if not candidate_targets:
                    options.append(((
                        1.0,
                        format_action_label(card),
                        next_hand,
                        next_draw_fixed,
                        next_draw_bag,
                        add_to_discard(next_discard_pile, card),
                        next_can_draw,
                        next_rage,
                        next_grapple,
                        next_energy,
                        next_hp,
                        next_enemy_hp,
                        next_vulnerable,
                        next_weak,
                        next_block,
                    ),))
                else:
                    for target in candidate_targets:
                        options.append(((
                            1.0,
                            f"{card}[upgrade {target}]",
                            replace_one_card(next_hand, target, upgrade_card(target)),
                            next_draw_fixed,
                            next_draw_bag,
                            add_to_discard(next_discard_pile, card),
                            next_can_draw,
                            next_rage,
                            next_grapple,
                            next_energy,
                            next_hp,
                            next_enemy_hp,
                            next_vulnerable,
                            next_weak,
                            next_block,
                        ),))
            elif base_card == "Headbutt":
                candidate_targets = tuple(sorted(set(next_discard_pile)))
                if not candidate_targets:
                    options.append(((
                        1.0,
                        format_action_label(card),
                        next_hand,
                        next_draw_fixed,
                        next_draw_bag,
                        add_to_discard(next_discard_pile, card),
                        next_can_draw,
                        next_rage,
                        next_grapple,
                        next_energy,
                        next_hp,
                        next_enemy_hp,
                        next_vulnerable,
                        next_weak,
                        next_block,
                    ),))
                else:
                    for target in candidate_targets:
                        options.append(((
                            1.0,
                            f"{card}[top {target}]",
                            next_hand,
                            (target,) + next_draw_fixed,
                            next_draw_bag,
                            add_to_discard(remove_one_card(next_discard_pile, target), card),
                            next_can_draw,
                            next_rage,
                            next_grapple,
                            next_energy,
                            next_hp,
                            next_enemy_hp,
                            next_vulnerable,
                            next_weak,
                            next_block,
                        ),))
            elif base_card in ("Battle Trance", "Pommel Strike", "Shrug It Off"):
                draw_count = 3 if base_card == "Battle Trance" else 1
                draw_states = (
                    (((), next_draw_fixed, next_draw_bag, next_discard_pile, 1.0),)
                    if not next_can_draw
                    else midturn_draw_states(next_draw_fixed, next_draw_bag, next_discard_pile, draw_count)
                )
                final_can_draw = False if base_card == "Battle Trance" else next_can_draw
                options.append(
                    tuple(
                        (
                            draw_prob,
                            format_action_label(card, drawn_cards),
                            tuple(sorted(next_hand + drawn_cards)),
                            drawn_next_draw_fixed,
                            drawn_next_draw_bag,
                            add_to_discard(drawn_next_discard_pile, card),
                            final_can_draw,
                            next_rage,
                            next_grapple,
                            next_energy,
                            next_hp,
                            next_enemy_hp,
                            next_vulnerable,
                            next_weak,
                            next_block,
                        )
                        for drawn_cards, drawn_next_draw_fixed, drawn_next_draw_bag, drawn_next_discard_pile, draw_prob in draw_states
                        if draw_prob > 0.0
                    )
                )
            elif base_card == "Second Wind":
                exhausted_cards = tuple(sorted(c for c in next_hand if card_base_name(c) not in ATTACK_CARDS))
                hand_after_exhaust = tuple(sorted(c for c in next_hand if card_base_name(c) in ATTACK_CARDS))
                exhaust_gain = (7 if upgraded else 5) * len(exhausted_cards)
                final_block = next_block + exhaust_gain
                final_enemy_hp = next_enemy_hp
                if exhaust_gain > 0 and grapple > 0:
                    final_enemy_hp -= vuln_damage(grapple, next_vulnerable)
                options.append(((
                    1.0,
                    format_second_wind_label(exhausted_cards),
                    hand_after_exhaust,
                    next_draw_fixed,
                    next_draw_bag,
                    add_to_discard(next_discard_pile, card),
                    next_can_draw,
                    next_rage,
                    next_grapple,
                    next_energy,
                    next_hp,
                    final_enemy_hp,
                    next_vulnerable,
                    next_weak,
                    final_block,
                ),))
            elif base_card == "Burning Pact":
                candidate_targets = tuple(sorted(set(next_hand)))
                for target in candidate_targets:
                    hand_after_exhaust = remove_one_card(next_hand, target)
                    draw_states = (
                        (((), next_draw_fixed, next_draw_bag, next_discard_pile, 1.0),)
                        if not next_can_draw
                        else midturn_draw_states(next_draw_fixed, next_draw_bag, next_discard_pile, 2)
                    )
                    options.append(
                        tuple(
                            (
                                draw_prob,
                                format_burning_pact_label(target, drawn_cards),
                                tuple(sorted(hand_after_exhaust + drawn_cards)),
                                drawn_next_draw_fixed,
                                drawn_next_draw_bag,
                                add_to_discard(drawn_next_discard_pile, card),
                                next_can_draw,
                                next_rage,
                                next_grapple,
                                next_energy,
                                next_hp,
                                next_enemy_hp,
                                next_vulnerable,
                                next_weak,
                                next_block,
                            )
                            for drawn_cards, drawn_next_draw_fixed, drawn_next_draw_bag, drawn_next_discard_pile, draw_prob in draw_states
                            if draw_prob > 0.0
                        )
                    )
            else:
                discard_after_play = add_to_discard(next_discard_pile, card)
                if base_card == "Anger":
                    discard_after_play = add_to_discard(discard_after_play, "Anger")
                options.append(((
                    1.0,
                    format_action_label(card),
                    next_hand,
                    next_draw_fixed,
                    next_draw_bag,
                    discard_after_play,
                    next_can_draw,
                    next_rage,
                    next_grapple,
                    next_energy,
                    next_hp,
                    next_enemy_hp,
                    next_vulnerable,
                    next_weak,
                    next_block,
                ),))
        return tuple(options)

    @lru_cache(maxsize=None)
    def in_turn_best(
        turn: int,
        hand: tuple[str, ...],
        draw_fixed: tuple[str, ...],
        draw_bag: tuple[str, ...],
        discard_pile: tuple[str, ...],
        can_draw: bool,
        rage: int,
        grapple: int,
        energy: int,
        hp: int,
        cur_enemy_hp: int,
        vulnerable: int,
        weak: int,
        block: int,
    ) -> tuple[tuple[float, ...], tuple[str, ...], tuple[tuple[float, tuple[tuple[str, ...], bool, int, int, int, int, int, int, int, tuple[str, ...], tuple[str, ...], tuple[str, ...], tuple[str, ...]]], ...]]:
        best_value = resolve_turn_end(turn, hand, draw_fixed, draw_bag, discard_pile, can_draw, hp, cur_enemy_hp, vulnerable, weak, block)
        best_actions: tuple[str, ...] = ()
        best_distribution = ((1.0, serialize_leaf(hand, can_draw, rage, grapple, hp, cur_enemy_hp, vulnerable, weak, block, draw_fixed, draw_bag, discard_pile, ())),)

        for option in action_options(hand, draw_fixed, draw_bag, discard_pile, can_draw, rage, grapple, energy, hp, cur_enemy_hp, vulnerable, weak, block):
            merged = [0.0] * (max_turn + 1)
            dist_map: dict[tuple[tuple[str, ...], bool, int, int, int, int, int, int, int, tuple[str, ...], tuple[str, ...], tuple[str, ...], tuple[str, ...]], float] = {}
            first_action: tuple[str, ...] = ()
            for prob, label, next_hand, next_draw_fixed, next_draw_bag, next_discard_pile, next_can_draw, next_rage, next_grapple, next_energy, next_hp, next_enemy_hp, next_vulnerable, next_weak, next_block in option:
                future_value, future_actions, future_dist = in_turn_best(
                    turn,
                    next_hand,
                    next_draw_fixed,
                    next_draw_bag,
                    next_discard_pile,
                    next_can_draw,
                    next_rage,
                    next_grapple,
                    next_energy,
                    next_hp,
                    next_enemy_hp,
                    next_vulnerable,
                    next_weak,
                    next_block,
                )
                first_action = (label,)
                for idx in range(max_turn + 1):
                    merged[idx] += prob * future_value[idx]
                for subprob, raw_leaf in future_dist:
                    leaf = list(raw_leaf)
                    leaf[-1] = (label,) + tuple(leaf[-1])
                    key = tuple(leaf)
                    dist_map[key] = dist_map.get(key, 0.0) + prob * subprob
            candidate = tuple(merged)
            if candidate[:-1] > best_value[:-1]:
                best_value = candidate
                best_actions = first_action
                best_distribution = tuple((prob, key) for key, prob in sorted(dist_map.items(), key=lambda item: (-item[1], item[0])))
        return best_value, best_actions, best_distribution

    @lru_cache(maxsize=None)
    def value(
        turn: int,
        hand: tuple[str, ...],
        draw_fixed: tuple[str, ...],
        draw_bag: tuple[str, ...],
        discard_pile: tuple[str, ...],
        can_draw: bool,
        hp: int,
        cur_enemy_hp: int,
        vulnerable: int,
        weak: int,
    ) -> tuple[tuple[float, ...], ActionState]:
        exact_value, best_actions, best_distribution = in_turn_best(turn, hand, draw_fixed, draw_bag, discard_pile, can_draw, 0, 0, 3, hp, cur_enemy_hp, vulnerable, weak, 0)
        representative = deserialize_leaf(best_distribution[0][1])
        representative = ActionState(
            hand=representative.hand,
            can_draw=representative.can_draw,
            rage=representative.rage,
            grapple=representative.grapple,
            player_hp=representative.player_hp,
            enemy_hp=representative.enemy_hp,
            vulnerable=representative.vulnerable,
            weak=representative.weak,
            block=representative.block,
            draw_fixed=representative.draw_fixed,
            draw_bag=representative.draw_bag,
            discard_pile=representative.discard_pile,
            actions=best_distribution[0][1][-1],
        )
        return exact_value, representative

    frontier = [
        (1, hand, (), remaining_draw_bag, (), True, variant.player_hp, variant.enemy_hp, 0, 0, prob)
        for hand, remaining_draw_bag, prob in draw_outcomes(deck, hand_size)
    ]
    exact_result = exact_multiturn_result(deck, variant)
    turn_rows: list[dict[str, object]] = []
    cumulative_kill = 0.0

    for turn in range(1, max_turn + 1):
        if not frontier:
            break

        start_groups: dict[tuple[int, int, int, int], float] = {}
        chosen_action_groups: dict[tuple[tuple[int, int, int, int], tuple[str, ...], int, int, int], float] = {}
        next_groups: dict[tuple[int, int, int, int], float] = {}
        turn_kill = 0.0
        turn_fail = 0.0
        next_frontier: list[tuple[int, tuple[str, ...], tuple[str, ...], tuple[str, ...], tuple[str, ...], bool, int, int, int, int, float]] = []

        for _, hand, draw_fixed, draw_bag, discard_pile, can_draw, hp, enemy_hp, vulnerable, weak, prob in frontier:
            start_key = (hp, enemy_hp, vulnerable, weak)
            start_groups[start_key] = start_groups.get(start_key, 0.0) + prob

            _, _, leafs = in_turn_best(turn, hand, draw_fixed, draw_bag, discard_pile, can_draw, 0, 0, 3, hp, enemy_hp, vulnerable, weak, 0)
            for leaf_prob, raw_leaf in leafs:
                action_state = deserialize_leaf(raw_leaf)
                merged_prob = prob * leaf_prob
                action_key = (
                    start_key,
                    action_state.actions,
                    action_state.player_hp,
                    action_state.enemy_hp,
                    action_state.block,
                )
                chosen_action_groups[action_key] = chosen_action_groups.get(action_key, 0.0) + merged_prob

                if action_state.player_hp <= 0:
                    turn_fail += merged_prob
                    continue
                if action_state.enemy_hp <= 0:
                    turn_kill += merged_prob
                    continue

                enemy_damage = weak_damage(variant.enemy_damage_by_turn[turn - 1], action_state.weak)
                hp_after = action_state.player_hp - max(0, enemy_damage - action_state.block)
                if turn >= max_turn or hp_after <= 0:
                    turn_fail += merged_prob
                    continue

                post_vulnerable = max(0, action_state.vulnerable - 1)
                post_weak = max(0, action_state.weak - 1)
                for next_hand, next_draw_fixed, next_draw_bag, next_discard_pile, next_prob in end_turn_next_states(
                    action_state.hand,
                    action_state.draw_fixed,
                    action_state.draw_bag,
                    action_state.discard_pile,
                ):
                    final_prob = merged_prob * next_prob
                    next_key = (hp_after, action_state.enemy_hp, post_vulnerable, post_weak)
                    next_groups[next_key] = next_groups.get(next_key, 0.0) + final_prob
                    next_frontier.append(
                        (
                            turn + 1,
                            next_hand,
                            next_draw_fixed,
                            next_draw_bag,
                            next_discard_pile,
                            True,
                            hp_after,
                            action_state.enemy_hp,
                            post_vulnerable,
                            post_weak,
                            final_prob,
                        )
                    )

        cumulative_kill += turn_kill
        turn_rows.append(
            {
                "turn": turn,
                "start_mass": sum(start_groups.values()),
                "exact_kill_prob": turn_kill,
                "cumulative_kill_prob": cumulative_kill,
                "fail_prob_after_action": turn_fail,
                "start_state_groups": [
                    {
                        "player_hp": key[0],
                        "enemy_hp": key[1],
                        "vulnerable": key[2],
                        "weak": key[3],
                        "prob": value,
                    }
                    for key, value in sorted(start_groups.items(), key=lambda item: (-item[1], item[0]))
                ],
                "chosen_action_groups": [
                    {
                        "start_state": {
                            "player_hp": key[0][0],
                            "enemy_hp": key[0][1],
                            "vulnerable": key[0][2],
                            "weak": key[0][3],
                        },
                        "actions": list(key[1]),
                        "end_player_hp_before_enemy": key[2],
                        "end_enemy_hp": key[3],
                        "block": key[4],
                        "prob": value,
                    }
                    for key, value in sorted(chosen_action_groups.items(), key=lambda item: (-item[1], item[0][0], item[0][1]))
                ],
                "next_state_groups": [
                    {
                        "player_hp": key[0],
                        "enemy_hp": key[1],
                        "vulnerable": key[2],
                        "weak": key[3],
                        "prob": value,
                    }
                    for key, value in sorted(next_groups.items(), key=lambda item: (-item[1], item[0]))
                ],
            }
        )
        frontier = next_frontier

    return {
        "variant": variant.name,
        "deck": list(deck),
        "deck_display": deck_display(deck),
        "exact_result": list(exact_result),
        "expected_kill_turn_if_success": expected_kill_turn(exact_result),
        "turn_rows": turn_rows,
    }


def audit_variant(variant: SearchVariant) -> dict[str, object]:
    decks = unique_decks(variant.card_pool, variant.min_deck_size, variant.max_deck_size)
    rows: list[dict[str, object]] = []
    stable_count = 0

    for deck in decks:
        result = exact_multiturn_result(deck, variant)
        row = {
            "deck": deck,
            "deck_size": len(deck),
            "result": result,
            "fail": result[-1],
        }
        rows.append(row)
        if result[-1] < 1e-12:
            stable_count += 1

    rows.sort(key=lambda row: row["result"][:-1], reverse=True)
    return {
        "variant": variant.name,
        "case_count": len(rows),
        "stable_count": stable_count,
        "top_rows": rows[:20],
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--variant", help="Variant name from the built-in catalog")
    parser.add_argument("--report-deck", help="Comma-separated exact deck list to export with turn-by-turn policy data")
    parser.add_argument("--format", choices=("json",), default="json")
    args = parser.parse_args()

    variants = variant_catalog()
    variant_map = {variant.name: variant for variant in variants}

    if args.variant and args.report_deck:
        if args.variant not in variant_map:
            raise SystemExit(f"Unknown variant: {args.variant}")
        deck = tuple(sorted(card.strip() for card in args.report_deck.split(",") if card.strip()))
        report = deck_turn_report_precise(deck, variant_map[args.variant])
        print(json.dumps(report, ensure_ascii=False, indent=2))
        return

    for variant in variants:
        result = audit_variant(variant)
        print(result["variant"])
        print("case_count", result["case_count"])
        print("stable_count", result["stable_count"])
        for row in result["top_rows"][:10]:
            print(row)
        print()


if __name__ == "__main__":
    main()
