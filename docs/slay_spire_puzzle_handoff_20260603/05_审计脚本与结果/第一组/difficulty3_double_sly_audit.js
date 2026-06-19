const fs = require("fs");

const N = 12;
const BASH = 0, PREPARED = 1, SURVIVOR = 2, FINISHER = 3, BACKSTAB = 4, DAGGER = 5;
const QUICK = 6, NEUTRALIZE = 7, STRIKE = 8, DEFEND = 9, SHADOW = 10, FEINT = 11;
const SLY_BREW = 0, VULN_POTION = 1, FIRE_POTION = 2;
const SHARP_DICE = 0, RETURN_HOLSTER = 1, HOLLOW_AMULET = 2;

const POOL = [1, 1, 1, 1, 2, 1, 2, 1, 3, 2, 2, 2];
const COST = [2, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0];
const CN = ["重击", "预备（改）", "生存者（改）", "终结（改）", "背刺（改，狡黠）", "匕首投掷（改，非狡黠）", "佯刺（改，非狡黠）", "中和", "打击", "防御", "影步（改，狡黠）", "虚刃（改，非狡黠）"];
const POTION_CN = ["狡黠药水", "破甲药水", "火焰药水"];
const RELIC_CN = ["锋利骰子", "折返皮套", "空心护符"];

let ENEMY_HP = Number(process.argv[2] || 78);
let PLAYER_HP = Number(process.argv[3] || 22);
let ACTIVE_CAP = 2;

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
function total(a) { let s = 0; for (const v of a) s += v; return s; }
function add(a, b) { const o = a.slice(); for (let i = 0; i < N; i++) o[i] += b[i]; return o; }
function subOne(a, c) { const o = a.slice(); o[c]--; return o; }
function addOne(a, c) { const o = a.slice(); o[c]++; return o; }
function keyCounts(a) { return a.join(","); }
function atk(base, vuln) { return vuln > 0 ? Math.floor(base * 3 / 2) : base; }
function weakd(base, weak) { return weak > 0 ? Math.floor(base * 3 / 4) : base; }
function isSly(c) { return c === BACKSTAB || c === SHADOW; }
function deal(ctx, amount) {
  const blocked = Math.min(ctx.armor, amount);
  ctx.armor -= blocked;
  ctx.enemy -= amount - blocked;
}
function vecFail() { return [0, 0, 0, 0, 1]; }
function vecKill(turn) { const v = [0, 0, 0, 0, 0]; v[turn - 1] = 1; return v; }
function success(v) { return v[0] + v[1] + v[2] + v[3]; }
function better(a, b) {
  if (!b) return true;
  const sa = success(a), sb = success(b);
  if (Math.abs(sa - sb) > 1e-12) return sa > sb;
  for (let i = 0; i < 4; i++) if (Math.abs(a[i] - b[i]) > 1e-12) return a[i] > b[i];
  return false;
}
function merge(items) {
  const out = [0, 0, 0, 0, 0];
  for (const [p, v] of items) for (let i = 0; i < 5; i++) out[i] += p * v[i];
  return out;
}

const drawMemo = new Map();
function drawOutcomes(cards, n, fixedDiscard = zero()) {
  const memoKey = keyCounts(cards) + "|" + n + "|" + keyCounts(fixedDiscard);
  if (drawMemo.has(memoKey)) return drawMemo.get(memoKey);
  const m = total(cards);
  if (n >= m) {
    const out = [{ hand: clone(cards), rest: zero(), discard: clone(fixedDiscard), prob: 1 }];
    drawMemo.set(memoKey, out);
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
    const mx = Math.min(cards[i], left);
    for (let k = 0; k <= mx; k++) {
      pick[i] = k;
      rec(i + 1, left - k, ways * C(cards[i], k));
    }
    pick[i] = 0;
  }
  rec(0, n, 1);
  drawMemo.set(memoKey, out);
  return out;
}

function nextDraws(draw, discard, n) {
  if (total(draw) >= n) return drawOutcomes(draw, n, discard);
  const fixed = clone(draw);
  const need = n - total(draw);
  if (total(discard) === 0) return [{ hand: fixed, rest: zero(), discard: zero(), prob: 1 }];
  return drawOutcomes(discard, need, zero()).map(d => ({ hand: add(fixed, d.hand), rest: d.rest, discard: zero(), prob: d.prob }));
}

function looseUpperBound(k) {
  const turnsLeft = 5 - k.turn;
  if (turnsLeft <= 0) return 0;
  const dmg = [12, 0, 0, 20, 9, 13, 12, 4, 9, 0, 7, 4];
  const cards = add(add(k.hand, k.draw), k.discard);
  const pool = [];
  for (let i = 0; i < N; i++) for (let j = 0; j < cards[i]; j++) pool.push(dmg[i]);
  pool.sort((a, b) => b - a);
  let active = 0;
  for (let i = 0; i < Math.min(pool.length, ACTIVE_CAP * turnsLeft); i++) active += pool[i];
  const sly = Math.min(cards[BACKSTAB] + cards[SHADOW], turnsLeft) * (k.relic === SHARP_DICE ? 19 : 15);
  const pot = (!k.potionUsed && k.potion === FIRE_POTION) ? 18 : 0;
  return active + sly + pot - k.armor;
}

function discardCard(ctx, card, relic) {
  const x = { ...ctx, hand: subOne(ctx.hand, card), discard: clone(ctx.discard) };
  if (!x.firstDiscard) {
    x.firstDiscard = 1;
    if (relic === RETURN_HOLSTER) x.energy += 1;
    else if (relic === HOLLOW_AMULET) x.block += 6;
  }
  if (isSly(card)) {
    let dmg = card === BACKSTAB ? 10 : 7;
    if (card === SHADOW) x.block += 4;
    if (card === BACKSTAB && relic === SHARP_DICE && !x.sharpUsed && x.block === 0) {
      dmg += 2;
      x.sharpUsed = 1;
    }
    x.sly += 1;
    deal(x, atk(dmg, x.vuln));
  } else {
    x.badDiscard = 1;
  }
  x.discard = addOne(x.discard, card);
  return x;
}

function playBash(ctx) {
  if (ctx.hand[BASH] === 0 || ctx.energy < 2 || ctx.played >= ACTIVE_CAP) return null;
  const x = { ...ctx, hand: subOne(ctx.hand, BASH), discard: clone(ctx.discard) };
  x.energy -= 2;
  x.played += 1;
  deal(x, atk(8, x.vuln));
  x.vuln += 2;
  x.discard = addOne(x.discard, BASH);
  return x;
}

function playEngine(ctx, engine, relic) {
  if (ctx.firstDiscard || ctx.hand[engine] === 0 || ctx.energy < COST[engine] || total(ctx.hand) <= 1 || ctx.played >= ACTIVE_CAP) return [];
  const base = { ...ctx, hand: subOne(ctx.hand, engine), discard: clone(ctx.discard) };
  base.energy -= COST[engine];
  base.played += 1;
  if (engine === SURVIVOR) base.block += 5;
  const out = [];
  for (let c = 0; c < N; c++) {
    if (base.hand[c] > 0) {
      const x = discardCard(base, c, relic);
      x.discard = addOne(x.discard, engine);
      out.push(x);
    }
  }
  return out;
}

function genSubsets(hand, energy, maxCards) {
  const out = [];
  const cur = zero();
  function rec(i, spent, used) {
    if (i === N) { out.push(clone(cur)); return; }
    if (i === PREPARED || i === SURVIVOR) { rec(i + 1, spent, used); return; }
    const mx = Math.min(hand[i], maxCards - used);
    for (let k = 0; k <= mx; k++) {
      const ns = spent + k * COST[i];
      if (ns <= energy) {
        cur[i] = k;
        rec(i + 1, ns, used + k);
      }
    }
    cur[i] = 0;
  }
  rec(0, 0, 0);
  return out;
}

function playSubset(ctx, sub) {
  if (ctx.played + total(sub) > ACTIVE_CAP) return null;
  const x = { ...ctx, hand: clone(ctx.hand), discard: clone(ctx.discard) };
  for (let c = 0; c < N; c++) {
    for (let k = 0; k < sub[c]; k++) {
      if (x.hand[c] === 0 || x.energy < COST[c]) return null;
      x.hand = subOne(x.hand, c);
      x.energy -= COST[c];
      x.played += 1;
      if (c === STRIKE) deal(x, atk(6, x.vuln));
      else if (c === DEFEND) x.block += 5;
      else if (c === BASH) { deal(x, atk(8, x.vuln)); x.vuln += 2; }
      else if (c === NEUTRALIZE) { deal(x, atk(3, x.vuln)); x.weak += 1; }
      else if (c === QUICK) deal(x, atk(8, x.vuln));
      else if (c === DAGGER) deal(x, atk(9, x.vuln));
      else if (c === FEINT) deal(x, atk(4, x.vuln));
      else if (c === SHADOW) { deal(x, atk(5, x.vuln)); x.block += 3; x.activeSly += 1; }
      else if (c === BACKSTAB) { deal(x, atk(6, x.vuln)); x.activeSly += 1; }
      else if (c === FINISHER) deal(x, atk(8 + 6 * x.sly, x.vuln));
      else return null;
      x.discard = addOne(x.discard, c);
    }
  }
  return x;
}

function endContexts(k) {
  const base = {
    hand: clone(k.hand), draw: clone(k.draw), discard: clone(k.discard),
    energy: 3, block: 0, enemy: k.enemy, armor: k.armor, vuln: k.vuln, weak: k.weak,
    potionUsed: k.potionUsed, sly: 0, firstDiscard: 0, sharpUsed: 0, played: 0,
    activeSly: 0, badDiscard: 0,
  };
  const starts = [base];
  if (!k.potionUsed) {
    if (k.potion === FIRE_POTION) {
      const x = { ...base, hand: clone(base.hand), draw: clone(base.draw), discard: clone(base.discard), potionUsed: 1 };
      deal(x, 18);
      starts.push(x);
    } else if (k.potion === VULN_POTION) {
      const x = { ...base, hand: clone(base.hand), draw: clone(base.draw), discard: clone(base.discard), potionUsed: 1, vuln: base.vuln + 2 };
      starts.push(x);
    } else if (k.potion === SLY_BREW) {
      const b = { ...base, hand: clone(base.hand), draw: clone(base.draw), discard: clone(base.discard), potionUsed: 1 };
      for (let c = 0; c < N; c++) if (b.hand[c] > 0) starts.push(discardCard(b, c, k.relic));
    }
  }
  const finals = [];
  for (const s of starts) {
    const pre = [s];
    const pb = playBash(s);
    if (pb) pre.push(pb);
    for (const ctx1 of pre) {
      const states = [ctx1, ...playEngine(ctx1, PREPARED, k.relic), ...playEngine(ctx1, SURVIVOR, k.relic)];
      for (const ctx2 of states) {
        const post = [ctx2];
        const po = playBash(ctx2);
        if (po) post.push(po);
        for (const ctx3 of post) {
          for (const sub of genSubsets(ctx3.hand, ctx3.energy, ACTIVE_CAP - ctx3.played)) {
            const end = playSubset(ctx3, sub);
            if (end) finals.push(end);
          }
        }
      }
    }
  }
  const seen = new Set();
  const uniq = [];
  for (const c of finals) {
    const key = [keyCounts(c.hand), keyCounts(c.draw), keyCounts(c.discard), c.energy, c.block, c.enemy, c.armor, c.vuln, c.weak, c.potionUsed, c.sly > 0 ? 1 : 0, c.activeSly, c.badDiscard].join("|");
    if (!seen.has(key)) { seen.add(key); uniq.push(c); }
  }
  return uniq;
}

const memo = new Map();
function keyState(k) {
  return [k.turn, k.hp, k.enemy, k.armor, k.vuln, k.weak, k.potionUsed, k.potion, k.relic, keyCounts(k.hand), keyCounts(k.draw), keyCounts(k.discard)].join("|");
}
function solve(k) {
  if (k.enemy > looseUpperBound(k)) return vecFail();
  const key = keyState(k);
  if (memo.has(key)) return memo.get(key);
  let best = null;
  for (const ctx of endContexts(k)) {
    let cand;
    if (ctx.enemy <= 0) cand = vecKill(k.turn);
    else if (k.turn >= 4) cand = vecFail();
    else {
      let incoming = 0, armorGain = 0;
      if (k.turn === 1) { incoming = weakd(8, ctx.weak); if (ctx.sly === 0) armorGain = 14; }
      else if (k.turn === 2) { incoming = weakd(22, ctx.weak); if (ctx.sly === 0) armorGain = 20; }
      else if (k.turn === 3) { incoming = weakd(22, ctx.weak); if (ctx.sly === 0) armorGain = 8; }
      armorGain += 8 * ctx.activeSly;
      if (ctx.badDiscard) armorGain += 6;
      const hp2 = k.hp - Math.max(0, incoming - ctx.block);
      if (hp2 <= 0) cand = vecFail();
      else {
        const discard2 = add(ctx.discard, ctx.hand);
        const items = [];
        for (const d of nextDraws(ctx.draw, discard2, 5)) {
          items.push([d.prob, solve({ turn: k.turn + 1, hp: hp2, enemy: ctx.enemy, armor: ctx.armor + armorGain, vuln: Math.max(0, ctx.vuln - 1), weak: Math.max(0, ctx.weak - 1), potionUsed: ctx.potionUsed, potion: k.potion, relic: k.relic, hand: d.hand, draw: d.rest, discard: d.discard })]);
        }
        cand = merge(items);
      }
    }
    if (better(cand, best)) best = cand;
  }
  best ||= vecFail();
  memo.set(key, best);
  return best;
}

function resultFor(deck, potion, relic) {
  const items = [];
  for (const d of drawOutcomes(deck, 5)) {
    memo.clear();
    items.push([d.prob, solve({ turn: 1, hp: PLAYER_HP, enemy: ENEMY_HP, armor: 0, vuln: 0, weak: 0, potionUsed: 0, potion, relic, hand: d.hand, draw: d.rest, discard: zero() })]);
  }
  return merge(items);
}

function genDecksRec(i, left, cur, out) {
  if (i === N) {
    if (left === 0 && cur[BACKSTAB] === 2 && cur[FINISHER] === 1 && cur[SHADOW] >= 1 && cur[PREPARED] + cur[SURVIVOR] === 1) out.push(clone(cur));
    return;
  }
  for (let k = 0; k <= Math.min(POOL[i], left); k++) {
    cur[i] = k;
    genDecksRec(i + 1, left - k, cur, out);
  }
  cur[i] = 0;
}
function decks() { const out = []; genDecksRec(0, 8, zero(), out); return out; }
function deckStr(d) {
  const parts = [];
  for (let i = 0; i < N; i++) if (d[i]) parts.push(CN[i] + (d[i] > 1 ? ` x${d[i]}` : ""));
  return parts.join("、");
}
function firstTurn(v) { for (let i = 0; i < 4; i++) if (v[i] > 1e-9) return i + 1; return 0; }
function family(d, p, r) {
  if (p === SLY_BREW && r === SHARP_DICE) return "药水骰子双狡黠快线";
  if (r === SHARP_DICE) return "锋利骰子双狡黠爆发线";
  if (d[FINISHER] && d[SHADOW] && (d[PREPARED] || d[SURVIVOR])) return "双狡黠终结线";
  if (d[DAGGER] || d[QUICK] || d[FEINT]) return "伪狡黠干扰线";
  if (d[SURVIVOR] && r === HOLLOW_AMULET) return "弃牌格挡稳线";
  if (d[BASH] && p === VULN_POTION) return "双易伤攻击线";
  if (p === FIRE_POTION) return "火焰补刀线";
  return "混合线";
}
function pct(x) { return Math.round(x * 1000000) / 10000; }

const ds = decks();
const rows = [];
let done = 0;
for (const d of ds) {
  for (let p = 0; p < 3; p++) for (let r = 0; r < 3; r++) {
    const v = resultFor(d, p, r);
    rows.push({ success: pct(success(v)), first_turn: firstTurn(v), family: family(d, p, r), build_display: `${deckStr(d)}；${POTION_CN[p]}；${RELIC_CN[r]}`, kill_vector: v.map(pct) });
    done++;
    if (done % 25 === 0 || done === ds.length * 9) {
      const best = rows.reduce((a, b) => b.success > a.success ? b : a, rows[0]);
      console.log(`audited ${done}/${ds.length * 9} best ${best.success} first ${best.first_turn} ${best.family}`);
    }
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
  legal_deck_count: ds.length,
  legal_build_count: rows.length,
  perfect_success_count: rows.filter(r => r.success >= 99.9999).length,
  top30: rows.slice(0, 30),
  best_by_first_turn: bestByFirstTurn,
  best_by_family: bestByFamily,
};
fs.writeFileSync("difficulty3_double_sly_audit.json", JSON.stringify(summary, null, 2), "utf8");
console.log(`enemy_hp ${ENEMY_HP} player_hp ${PLAYER_HP}`);
console.log(`legal_deck_count ${summary.legal_deck_count}`);
console.log(`legal_build_count ${summary.legal_build_count}`);
console.log(`perfect_success_count ${summary.perfect_success_count}`);
console.log("top30");
for (const r of summary.top30) console.log(`${r.success} first ${r.first_turn} ${r.family} [${r.kill_vector.join(",")}] ${r.build_display}`);
console.log("best_by_first_turn");
for (const k of Object.keys(bestByFirstTurn).sort((a, b) => Number(a) - Number(b))) {
  const r = bestByFirstTurn[k];
  console.log(`${k} ${r.success} ${r.family} [${r.kill_vector.join(",")}] ${r.build_display}`);
}
console.log("best_by_family");
for (const [name, r] of Object.entries(bestByFamily)) console.log(`${name} ${r.success} first ${r.first_turn} [${r.kill_vector.join(",")}] ${r.build_display}`);
