---
name: slay-spire-mod-design-director
description: Use when working on Slay the Spire-style puzzle design, card/relic/potion mechanics, mod production specs, player-facing difficulty, balance audits, or converting gameplay ideas into implementable mod tasks. Coordinates player perspective, designer perspective, probability control, and installed gameplay-design skills.
---

# Slay Spire Mod Design Director

This skill is the entry point for Slay the Spire-style残局题、卡牌/药水/遗物设计、mod 机制设计、难度曲线、玩家体验复核和实现交接。

## Core Stance

Always check the work from three angles:

- Player view: Can a human understand the goal, read the trap, and feel the route difference?
- Designer view: Are difficulty, resource choice, route count, and bypass risk controlled?
- Mod maker view: Are card text, enemy intent, relic triggers, state timing, and edge cases implementable without ambiguity?

Do not treat a mathematically valid puzzle as finished if it is boring, visually obvious, or hard to parse from the player side.

## Skill Routing Rules

Use these rules before starting substantial work:

- Use this skill for any Slay the Spire-like puzzle, card pool, potion pool, relic pool, enemy action, probability audit, or mod production task.
- Use `gameplay-design-orchestrator` when the user gives a raw game/mod idea and wants a complete gameplay package or direction lock.
- Use `gameplay-input-normalizer` when the user prompt is loose, contradictory, or needs audience/platform/scope constraints extracted.
- Use `gameplay-fantasy-loop-designer` when defining the core fun, player promise, repeated decision loop, or theme of a puzzle series.
- Use `gameplay-pacing-and-structure` when checking difficulty ramp, turn pressure, learning curve, frustration, or route pacing.
- Use `gameplay-system-weaver-and-scope-cutter` when adding cards, potions, relics, enemies, status effects, or when deciding what to cut for controllability.
- Use `gameplay-coding-handoff-compiler` when converting a design into implementable mod objects, scenes, JSON/data tables, scripts, triggers, tests, or acceptance criteria.
- Use `game-design-spec` when the user asks for a complete Chinese production spec.
- Use `game-design-execution-compiler` when a validated spec needs an implementation plan.
- Use `game-design-execution-runner` only when executing a prepared plan step by step.
- Use `frontend-skill` only for playable UI/prototype/web tool work, not for pure puzzle text.
- Use `playwright` only when verifying an implemented UI/prototype.
- Use `skill-creator` when the current workflow needs a new reusable skill or a rule update.

## Non-Negotiable Puzzle Checks

Before finalizing a puzzle:

- Card pool size must match target difficulty.
- Player must have meaningful resource choices, not single forced resources disguised as choices.
- If potion is optional, provide a potion pool or a real timing/resource tradeoff.
- From difficulty 5 onward, require card, potion, and relic choices unless the user explicitly says otherwise.
- Modified original cards must be marked with `（改）` and must list removed or changed effects.
- Enemy behavior must add a new pressure source as difficulty rises; do not reuse only “0 damage then increasing attack.”
- Representative routes must not all collapse into one indistinguishable turn pattern.
- If a fast route has tiny same-turn kill probability, do not market it as a main fast solution.
- Include traps that are plausible to a player, not obvious dead cards.
- For final user-facing puzzle documents, omit audit process unless the user asks for it.

## Audit Expectations

When doing probability-controlled puzzles:

- Enumerate all legal card choices.
- Enumerate potion/relic choices if present.
- Track draw pile, discard pile, hand, energy, HP, block, enemy HP, armor, vulnerable, weak, artifacts, status cards, and turn limit.
- Output kill vector internally: `[T1, T2, T3, T4, ..., fail]`.
- Check highest success route, fastest route, stable route, 100% route count, and unclassified high-success routes.
- Re-run full audit after any card, potion, relic, enemy, HP, or selection-range change.

## Final Output Discipline

Match the user’s requested artifact:

- If the user asks for “只保留题目本身”, output only player-facing puzzle documents.
- If the user asks for framework, include rules and templates but no long process diary.
- If the user asks for mod implementation, provide data schemas, trigger timing, file/task breakdown, and tests.
- If the user asks whether a design is okay, answer with risks first, then concrete fixes.

