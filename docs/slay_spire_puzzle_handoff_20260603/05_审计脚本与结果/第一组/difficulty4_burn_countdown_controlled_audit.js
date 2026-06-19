const fs = require("fs");

const N = 14;
const BASH = 0, UPPERCUT = 1, NEUTRALIZE = 2, CARNAGE = 3, BURNING = 4, TRUE_GRIT = 5, SURVIVOR = 6;
const DAGGER = 7, QUICK = 8, BALL = 9, IRON = 10, STRIKE = 11, DEFEND = 12, BURN = 13;
const FIRE = 0, CLARITY = 1, GHOST = 2;

const POOL = [1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 1, 3, 2, 0];
const COST = [2, 2, 0, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 99];
const CN = ["重击", "上勾拳", "中和", "残杀（改）", "燃烧契约（改）", "坚毅（改）", "生存者（改）", "投掷匕首（改）", "快斩（改）", "球状闪电（改）", "铁斩波", "打击", "防御", "灼伤"];
const POTION_CN = ["火焰药水", "清醒药水", "幽灵药水"];
const CARD_ORDER = Array.from({ length: N - 1 }, (_, i) => i);

const ENEMY_HP = Number(process.argv[2] || 112);
const PLAYER_HP = Number(process.argv[3] || 26);
const FIRE_DMG = Number(process.argv[4] || 16);
const CLARITY_DMG = Number(process.argv[5] || 13);
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
const DMG = [7, 13, 18, 25, 35];
const ARMOR_GAIN = [0, 8, 0, 12, 0];
const BURN_GAIN = [1, 0, 1, 1, 0];
const OVERHEAT_LOSS = 32;

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
function deal(ctx, amount) { const b = Math.min(ctx.armor, amount); ctx.armor -= b; ctx.enemy -= amount - b; }
function success(v) { return v.slice(0, 5).reduce((a, b) => a + b, 0); }
function fail() { return [0, 0, 0, 0, 0, 1]; }
function kill(t) { const v = [0, 0, 0, 0, 0, 0]; v[t - 1] = 1; return v; }
function merge(items) {
  const out = [0, 0, 0, 0, 0, 0];
  for (const [p, v] of items) for (let i = 0; i < 6; i++) out[i] += p * v[i];
  return out;
}
function better(a, b) {
  if (!b) return true;
  if (Math.abs(success(a) - success(b)) > 1e-12) return success(a) > success(b);
  for (let i = 0; i < 5; i++) if (Math.abs(a[i] - b[i]) > 1e-12) return a[i] > b[i];
  return false;
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

function exhaustBurn(ctx) {
  if (ctx.hand[BURN] > 0) {
    ctx.hand = subOne(ctx.hand, BURN);
    return true;
  }
  return false;
}
function playCard(ctx, card) {
  if (ctx.hand[card] <= 0 || ctx.energy < COST[card]) return false;
  ctx.hand = subOne(ctx.hand, card);
  ctx.energy -= COST[card];
  if (card === BASH) { deal(ctx, atk(8, ctx.vuln)); ctx.vuln += 2; }
  else if (card === UPPERCUT) { deal(ctx, atk(13, ctx.vuln)); ctx.vuln += 1; ctx.weak += 1; }
  else if (card === NEUTRALIZE) { deal(ctx, atk(3, ctx.vuln)); ctx.weak += 1; }
  else if (card === CARNAGE) deal(ctx, atk(18, ctx.vuln));
  else if (card === BURNING) { const burned = exhaustBurn(ctx); deal(ctx, atk(burned ? 14 : 9, ctx.vuln)); }
  else if (card === TRUE_GRIT) { ctx.block += 7; exhaustBurn(ctx); }
  else if (card === SURVIVOR) { ctx.block += 7; exhaustBurn(ctx); }
  else if (card === DAGGER) deal(ctx, atk(9, ctx.vuln));
  else if (card === QUICK) deal(ctx, atk(8, ctx.vuln));
  else if (card === BALL) deal(ctx, atk(7, ctx.vuln));
  else if (card === IRON) { deal(ctx, atk(5, ctx.vuln)); ctx.block += 5; }
  else if (card === STRIKE) deal(ctx, atk(6, ctx.vuln));
  else if (card === DEFEND) ctx.block += 5;
  else return false;
  ctx.discard = addOne(ctx.discard, card);
  return true;
}
function startTurnContext(state) {
  return {
    turn: state.turn,
    hand: clone(state.hand), draw: clone(state.draw), discard: clone(state.discard),
    hp: state.hp, enemy: state.enemy, armor: state.armor, vuln: state.vuln, weak: state.weak,
    energy: 3, block: 0, potionUsed: state.potionUsed, potion: state.potion, ghost: 0, burnClock: state.burnClock,
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
    ctx.turn, ctx.hp, ctx.enemy, ctx.armor, ctx.vuln, ctx.weak, ctx.energy, ctx.block,
    ctx.potionUsed, ctx.potion, ctx.ghost, ctx.burnClock,
    keyCounts(ctx.hand), keyCounts(ctx.draw), keyCounts(ctx.discard),
  ].join("|");
}

function usePotion(ctx) {
  if (ctx.potionUsed) return false;
  if (ctx.potion === FIRE) {
    deal(ctx, FIRE_DMG);
    ctx.potionUsed = 1;
    return true;
  }
  if (ctx.potion === CLARITY && ctx.hand[BURN] > 0) {
    const n = Math.min(2, ctx.hand[BURN]);
    ctx.hand[BURN] -= n;
    deal(ctx, CLARITY_DMG * n);
    ctx.potionUsed = 1;
    return true;
  }
  if (ctx.potion === GHOST && ctx.turn >= 4) {
    ctx.ghost = 1;
    ctx.potionUsed = 1;
    return true;
  }
  return false;
}

function endTurnValue(ctx) {
  if (ctx.enemy <= 0) return kill(ctx.turn);
  if (ctx.turn >= 5) return fail();

  let hp2 = ctx.hp - 3 * ctx.hand[BURN];
  let incoming = weakd(DMG[ctx.turn - 1], ctx.weak);
  if (ctx.ghost) incoming = Math.floor(incoming / 2);
  hp2 -= Math.max(0, incoming - ctx.block);
  if (hp2 <= 0) return fail();

  let discard2 = add(ctx.discard, ctx.hand);
  for (let i = 0; i < BURN_GAIN[ctx.turn - 1]; i++) discard2 = addOne(discard2, BURN);
  const burnClock2 = ctx.burnClock + BURN_GAIN[ctx.turn - 1];
  const enemy2 = (ctx.turn + 1 === 5 && burnClock2 >= 3) ? ctx.enemy - OVERHEAT_LOSS : ctx.enemy;
  const items = [];
  for (const d of nextDraws(ctx.draw, discard2, DRAW)) {
    items.push([d.prob, solve({
      turn: ctx.turn + 1, hp: hp2, enemy: enemy2, armor: ctx.armor + ARMOR_GAIN[ctx.turn - 1],
      vuln: Math.max(0, ctx.vuln - 1), weak: Math.max(0, ctx.weak - 1),
      potionUsed: ctx.potionUsed, potion: ctx.potion, burnClock: burnClock2, hand: d.hand, draw: d.rest, discard: d.discard,
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
    if (ctx.hand[card] <= 0 || ctx.energy < COST[card]) continue;
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
  return [s.turn, s.hp, s.enemy, s.armor, s.vuln, s.weak, s.potionUsed, s.potion, s.burnClock, keyCounts(s.hand), keyCounts(s.draw), keyCounts(s.discard)].join("|");
}
function solve(state) {
  if (state.enemy <= 0) return kill(state.turn);
  if (state.hp <= 0) return fail();
  const key = stateKey(state);
  if (memo.has(key)) return memo.get(key);
  const result = bestFromCtx(startTurnContext(state));
  memo.set(key, result);
  return result;
}
function resultFor(deck, potion) {
  memo.clear();
  actionMemo.clear();
  const items = [];
  for (const d of drawOutcomes(deck, DRAW)) {
    items.push([d.prob, solve({ turn: 1, hp: PLAYER_HP, enemy: ENEMY_HP, armor: 0, vuln: 0, weak: 0, potionUsed: 0, potion, burnClock: 0, hand: d.hand, draw: d.rest, discard: zero() })]);
  }
  return merge(items);
}
function genDecksRec(i, left, cur, out) {
  if (i === N) {
    if (left === 0 && cur[CARNAGE] === 1 && cur[BURNING] === 1 && cur[NEUTRALIZE] + cur[IRON] === 1 && cur[QUICK] + cur[BALL] === 2) out.push(clone(cur));
    return;
  }
  for (let k = 0; k <= Math.min(POOL[i], left); k++) {
    cur[i] = k;
    genDecksRec(i + 1, left - k, cur, out);
  }
  cur[i] = 0;
}
function legalDecks() { const out = []; genDecksRec(0, 9, zero(), out); return out; }
function firstTurn(v) { for (let i = 0; i < 5; i++) if (v[i] > 1e-9) return i + 1; return 0; }
function pct(x) { return Math.round(x * 1000000) / 10000; }
function deckStr(deck) {
  const parts = [];
  for (let i = 0; i < N - 1; i++) if (deck[i]) parts.push(CN[i] + (deck[i] > 1 ? ` x${deck[i]}` : ""));
  return parts.join("、");
}
function family(deck, potion) {
  if (potion === GHOST) return "幽灵过热线";
  if (potion === FIRE && (deck[BASH] || deck[UPPERCUT])) return "火焰易伤快线";
  if (potion === CLARITY || deck[TRUE_GRIT] || deck[SURVIVOR]) return "清理灼伤线";
  if (deck[QUICK] + deck[BALL] >= 2) return "小伤堆叠线";
  return "混合线";
}

const decks = legalDecks();
const rows = [];
let done = 0;
let seen = 0;
const totalBuilds = decks.length * 3;
outer:
for (const deck of decks) {
  for (let potion = 0; potion < 3; potion++) {
    if (seen++ < SKIP_BUILDS) continue;
    const vec = resultFor(deck, potion);
    rows.push({ success: pct(success(vec)), first_turn: firstTurn(vec), family: family(deck, potion), build_display: `${deckStr(deck)}；${POTION_CN[potion]}`, kill_vector: vec.map(pct) });
    done++;
    if (PROGRESS_EVERY && done % PROGRESS_EVERY === 0) {
      const best = rows.reduce((a, b) => (a.success > b.success ? a : b));
      console.log(`audited ${done}/${totalBuilds} best_so_far ${best.success} first ${best.first_turn} ${best.family}`);
    }
    if (MAX_BUILDS && done >= MAX_BUILDS) break outer;
  }
}
rows.sort((a, b) => (b.success - a.success) || ((a.first_turn || 99) - (b.first_turn || 99)) || (b.kill_vector[1] - a.kill_vector[1]) || (b.kill_vector[2] - a.kill_vector[2]));
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
  clarity_damage_per_burn: CLARITY_DMG,
  overheat_loss: OVERHEAT_LOSS,
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
if (!NO_WRITE) fs.writeFileSync(`difficulty4_burn_countdown_controlled_audit${suffix}.json`, JSON.stringify(summary, null, 2), "utf8");
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
for (const [name, r] of Object.entries(bestByFamily)) console.log(`${name} ${r.success} first ${r.first_turn} [${r.kill_vector.join(",")}] ${r.build_display}`);
