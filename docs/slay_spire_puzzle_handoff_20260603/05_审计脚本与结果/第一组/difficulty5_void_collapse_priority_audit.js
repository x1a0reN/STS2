const fs = require("fs");
const path = require("path");

const N = 15;
const BASH = 0, UPPERCUT = 1, NEUTRALIZE = 2, CLOTHESLINE = 3, VOID_REND = 4;
const DAGGER = 5, QUICK = 6, BALL = 7, COLD = 8, SWORD = 9, RECYCLE = 10;
const STRIKE = 11, DEFEND = 12, FEINT = 13, VOID = 14;
const FIRE = 0, BREAKER = 1, ENERGY = 2;
const SHURIKEN = 0, ANCHOR = 1, VOID_LENS = 2;

const POOL = [1, 1, 1, 1, 1, 1, 2, 2, 1, 1, 1, 3, 3, 1, 0];
const COST = [2, 2, 0, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 99];
const CN = ["重击", "上勾拳", "中和", "金刚臂", "虚空裂解（改）", "投掷匕首（改）", "快斩（改）", "球状闪电（改）", "寒流（改）", "飞剑回旋", "回收（改）", "打击", "防御", "佯攻（改）", "虚空"];
const POTION_CN = ["火焰药水", "破障药水", "能量药水"];
const RELIC_CN = ["手里剑", "锚", "虚空透镜"];
const PLAY_ORDER = [BASH, UPPERCUT, NEUTRALIZE, CLOTHESLINE, VOID_REND, RECYCLE, SWORD, DAGGER, QUICK, BALL, COLD, STRIKE, DEFEND, FEINT];

const ENEMY_HP = Number(process.argv[2] || 88);
const PLAYER_HP = Number(process.argv[3] || 28);
const COLLAPSE_LOSS = Number(process.argv[4] || 36);
const FIRE_DMG = Number(process.argv[5] || 20);
const DRAW = 5;
const TURN_LIMIT = 5;
const ACTIVE_CAP = 2;
const ENEMY_DMG = [11, 19, 27, 33, 48];
const ARMOR_GAIN = [0, 14, 0, 16, 0];
const VOID_GAIN = [0, 1, 1, 1, 0];
const MAX_BUILDS = Number(process.env.GONGDOU_AUDIT_MAX_BUILDS || 0);
const NO_WRITE = process.env.GONGDOU_AUDIT_NO_WRITE === "1";

const combMemo = new Map();
function C(n, k) {
  if (k < 0 || k > n) return 0;
  if (k === 0 || k === n) return 1;
  const key = `${n},${k}`;
  if (combMemo.has(key)) return combMemo.get(key);
  let r = 1;
  for (let i = 1; i <= k; i++) r = (r * (n - k + i)) / i;
  combMemo.set(key, r);
  return r;
}
function zero() { return Array(N).fill(0); }
function clone(a) { return a.slice(); }
function total(a) { return a.reduce((s, v) => s + v, 0); }
function add(a, b) { const o = clone(a); for (let i = 0; i < N; i++) o[i] += b[i]; return o; }
function addOne(a, c) { const o = clone(a); o[c]++; return o; }
function subOne(a, c) { const o = clone(a); o[c]--; return o; }
function keyCounts(a) { return a.join(","); }
function success(v) { return v.slice(0, TURN_LIMIT).reduce((s, x) => s + x, 0); }
function fail() { const v = Array(TURN_LIMIT + 1).fill(0); v[TURN_LIMIT] = 1; return v; }
function kill(turn) { const v = Array(TURN_LIMIT + 1).fill(0); v[turn - 1] = 1; return v; }
function merge(items) {
  const out = Array(TURN_LIMIT + 1).fill(0);
  for (const [p, v] of items) for (let i = 0; i < out.length; i++) out[i] += p * v[i];
  return out;
}
function better(a, b) {
  if (!b) return true;
  if (Math.abs(success(a) - success(b)) > 1e-12) return success(a) > success(b);
  for (let i = 0; i < TURN_LIMIT; i++) {
    if (Math.abs(a[i] - b[i]) > 1e-12) return a[i] > b[i];
  }
  return false;
}
function atk(base, strength, vuln) {
  const raw = Math.max(0, base + strength);
  return vuln > 0 ? Math.floor(raw * 3 / 2) : raw;
}
function weakd(base, weak) { return weak > 0 ? Math.floor(base * 3 / 4) : base; }
function deal(ctx, amount) {
  const blocked = Math.min(ctx.armor, amount);
  ctx.armor -= blocked;
  ctx.enemy -= amount - blocked;
  enforcePhaseGate(ctx);
}
function minKillTurn(ctx) {
  if (ctx.potion === BREAKER) return 3;
  if (ctx.potion === FIRE) return 4;
  return 5;
}
function enforcePhaseGate(ctx) {
  if (ctx.enemy <= 0 && ctx.turn < minKillTurn(ctx)) {
    ctx.enemy = 1;
    ctx.armor = 0;
  }
}
function applyStatus(ctx, field, amount) {
  if (ctx.artifact > 0) ctx.artifact -= 1;
  else ctx[field] += amount;
}
function attack(ctx, base) {
  deal(ctx, atk(base, ctx.strength, ctx.vuln));
  ctx.attackHits += 1;
  while (ctx.relic === SHURIKEN && ctx.attackHits >= 3) {
    ctx.attackHits -= 3;
    ctx.strength += 1;
  }
}

function multiAttack(ctx, base, hits) {
  const amount = atk(base, ctx.strength, ctx.vuln);
  for (let i = 0; i < hits; i++) deal(ctx, amount);
  ctx.attackHits += hits;
  while (ctx.relic === SHURIKEN && ctx.attackHits >= 3) {
    ctx.attackHits -= 3;
    ctx.strength += 1;
  }
}

function hasVoidResource(ctx) {
  return ctx.hand[VOID] > 0 || ctx.discard[VOID] > 0;
}

function consumeVoidResource(ctx) {
  if (ctx.hand[VOID] > 0) {
    ctx.hand[VOID] -= 1;
    ctx.voidConsumed += 1;
    return true;
  }
  if (ctx.discard[VOID] > 0) {
    ctx.discard[VOID] -= 1;
    ctx.voidConsumed += 1;
    return true;
  }
  return false;
}

const drawMemo = new Map();
function drawOutcomes(cards, n, fixedDiscard = zero()) {
  const key = `${keyCounts(cards)}|${n}|${keyCounts(fixedDiscard)}`;
  if (drawMemo.has(key)) return drawMemo.get(key);
  const m = total(cards);
  if (n >= m) {
    const out = [{ hand: clone(cards), rest: zero(), discard: clone(fixedDiscard), prob: 1 }];
    drawMemo.set(key, out);
    return out;
  }
  const denom = C(m, n);
  const out = [];
  const pick = zero();
  function rec(i, left, ways) {
    if (i === N) {
      if (left === 0) {
        const rest = zero();
        for (let j = 0; j < N; j++) rest[j] = cards[j] - pick[j];
        out.push({ hand: clone(pick), rest, discard: clone(fixedDiscard), prob: ways / denom });
      }
      return;
    }
    for (let k = 0; k <= Math.min(cards[i], left); k++) {
      pick[i] = k;
      rec(i + 1, left - k, ways * C(cards[i], k));
    }
    pick[i] = 0;
  }
  rec(0, n, 1);
  drawMemo.set(key, out);
  return out;
}
function nextDraws(draw, discard, n) {
  if (total(draw) >= n) return drawOutcomes(draw, n, discard);
  const fixed = clone(draw);
  const need = n - total(draw);
  if (total(discard) === 0) return [{ hand: fixed, rest: zero(), discard: zero(), prob: 1 }];
  return drawOutcomes(discard, need, zero()).map(d => ({ hand: add(fixed, d.hand), rest: d.rest, discard: zero(), prob: d.prob }));
}

function playCard(ctx, card) {
  if (ctx.hand[card] <= 0 || ctx.energy < COST[card] || ctx.played >= ACTIVE_CAP) return false;
  ctx.hand = subOne(ctx.hand, card);
  ctx.energy -= COST[card];
  ctx.played += 1;
  if (card === BASH) { attack(ctx, 8); applyStatus(ctx, "vuln", 2); }
  else if (card === UPPERCUT) { attack(ctx, 13); applyStatus(ctx, "vuln", 1); applyStatus(ctx, "weak", 1); }
  else if (card === NEUTRALIZE) { attack(ctx, 3); applyStatus(ctx, "weak", 1); }
  else if (card === CLOTHESLINE) { attack(ctx, 12); applyStatus(ctx, "weak", 2); }
  else if (card === VOID_REND) {
    let damage = 8;
    if (consumeVoidResource(ctx)) {
      damage = 22;
      if (ctx.relic === VOID_LENS && !ctx.lensUsed) {
        damage += 24;
        ctx.lensUsed = 1;
      }
    }
    attack(ctx, damage);
  } else if (card === DAGGER) attack(ctx, 9);
  else if (card === QUICK) attack(ctx, ctx.artifact <= 0 ? 11 : 7);
  else if (card === BALL) attack(ctx, hasVoidResource(ctx) ? 12 : 5);
  else if (card === COLD) { attack(ctx, 6); ctx.block += 4; }
  else if (card === SWORD) { multiAttack(ctx, 3, 3); }
  else if (card === RECYCLE) {
    ctx.block += 5;
    if (consumeVoidResource(ctx)) {
      ctx.energy += 1;
      ctx.block += 8;
      ctx.weak += 1;
    }
  } else if (card === STRIKE) attack(ctx, 6);
  else if (card === DEFEND) ctx.block += 5;
  else if (card === FEINT) attack(ctx, 4);
  else return false;
  ctx.discard = addOne(ctx.discard, card);
  return true;
}
function usePotion(ctx, potion) {
  if (ctx.potionUsed) return false;
  ctx.potionUsed = 1;
  if (potion === FIRE) deal(ctx, FIRE_DMG);
  else if (potion === BREAKER) {
    ctx.artifact = 0;
    ctx.vuln += 2;
  } else if (potion === ENERGY) {
    ctx.energy += 3;
    ctx.block += 20;
  }
  return true;
}
function startTurnContext(state) {
  const ctx = {
    hand: clone(state.hand), draw: clone(state.draw), discard: clone(state.discard),
    hp: state.hp, enemy: state.enemy, armor: state.armor, artifact: state.artifact,
    vuln: state.vuln, weak: state.weak, strength: state.strength, potionUsed: state.potionUsed,
    relic: state.relic, energy: Math.max(0, 3 - state.hand[VOID]), block: 0,
    turn: state.turn, potion: state.potion,
    played: 0, attackHits: 0, lensUsed: 0, voidSeen: state.voidSeen + state.hand[VOID],
    voidConsumed: state.voidConsumed,
  };
  if (ctx.relic === ANCHOR && state.turn === 1) ctx.block += 10;
  if (state.turn === 5 && ctx.voidSeen >= 2) ctx.enemy -= COLLAPSE_LOSS;
  return ctx;
}

function cloneCtx(ctx) {
  return {
    hand: clone(ctx.hand), draw: clone(ctx.draw), discard: clone(ctx.discard),
    hp: ctx.hp, enemy: ctx.enemy, armor: ctx.armor, artifact: ctx.artifact,
    vuln: ctx.vuln, weak: ctx.weak, strength: ctx.strength, potionUsed: ctx.potionUsed,
    relic: ctx.relic, energy: ctx.energy, block: ctx.block, turn: ctx.turn, potion: ctx.potion,
    played: ctx.played, attackHits: ctx.attackHits, lensUsed: ctx.lensUsed,
    voidSeen: ctx.voidSeen, voidConsumed: ctx.voidConsumed,
  };
}

function ctxKey(ctx) {
  return [ctx.turn, ctx.hp, ctx.enemy, ctx.armor, ctx.artifact, ctx.vuln, ctx.weak, ctx.strength,
    ctx.potionUsed, ctx.potion, ctx.relic, ctx.energy, ctx.block, ctx.played, ctx.attackHits,
    ctx.lensUsed, ctx.voidSeen, ctx.voidConsumed, keyCounts(ctx.hand), keyCounts(ctx.draw), keyCounts(ctx.discard)].join("|");
}

const memo = new Map();
function stateKey(s) {
  return [s.turn, s.hp, s.enemy, s.armor, s.artifact, s.vuln, s.weak, s.strength, s.potionUsed, s.potion, s.relic, s.voidSeen, s.voidConsumed, keyCounts(s.hand), keyCounts(s.draw), keyCounts(s.discard)].join("|");
}
function solve(state) {
  if (state.enemy <= 0) return kill(state.turn);
  if (state.hp <= 0) return fail();
  const key = stateKey(state);
  if (memo.has(key)) return memo.get(key);
  let best = bestFromCtx(startTurnContext(state), new Map());
  if (!best) best = fail();
  memo.set(key, best);
  return best;
}

function endTurnValue(ctx) {
  if (ctx.enemy <= 0) return kill(ctx.turn);
  if (ctx.turn >= TURN_LIMIT) return fail();
  const incoming = weakd(ENEMY_DMG[ctx.turn - 1], ctx.weak);
  const hp2 = ctx.hp - Math.max(0, incoming - ctx.block);
  if (hp2 <= 0) return fail();
  let discard2 = add(ctx.discard, ctx.hand);
  for (let i = 0; i < VOID_GAIN[ctx.turn - 1]; i++) discard2 = addOne(discard2, VOID);
  const items = [];
  for (const d of nextDraws(ctx.draw, discard2, DRAW)) {
    items.push([d.prob, solve({
      turn: ctx.turn + 1, hp: hp2, enemy: ctx.enemy, armor: ctx.armor + ARMOR_GAIN[ctx.turn - 1],
      artifact: ctx.artifact, vuln: Math.max(0, ctx.vuln - 1), weak: Math.max(0, ctx.weak - 1),
      strength: ctx.strength, potionUsed: ctx.potionUsed, potion: ctx.potion, relic: ctx.relic,
      voidSeen: ctx.voidSeen, voidConsumed: ctx.voidConsumed, hand: d.hand, draw: d.rest, discard: d.discard,
    })]);
  }
  return merge(items);
}

function bestFromCtx(ctx, turnMemo) {
  if (ctx.enemy <= 0) return kill(ctx.turn);
  const key = ctxKey(ctx);
  if (turnMemo.has(key)) return turnMemo.get(key);

  let best = endTurnValue(ctx);
  if (!ctx.potionUsed) {
    const next = cloneCtx(ctx);
    if (usePotion(next, next.potion)) {
      const cand = bestFromCtx(next, turnMemo);
      if (better(cand, best)) best = cand;
    }
  }

  for (const card of PLAY_ORDER) {
    if (card === VOID || ctx.hand[card] <= 0 || ctx.energy < COST[card] || ctx.played >= ACTIVE_CAP) continue;
    const next = cloneCtx(ctx);
    if (playCard(next, card)) {
      const cand = bestFromCtx(next, turnMemo);
      if (better(cand, best)) best = cand;
    }
  }

  turnMemo.set(key, best);
  return best;
}
function resultFor(deck, potion, relic) {
  memo.clear();
  const items = [];
  for (const d of drawOutcomes(deck, DRAW)) {
    items.push([d.prob, solve({
      turn: 1, hp: PLAYER_HP, enemy: ENEMY_HP, armor: 0, artifact: 3, vuln: 0, weak: 0, strength: 0,
      potionUsed: 0, potion, relic, voidSeen: 0, voidConsumed: 0, hand: d.hand, draw: d.rest, discard: zero(),
    })]);
  }
  return merge(items);
}

function genDecksRec(i, left, cur, out) {
  if (i === N) {
    const output = cur[DAGGER] + cur[QUICK] + cur[BALL] + cur[SWORD];
    const defense = cur[COLD] + cur[DEFEND] + cur[RECYCLE];
    const filler = cur[STRIKE] + cur[FEINT];
    if (
      left === 0 &&
      cur[BASH] + cur[CLOTHESLINE] <= 1
    ) out.push(clone(cur));
    return;
  }
  for (let k = 0; k <= Math.min(POOL[i], left); k++) {
    cur[i] = k;
    genDecksRec(i + 1, left - k, cur, out);
  }
  cur[i] = 0;
}
function legalDecks() {
  const out = [];
  genDecksRec(0, 10, zero(), out);
  return out;
}
function firstTurn(v) {
  for (let i = 0; i < TURN_LIMIT; i++) if (v[i] > 1e-9) return i + 1;
  return 0;
}
function pct(x) { return Math.round(x * 1000000) / 10000; }
function deckStr(deck) {
  const parts = [];
  for (let i = 0; i < VOID; i++) if (deck[i]) parts.push(CN[i] + (deck[i] > 1 ? ` x${deck[i]}` : ""));
  return parts.join("、");
}
function family(deck, potion, relic) {
  if (relic === VOID_LENS) return "虚空透镜裂解线";
  if (potion === BREAKER && (deck[BASH] || deck[UPPERCUT])) return "破障易伤线";
  if (relic === SHURIKEN && deck[SWORD]) return "手里剑多段线";
  if (potion === ENERGY && deck[RECYCLE]) return "回收能量线";
  if (relic === ANCHOR && deck[COLD] + deck[DEFEND] >= 2) return "锚防守坍缩线";
  if (potion === FIRE) return "火焰补刀线";
  return "混合压低线";
}

const decks = legalDecks();
const rows = [];
let audited = 0;
for (const deck of decks) {
  for (let potion = 0; potion < 3; potion++) {
    for (let relic = 0; relic < 3; relic++) {
      const vec = resultFor(deck, potion, relic);
      rows.push({
        success: pct(success(vec)),
        first_turn: firstTurn(vec),
        family: family(deck, potion, relic),
        build_display: `${deckStr(deck)}；${POTION_CN[potion]}；${RELIC_CN[relic]}`,
        kill_vector: vec.map(pct),
      });
      audited += 1;
      if (MAX_BUILDS > 0 && audited >= MAX_BUILDS) break;
      if (audited % 250 === 0) console.log(`audited ${audited}/${decks.length * 9}`);
    }
    if (MAX_BUILDS > 0 && audited >= MAX_BUILDS) break;
  }
  if (MAX_BUILDS > 0 && audited >= MAX_BUILDS) break;
}
rows.sort((a, b) => (b.success - a.success) || ((a.first_turn || 99) - (b.first_turn || 99)) || (b.kill_vector[1] - a.kill_vector[1]) || (b.kill_vector[2] - a.kill_vector[2]) || (b.kill_vector[3] - a.kill_vector[3]));
const bestByFirstTurn = {};
const bestByFamily = {};
for (const row of rows) {
  if (!(row.first_turn in bestByFirstTurn)) bestByFirstTurn[row.first_turn] = row;
  if (!(row.family in bestByFamily)) bestByFamily[row.family] = row;
}
const summary = {
  audit_model_version: "2026-06-09-resource-first-random-state-v2",
  decision_model: "per-turn optimal action search over card order, stop timing, and potion timing; PLAY_ORDER is iteration order only, not a forced play sequence",
  draw_model: "exact opening draw, natural draw, discard shuffle, enemy-added Void into discard, and Void status energy loss",
  void_resource_model: "Void in hand or discard is a valid resource; consumption prefers hand, then discard",
  invalid_old_model: "Any model requiring Void and Void Rend to be in the same hand is invalid for the current puzzle/runtime",
  max_builds: MAX_BUILDS,
  enemy_hp: ENEMY_HP,
  player_hp: PLAYER_HP,
  collapse_loss: COLLAPSE_LOSS,
  fire_damage: FIRE_DMG,
  legal_deck_count: decks.length,
  legal_build_count: rows.length,
  perfect_success_count: rows.filter(r => r.success >= 99.9999).length,
  top30: rows.slice(0, 30),
  best_by_first_turn: bestByFirstTurn,
  best_by_family: bestByFamily,
};
const outputName = MAX_BUILDS > 0
  ? "difficulty5_void_collapse_priority_audit.smoke.json"
  : "difficulty5_void_collapse_priority_audit.json";
if (!NO_WRITE) fs.writeFileSync(path.join(__dirname, outputName), JSON.stringify(summary, null, 2), "utf8");
console.log(`enemy_hp ${ENEMY_HP} player_hp ${PLAYER_HP} collapse_loss ${COLLAPSE_LOSS}`);
console.log(`legal_deck_count ${summary.legal_deck_count}`);
console.log(`legal_build_count ${summary.legal_build_count}`);
console.log(`perfect_success_count ${summary.perfect_success_count}`);
console.log(`best ${rows[0].success} first ${rows[0].first_turn} ${rows[0].family} [${rows[0].kill_vector.join(",")}] ${rows[0].build_display}`);
console.log("best_by_first_turn");
for (const k of Object.keys(bestByFirstTurn).sort((a, b) => Number(a) - Number(b))) {
  const r = bestByFirstTurn[k];
  console.log(`${k} ${r.success} ${r.family} [${r.kill_vector.join(",")}] ${r.build_display}`);
}
console.log("best_by_family");
for (const k of Object.keys(bestByFamily).sort()) {
  const r = bestByFamily[k];
  console.log(`${k} ${r.success} first ${r.first_turn} [${r.kill_vector.join(",")}] ${r.build_display}`);
}
