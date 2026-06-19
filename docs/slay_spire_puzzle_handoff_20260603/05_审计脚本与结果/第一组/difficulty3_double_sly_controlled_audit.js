const fs = require("fs");

const N = 12;
const BASH = 0, PREPARED = 1, SURVIVOR = 2, FINISHER = 3, BACKSTAB = 4, DAGGER = 5;
const QUICK = 6, NEUTRALIZE = 7, STRIKE = 8, DEFEND = 9, SHADOW = 10, FEINT = 11;
const SLY_BREW = 0, VULN_POTION = 1, FIRE_POTION = 2;
const SHARP_DICE = 0, RETURN_HOLSTER = 1, HOLLOW_AMULET = 2;

const POOL = [1, 1, 1, 1, 2, 1, 2, 1, 3, 2, 2, 2];
const COST = [2, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0];
const CN = ["痛击", "预备（改）", "生存者（改）", "终结（改）", "背刺（改）", "投掷匕首（改）", "佯刺（改）", "中和", "打击", "防御", "影步（改）", "虚刃（改）"];
const POTION_CN = ["奇巧药水", "易伤药水", "火焰药水"];
const RELIC_CN = ["锋利骰子", "折返皮套", "空心护符"];
const CARD_ORDER = Array.from({ length: N }, (_, i) => i);
const DISCARD_PRIORITY = [BACKSTAB, SHADOW, FEINT, QUICK, DAGGER, STRIKE, DEFEND, NEUTRALIZE, BASH, FINISHER];

const ENEMY_HP = Number(process.argv[2] || 92);
const PLAYER_HP = Number(process.argv[3] || 24);
function optionNumber(argIndex, envName, fallback = 0) {
  const raw = process.argv[argIndex] ?? process.env[envName] ?? fallback;
  const n = Number(raw);
  return Number.isFinite(n) ? n : fallback;
}
const MAX_BUILDS = optionNumber(4, "GONGDOU_AUDIT_MAX_BUILDS");
const SKIP_BUILDS = optionNumber(5, "GONGDOU_AUDIT_SKIP_BUILDS");
const PROGRESS_EVERY = optionNumber(6, "GONGDOU_AUDIT_PROGRESS_EVERY", 200);
const NO_WRITE = process.env.GONGDOU_AUDIT_NO_WRITE === "1";
const DRAW = 5;
const ACTIVE_CAP = 2;
const LATE_BREAK = 40;

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
function atk(base, vuln) { return vuln > 0 ? Math.floor(base * 3 / 2) : base; }
function weakd(base, weak) { return weak > 0 ? Math.floor(base * 3 / 4) : base; }
function isSly(c) { return c === BACKSTAB || c === SHADOW; }
function success(v) { return v[0] + v[1] + v[2] + v[3]; }
function fail() { return [0, 0, 0, 0, 1]; }
function kill(turn) { const v = [0, 0, 0, 0, 0]; v[turn - 1] = 1; return v; }
function merge(items) {
  const out = [0, 0, 0, 0, 0];
  for (const [p, v] of items) for (let i = 0; i < 5; i++) out[i] += p * v[i];
  return out;
}
function better(a, b) {
  if (!b) return true;
  if (Math.abs(success(a) - success(b)) > 1e-12) return success(a) > success(b);
  for (let i = 0; i < 4; i++) if (Math.abs(a[i] - b[i]) > 1e-12) return a[i] > b[i];
  return false;
}
function deal(ctx, amount) {
  const blocked = Math.min(ctx.armor, amount);
  ctx.armor -= blocked;
  ctx.enemy -= amount - blocked;
}

const drawMemo = new Map();
function drawOutcomes(cards, n, fixedDiscard = zero()) {
  const key = keyCounts(cards) + "|" + n + "|" + keyCounts(fixedDiscard);
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

function discardCard(ctx, card) {
  ctx.hand = subOne(ctx.hand, card);
  if (!ctx.firstDiscard) {
    ctx.firstDiscard = 1;
    if (ctx.relic === RETURN_HOLSTER) ctx.energy += 1;
    if (ctx.relic === HOLLOW_AMULET) ctx.block += 6;
  }
  if (isSly(card)) {
    let damage = card === BACKSTAB ? 10 : 7;
    if (card === SHADOW) ctx.block += 4;
    if (card === BACKSTAB && ctx.relic === SHARP_DICE && !ctx.sharpUsed && ctx.block === 0) {
      damage += 2;
      ctx.sharpUsed = 1;
    }
    ctx.turnSly += 1;
    ctx.slyTotal += 1;
    deal(ctx, atk(damage, ctx.vuln));
  } else {
    ctx.badDiscard = 1;
  }
  ctx.discard = addOne(ctx.discard, card);
}

function firstDiscardTarget(hand) {
  for (const c of DISCARD_PRIORITY) if (hand[c] > 0) return c;
  return -1;
}

function playCard(ctx, card) {
  if (ctx.hand[card] <= 0 || ctx.energy < COST[card] || ctx.played >= ACTIVE_CAP) return false;
  ctx.hand = subOne(ctx.hand, card);
  ctx.energy -= COST[card];
  ctx.played += 1;
  if (card === STRIKE) deal(ctx, atk(6, ctx.vuln));
  else if (card === DEFEND) ctx.block += 5;
  else if (card === BASH) { deal(ctx, atk(8, ctx.vuln)); ctx.vuln += 2; }
  else if (card === NEUTRALIZE) { deal(ctx, atk(3, ctx.vuln)); ctx.weak += 1; }
  else if (card === DAGGER) deal(ctx, atk(9, ctx.vuln));
  else if (card === QUICK) deal(ctx, atk(8, ctx.vuln));
  else if (card === FEINT) deal(ctx, atk(4, ctx.vuln));
  else if (card === BACKSTAB) { deal(ctx, atk(6, ctx.vuln)); ctx.activeSly += 1; }
  else if (card === SHADOW) { deal(ctx, atk(5, ctx.vuln)); ctx.block += 3; ctx.activeSly += 1; }
  else if (card === FINISHER) deal(ctx, atk(8 + 4 * ctx.slyTotal, ctx.vuln));
  else if (card === PREPARED || card === SURVIVOR) {
    if (ctx.firstDiscard || total(ctx.hand) <= 0) {
      ctx.discard = addOne(ctx.discard, card);
      return true;
    }
    if (card === SURVIVOR) ctx.block += 5;
    const target = firstDiscardTarget(ctx.hand);
    if (target >= 0) discardCard(ctx, target);
  }
  ctx.discard = addOne(ctx.discard, card);
  return true;
}

function startTurnContext(state) {
  return {
    turn: state.turn, hp: state.hp, potion: state.potion,
    hand: clone(state.hand), draw: clone(state.draw), discard: clone(state.discard),
    energy: 3, block: 0, enemy: state.enemy, armor: state.armor, vuln: state.vuln, weak: state.weak,
    potionUsed: state.potionUsed, relic: state.relic,
    firstDiscard: 0, sharpUsed: 0, turnSly: 0, slyTotal: state.slyTotal, activeSly: 0, badDiscard: 0, played: 0,
    slyStreak: state.slyStreak,
  };
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
    ctx.turn, ctx.hp, ctx.enemy, ctx.armor, ctx.vuln, ctx.weak, ctx.potionUsed, ctx.potion, ctx.relic,
    ctx.energy, ctx.block, ctx.firstDiscard, ctx.sharpUsed, ctx.turnSly, ctx.slyTotal,
    ctx.activeSly, ctx.badDiscard, ctx.played, ctx.slyStreak,
    keyCounts(ctx.hand), keyCounts(ctx.draw), keyCounts(ctx.discard),
  ].join("|");
}

function usePotion(ctx) {
  if (ctx.potionUsed) return false;
  if (ctx.potion === SLY_BREW) {
    const target = firstDiscardTarget(ctx.hand);
    if (target < 0) return false;
    ctx.potionUsed = 1;
    discardCard(ctx, target);
    return true;
  }
  if (ctx.potion === VULN_POTION) {
    ctx.vuln += 3;
    ctx.potionUsed = 1;
    return true;
  }
  if (ctx.potion === FIRE_POTION) {
    deal(ctx, 20);
    ctx.potionUsed = 1;
    return true;
  }
  return false;
}

function endTurnValue(ctx) {
  if (ctx.enemy <= 0) return kill(ctx.turn);
  if (ctx.turn >= 4) return fail();

  const incomingByTurn = [10, 25, 26, 34];
  const noSlyArmorByTurn = [16, 24, 10, 0];
  let incoming = weakd(incomingByTurn[ctx.turn - 1], ctx.weak);
  let armorGain = ctx.turnSly === 0 ? noSlyArmorByTurn[ctx.turn - 1] : 0;
  armorGain += 10 * ctx.activeSly;
  if (ctx.badDiscard) armorGain += 8;

  const hp2 = ctx.hp - Math.max(0, incoming - ctx.block);
  if (hp2 <= 0) return fail();

  const discard2 = add(ctx.discard, ctx.hand);
  const slyStreak2 = ctx.turnSly > 0 ? ctx.slyStreak + 1 : 0;
  const enemy2 = (ctx.turn + 1 === 4 && slyStreak2 >= 3) ? ctx.enemy - LATE_BREAK : ctx.enemy;
  const items = [];
  for (const d of nextDraws(ctx.draw, discard2, DRAW)) {
    items.push([d.prob, solve({
      turn: ctx.turn + 1, hp: hp2, enemy: enemy2, armor: ctx.armor + armorGain,
      vuln: Math.max(0, ctx.vuln - 1), weak: Math.max(0, ctx.weak - 1),
      potionUsed: ctx.potionUsed, potion: ctx.potion, relic: ctx.relic, slyTotal: ctx.slyTotal, slyStreak: slyStreak2,
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
    if (usePotion(p)) {
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
  return [s.turn, s.hp, s.enemy, s.armor, s.vuln, s.weak, s.potionUsed, s.potion, s.relic, s.slyTotal, s.slyStreak, keyCounts(s.hand), keyCounts(s.draw), keyCounts(s.discard)].join("|");
}
function solve(state) {
  const key = stateKey(state);
  if (memo.has(key)) return memo.get(key);
  const result = bestFromCtx(startTurnContext(state));
  memo.set(key, result);
  return result;
}

function resultFor(deck, potion, relic) {
  const items = [];
  memo.clear();
  actionMemo.clear();
  for (const d of drawOutcomes(deck, DRAW)) {
    items.push([d.prob, solve({
      turn: 1, hp: PLAYER_HP, enemy: ENEMY_HP, armor: 0, vuln: 0, weak: 0,
      potionUsed: 0, potion, relic, slyTotal: 0, slyStreak: 0, hand: d.hand, draw: d.rest, discard: zero(),
    })]);
  }
  return merge(items);
}

function genDecksRec(i, left, cur, out) {
  if (i === N) {
    if (left === 0 && cur[PREPARED] + cur[SURVIVOR] <= 1) out.push(clone(cur));
    return;
  }
  for (let k = 0; k <= Math.min(POOL[i], left); k++) {
    cur[i] = k;
    genDecksRec(i + 1, left - k, cur, out);
  }
  cur[i] = 0;
}
function legalDecks() { const out = []; genDecksRec(0, 8, zero(), out); return out; }
function firstTurn(v) { for (let i = 0; i < 4; i++) if (v[i] > 1e-9) return i + 1; return 0; }
function pct(x) { return Math.round(x * 1000000) / 10000; }
function deckStr(deck) {
  const parts = [];
  for (let i = 0; i < N; i++) if (deck[i]) parts.push(CN[i] + (deck[i] > 1 ? ` x${deck[i]}` : ""));
  return parts.join("、");
}
function family(deck, potion, relic) {
  if (potion === SLY_BREW && relic === SHARP_DICE) return "药水骰子双狡黠快线";
  if (potion === SLY_BREW && relic === HOLLOW_AMULET) return "护符双狡黠稳线";
  if (deck[FINISHER] && deck[SHADOW] && (deck[PREPARED] || deck[SURVIVOR])) return "双狡黠终结线";
  if (deck[FEINT] || deck[QUICK] || deck[DAGGER]) return "伪狡黠干扰线";
  if (potion === VULN_POTION && deck[BASH]) return "易伤误导线";
  if (potion === FIRE_POTION) return "火焰补刀线";
  return "混合线";
}

const rows = [];
const decks = legalDecks();
let done = 0;
let seen = 0;
const totalBuilds = decks.length * 9;
outer:
for (const deck of decks) {
  for (let potion = 0; potion < 3; potion++) for (let relic = 0; relic < 3; relic++) {
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
rows.sort((a, b) => (b.success - a.success) || ((a.first_turn || 99) - (b.first_turn || 99)) || (b.kill_vector[0] - a.kill_vector[0]) || (b.kill_vector[1] - a.kill_vector[1]));
const bestByFirstTurn = {};
const bestByFamily = {};
for (const row of rows) {
  if (!(row.first_turn in bestByFirstTurn)) bestByFirstTurn[row.first_turn] = row;
  if (!(row.family in bestByFamily)) bestByFamily[row.family] = row;
}
const summary = {
  enemy_hp: ENEMY_HP,
  player_hp: PLAYER_HP,
  decision_model: "full_action_search",
  selection_model: "8 cards; Prepared and Survivor at most one",
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
if (!NO_WRITE) fs.writeFileSync(`difficulty3_double_sly_controlled_audit${suffix}.json`, JSON.stringify(summary, null, 2), "utf8");
console.log(`enemy_hp ${ENEMY_HP} player_hp ${PLAYER_HP}`);
console.log(`legal_deck_count ${summary.legal_deck_count}`);
console.log(`legal_build_count ${summary.legal_build_count}`);
console.log(`perfect_success_count ${summary.perfect_success_count}`);
console.log(`best ${rows[0].success} first ${rows[0].first_turn} ${rows[0].family} [${rows[0].kill_vector.join(",")}] ${rows[0].build_display}`);
console.log("best_by_first_turn");
for (const key of Object.keys(bestByFirstTurn).sort((a, b) => Number(a) - Number(b))) {
  const row = bestByFirstTurn[key];
  console.log(`${key} ${row.success} ${row.family} [${row.kill_vector.join(",")}] ${row.build_display}`);
}
console.log("best_by_family");
for (const [name, row] of Object.entries(bestByFamily)) console.log(`${name} ${row.success} first ${row.first_turn} [${row.kill_vector.join(",")}] ${row.build_display}`);
