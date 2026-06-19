const fs = require("fs");

const N = 13;
const ERUPTION = 0, VIGILANCE = 1, EMPTY_FIST = 2, EMPTY_BODY = 3, FLURRY = 4, WHEEL = 5, CONSECRATE = 6;
const CUT = 7, BOWLING = 8, PROTECT = 9, STRIKE = 10, DEFEND = 11, HALT = 12;
const WRATH_POTION = 0, CALM_POTION = 1, FIRE_POTION = 2;
const LOTUS = 0, WRATH_GUARD = 1, STANCE_MARK = 2;
const NORMAL = 0, CALM = 1, WRATH = 2;

const POOL = [1, 1, 1, 1, 2, 1, 1, 2, 1, 1, 3, 3, 2];
const COST = [2, 2, 1, 1, 0, 2, 0, 1, 1, 1, 1, 1, 0];
const CN = ["痛击（改）", "防御（改）", "切割（改）", "防御（改）", "切割（改）", "上勾拳（改）", "祭品（改）", "切割（改）", "全身撞击（改）", "防御（改）", "打击", "防御", "防御（改）"];
const POTION_CN = ["怒火药水", "平静药水", "火焰药水"];
const RELIC_CN = ["紫莲花", "怒焰护符", "姿态刻印"];
const CARD_ORDER = Array.from({ length: N }, (_, i) => i);

const ENEMY_HP = Number(process.argv[2] || 94);
const PLAYER_HP = Number(process.argv[3] || 29);
const FIRE_DMG = Number(process.argv[4] || 18);
const CALM_BREACH_LOSS = Number(process.argv[5] || 28);
function optionNumber(argIndex, envName, fallback = 0) {
  const raw = process.argv[argIndex] ?? process.env[envName] ?? fallback;
  const n = Number(raw);
  return Number.isFinite(n) ? n : fallback;
}
const MAX_BUILDS = optionNumber(6, "GONGDOU_AUDIT_MAX_BUILDS");
const SKIP_BUILDS = optionNumber(7, "GONGDOU_AUDIT_SKIP_BUILDS");
const PROGRESS_EVERY = optionNumber(8, "GONGDOU_AUDIT_PROGRESS_EVERY", 200);
const NO_WRITE = process.env.GONGDOU_AUDIT_NO_WRITE === "1";
const DRAW = 5;
const TURN_LIMIT = 5;
const ACTIVE_CAP = 2;
const ENEMY_DMG = [12, 19, 26, 34, 48];
const ARMOR_GAIN = [0, 14, 0, 22, 0];

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
  for (let i = 0; i < TURN_LIMIT; i++) if (Math.abs(a[i] - b[i]) > 1e-12) return a[i] > b[i];
  return false;
}
function weakd(base, weak) { return weak > 0 ? Math.floor(base * 3 / 4) : base; }
function deal(ctx, amount) {
  const blocked = Math.min(ctx.armor, amount);
  ctx.armor -= blocked;
  ctx.enemy -= amount - blocked;
}
function changeStance(ctx, next) {
  if (ctx.stance === next) return;
  if (ctx.stance === CALM) {
    ctx.energy += (ctx.relic === LOTUS ? 4 : 2);
    ctx.calmExit += 1;
  }
  ctx.stance = next;
  ctx.changed = 1;
  if (next === WRATH && ctx.relic === WRATH_GUARD && !ctx.guardUsed) {
    ctx.block += 7;
    ctx.guardUsed = 1;
  }
}
function attack(ctx, base) {
  let damage = base;
  if (ctx.relic === LOTUS && ctx.stance === CALM) damage += 8;
  if (ctx.relic === STANCE_MARK && ctx.changed && !ctx.markUsed) {
    damage += 5;
    ctx.markUsed = 1;
  }
  if (ctx.stance === WRATH) damage *= 2;
  deal(ctx, damage);
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
  ctx.discard = addOne(ctx.discard, card);
  if (card === ERUPTION) { attack(ctx, 9); changeStance(ctx, WRATH); }
  else if (card === VIGILANCE) { ctx.block += 8; changeStance(ctx, CALM); }
  else if (card === EMPTY_FIST) { attack(ctx, 9); changeStance(ctx, NORMAL); }
  else if (card === EMPTY_BODY) { ctx.block += 8; changeStance(ctx, NORMAL); }
  else if (card === FLURRY) attack(ctx, ctx.changed ? 8 : 4);
  else if (card === WHEEL) attack(ctx, 15);
  else if (card === CONSECRATE) attack(ctx, 5);
  else if (card === CUT) attack(ctx, ctx.stance === CALM ? 11 : 6);
  else if (card === BOWLING) attack(ctx, ctx.armor > 0 ? 12 : 8);
  else if (card === PROTECT) ctx.block += 11;
  else if (card === STRIKE) attack(ctx, 6);
  else if (card === DEFEND) ctx.block += 5;
  else if (card === HALT) ctx.block += (ctx.stance === WRATH ? 9 : 4);
  else return false;
  return true;
}
function usePotion(ctx, potion) {
  if (ctx.potionUsed) return false;
  ctx.potionUsed = 1;
  if (potion === WRATH_POTION) {
    ctx.energy += 1;
    changeStance(ctx, WRATH);
  } else if (potion === CALM_POTION) {
    ctx.block += 6;
    changeStance(ctx, CALM);
  } else if (potion === FIRE_POTION) {
    deal(ctx, FIRE_DMG);
  }
  return true;
}

function startTurnContext(state) {
  const ctx = {
    turn: state.turn, hand: clone(state.hand), draw: clone(state.draw), discard: clone(state.discard),
    hp: state.hp, enemy: state.enemy, armor: state.armor, stance: state.stance,
    energy: 3, block: 0, potionUsed: state.potionUsed, potion: state.potion, relic: state.relic,
    changed: 0, markUsed: 0, guardUsed: 0, played: 0, calmExit: state.calmExit,
  };
  if (state.turn === 4 && ctx.calmExit >= 2) ctx.enemy -= CALM_BREACH_LOSS;
  return ctx;
}

function cloneCtx(ctx) {
  return {
    ...ctx,
    hand: clone(ctx.hand),
    draw: clone(ctx.draw),
    discard: clone(ctx.discard),
  };
}

function ctxKey(ctx) {
  return [
    ctx.turn, ctx.hp, ctx.enemy, ctx.armor, ctx.stance, ctx.energy, ctx.block,
    ctx.potionUsed, ctx.potion, ctx.relic, ctx.changed, ctx.markUsed, ctx.guardUsed, ctx.played, ctx.calmExit,
    keyCounts(ctx.hand), keyCounts(ctx.draw), keyCounts(ctx.discard),
  ].join("|");
}

function endTurnValue(ctx) {
  if (ctx.enemy <= 0) return kill(ctx.turn);
  if (ctx.turn >= TURN_LIMIT) return fail();

  let incoming = weakd(ENEMY_DMG[ctx.turn - 1], 0);
  if (ctx.stance === WRATH) incoming *= 2;
  const hp2 = ctx.hp - Math.max(0, incoming - ctx.block);
  if (hp2 <= 0) return fail();

  const discard2 = add(ctx.discard, ctx.hand);
  const items = [];
  for (const d of nextDraws(ctx.draw, discard2, DRAW)) {
    items.push([d.prob, solve({
      turn: ctx.turn + 1, hp: hp2, enemy: ctx.enemy, armor: ctx.armor + ARMOR_GAIN[ctx.turn - 1],
      stance: ctx.stance, potionUsed: ctx.potionUsed, potion: ctx.potion, relic: ctx.relic,
      calmExit: ctx.calmExit,
      hand: d.hand, draw: d.rest, discard: d.discard,
    })]);
  }
  return merge(items);
}

const actionMemo = new Map();
function bestFromCtx(ctx) {
  const key = ctxKey(ctx);
  if (actionMemo.has(key)) return actionMemo.get(key);
  let best = endTurnValue(ctx);

  if (!ctx.potionUsed) {
    const p = cloneCtx(ctx);
    if (usePotion(p, p.potion)) {
      const cand = bestFromCtx(p);
      if (better(cand, best)) best = cand;
    }
  }

  for (const card of CARD_ORDER) {
    if (ctx.hand[card] <= 0 || ctx.energy < COST[card] || ctx.played >= ACTIVE_CAP) continue;
    const next = cloneCtx(ctx);
    if (playCard(next, card)) {
      const cand = bestFromCtx(next);
      if (better(cand, best)) best = cand;
    }
  }
  actionMemo.set(key, best);
  return best;
}

const memo = new Map();
function stateKey(s) {
  return [s.turn, s.hp, s.enemy, s.armor, s.stance, s.potionUsed, s.potion, s.relic, s.calmExit, keyCounts(s.hand), keyCounts(s.draw), keyCounts(s.discard)].join("|");
}
function solve(state) {
  if (state.enemy <= 0) return kill(state.turn);
  if (state.hp <= 0) return fail();
  const key = stateKey(state);
  if (memo.has(key)) return memo.get(key);
  let best = bestFromCtx(startTurnContext(state));
  if (!best) best = fail();
  memo.set(key, best);
  return best;
}
function resultFor(deck, potion, relic) {
  memo.clear();
  actionMemo.clear();
  const items = [];
  for (const d of drawOutcomes(deck, DRAW)) {
    items.push([d.prob, solve({
      turn: 1, hp: PLAYER_HP, enemy: ENEMY_HP, armor: 0, stance: NORMAL, potionUsed: 0,
      potion, relic, calmExit: 0, hand: d.hand, draw: d.rest, discard: zero(),
    })]);
  }
  return merge(items);
}

function genDecksRec(i, left, cur, out) {
  if (i === N) {
    // Current D6 rules only constrain total deck size; older required-card gates are invalid.
    if (left === 10) out.push(clone(cur));
    return;
  }
  for (let k = 0; k <= Math.min(POOL[i], 10 - left); k++) {
    cur[i] = k;
    genDecksRec(i + 1, left + k, cur, out);
  }
  cur[i] = 0;
}
function legalDecks() {
  const out = [];
  genDecksRec(0, 0, zero(), out);
  return out;
}
function firstTurn(v) {
  for (let i = 0; i < TURN_LIMIT; i++) if (v[i] > 1e-9) return i + 1;
  return 0;
}
function pct(x) { return Math.round(x * 1000000) / 10000; }
function deckStr(deck) {
  const parts = [];
  for (let i = 0; i < N; i++) if (deck[i]) parts.push(CN[i] + (deck[i] > 1 ? ` x${deck[i]}` : ""));
  return parts.join("、");
}
function family(deck, potion, relic) {
  if (relic === LOTUS && deck[EMPTY_FIST]) return "紫莲花平静返能线";
  if (relic === STANCE_MARK && deck[FLURRY]) return "姿态刻印追击线";
  if (relic === WRATH_GUARD && (potion === WRATH_POTION || deck[ERUPTION])) return "怒焰护符快线";
  if (potion === CALM_POTION) return "平静药水稳线";
  if (deck[BOWLING] && deck[CUT] >= 2) return "护甲破口线";
  if (deck[DEFEND] + deck[HALT] >= 3) return "过量防御陷阱";
  return "混合线";
}

const decks = legalDecks();
const rows = [];
let done = 0;
let seen = 0;
const totalBuilds = decks.length * 9;
outer:
for (const deck of decks) {
  for (let potion = 0; potion < 3; potion++) {
    for (let relic = 0; relic < 3; relic++) {
      if (seen++ < SKIP_BUILDS) continue;
      const vec = resultFor(deck, potion, relic);
      rows.push({
        success: pct(success(vec)),
        first_turn: firstTurn(vec),
        family: family(deck, potion, relic),
        build_display: `${deckStr(deck)}；${POTION_CN[potion]}；${RELIC_CN[relic]}`,
        kill_vector: vec.map(pct),
      });
      done++;
      if (PROGRESS_EVERY && done % PROGRESS_EVERY === 0) {
        const best = rows.reduce((a, b) => (a.success > b.success ? a : b));
        console.log(`audited ${done}/${totalBuilds} best_so_far ${best.success} first ${best.first_turn} ${best.family}`);
      }
      if (MAX_BUILDS && done >= MAX_BUILDS) break outer;
    }
  }
}
rows.sort((a, b) => (b.success - a.success) || ((a.first_turn || 99) - (b.first_turn || 99)) || (b.kill_vector[1] - a.kill_vector[1]) || (b.kill_vector[2] - a.kill_vector[2]) || (b.kill_vector[3] - a.kill_vector[3]));
const bestByFirstTurn = {};
const bestByFamily = {};
for (const row of rows) {
  if (!(row.first_turn in bestByFirstTurn)) bestByFirstTurn[row.first_turn] = row;
  if (!(row.family in bestByFamily)) bestByFamily[row.family] = row;
}
const summary = {
  enemy_hp: ENEMY_HP,
  player_hp: PLAYER_HP,
  fire_damage: FIRE_DMG,
  calm_breach_loss: CALM_BREACH_LOSS,
  decision_model: "full_action_search",
  legal_deck_count: decks.length,
  total_build_count: totalBuilds,
  skip_builds: SKIP_BUILDS,
  legal_build_count: rows.length,
  perfect_success_count: rows.filter(r => r.success >= 99.9999).length,
  top30: rows.slice(0, 30),
  best_by_first_turn: bestByFirstTurn,
  best_by_family: bestByFamily,
};
const suffix = SKIP_BUILDS || MAX_BUILDS ? `_part_${SKIP_BUILDS}_${rows.length}` : "";
if (!NO_WRITE) fs.writeFileSync(`difficulty6_stance_controlled_audit${suffix}.json`, JSON.stringify(summary, null, 2), "utf8");
console.log(`enemy_hp ${ENEMY_HP} player_hp ${PLAYER_HP}`);
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
