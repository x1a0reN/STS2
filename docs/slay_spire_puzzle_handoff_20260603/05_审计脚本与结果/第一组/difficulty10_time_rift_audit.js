const fs = require("fs");

const C = {
  TIME_SIGIL: 0,
  CHARGE: 1,
  RIFT: 2,
  ECHO_STRIKE: 3,
  ECHO_FORM: 4,
  DELAYED: 5,
  OVERLOAD: 6,
  VENT: 7,
  BARRIER: 8,
  FOCUS: 9,
  FINISHER: 10,
  MIRROR: 11,
  BURN: 12,
  COOL: 13,
  NULL: 14,
  SPIKE: 15,
  STRIKE: 16,
  DEFEND: 17,
};

const P = { TIME: 0, ECHO: 1, SHATTER: 2 };
const R = { CHRONO: 0, RESONATOR: 1, PRISM: 2 };

const CN = [
  "时间刻印（改）",
  "充能姿态（改）",
  "裂隙标记（改）",
  "回声打击（改）",
  "回声形态（改）",
  "延迟爆破（改）",
  "过载射线（改）",
  "排热（改）",
  "相位屏障（改）",
  "聚焦校准（改）",
  "终局指令（改）",
  "镜像预演（改）",
  "燃烧射击（改）",
  "冷却回路（改）",
  "空转程序（改）",
  "尖刺标记（改）",
  "打击",
  "防御",
];
const POTION_CN = ["时间药水", "回声药水", "碎甲药水"];
const RELIC_CN = ["怀表核心", "谐振器", "棱镜碎片"];

const POOL = [1, 3, 3, 3, 2, 2, 2, 2, 3, 2, 1, 2, 3, 2, 4, 3, 4, 4];
const COST = [1, 0, 1, 1, 1, 2, 2, 0, 1, 1, 2, 1, 1, 1, 1, 1, 1, 1];
const ENEMY_DMG = [18, 28, 38, 52];
const ENEMY_ARMOR = [0, 32, 0, 0];
const LIMIT = 4;
const DRAW_COUNT = 2;
const PLAY_LIMIT = 2;

let ENEMY_HP = Number(process.argv[2] || 78);
let PLAYER_HP = Number(process.argv[3] || 40);
function optionNumber(argIndex, envName, fallback = 0) {
  const raw = process.argv[argIndex] ?? process.env[envName] ?? fallback;
  const n = Number(raw);
  return Number.isFinite(n) ? n : fallback;
}
const maxBuilds = optionNumber(4, "GONGDOU_AUDIT_MAX_BUILDS");
const skipBuilds = optionNumber(5, "GONGDOU_AUDIT_SKIP_BUILDS");
const noWrite = process.env.GONGDOU_AUDIT_NO_WRITE === "1";

function total(a) { return a.reduce((s, v) => s + v, 0); }
function clone(a) { return a.slice(); }
function keyOf(o) { return JSON.stringify(o); }
function success(v) { return v.slice(0, LIMIT).reduce((s, x) => s + x, 0); }
function kill(turn) { const v = Array(LIMIT + 1).fill(0); v[turn - 1] = 1; return v; }
function fail() { const v = Array(LIMIT + 1).fill(0); v[LIMIT] = 1; return v; }
function addVec(items) {
  const out = Array(LIMIT + 1).fill(0);
  for (const [p, v] of items) for (let i = 0; i < out.length; i++) out[i] += p * v[i];
  return out;
}
function firstTurn(v) {
  for (let i = 0; i < LIMIT; i++) if (v[i] > 1e-12) return i + 1;
  return 0;
}
function better(a, b) {
  if (!b) return true;
  const sa = success(a), sb = success(b);
  if (Math.abs(sa - sb) > 1e-12) return sa > sb;
  for (let i = 0; i < LIMIT; i++) if (Math.abs(a[i] - b[i]) > 1e-12) return a[i] > b[i];
  return false;
}

const combMemo = new Map();
function comb(n, k) {
  const kk = `${n},${k}`;
  if (combMemo.has(kk)) return combMemo.get(kk);
  if (k < 0 || k > n) return 0;
  if (k === 0 || k === n) return 1;
  let r = 1;
  for (let i = 1; i <= k; i++) r = (r * (n - k + i)) / i;
  combMemo.set(kk, r);
  return r;
}

const drawMemo = new Map();
function drawOutcomes(deck, n) {
  const k = `${deck.join(",")}|${n}`;
  if (drawMemo.has(k)) return drawMemo.get(k);
  const m = total(deck);
  const denom = comb(m, n);
  const pick = Array(18).fill(0);
  const out = [];
  function rec(i, left, ways) {
    if (i === 18) {
      if (left === 0) out.push({ hand: clone(pick), prob: ways / denom });
      return;
    }
    const mx = Math.min(deck[i], left);
    for (let c = 0; c <= mx; c++) {
      pick[i] = c;
      rec(i + 1, left - c, ways * comb(deck[i], c));
    }
    pick[i] = 0;
  }
  rec(0, n, 1);
  drawMemo.set(k, out);
  return out;
}

function deal(ctx, amount, attack = true) {
  let raw = amount;
  if (ctx.echo > 0 && attack) {
    raw *= 2;
    ctx.echo -= 1;
  }
  if (ctx.relic === R.PRISM && attack && ctx.mark >= 4) raw += 4;
  const blocked = Math.min(ctx.armor, raw);
  ctx.armor -= blocked;
  ctx.enemy -= raw - blocked;
  enforcePhaseGate(ctx);
}

function minKillTurn(ctx) {
  if (ctx.potion === P.TIME) return 1;
  if (ctx.potion === P.ECHO) return 2;
  return 3;
}

function enforcePhaseGate(ctx) {
  if (ctx.enemy <= 0 && ctx.turn < minKillTurn(ctx)) {
    ctx.enemy = 1;
    ctx.armor = 0;
  }
}

function playCard(input, card) {
  if (input.hand[card] <= 0 || input.energy < COST[card] || input.plays >= PLAY_LIMIT) return null;
  const c = {
    ...input,
    hand: clone(input.hand),
  };
  c.hand[card] -= 1;
  c.energy -= COST[card];
  c.plays += 1;
  switch (card) {
    case C.TIME_SIGIL:
      c.charge += 3;
      break;
    case C.CHARGE:
      c.charge += 1;
      c.block += 3;
      break;
    case C.RIFT:
      c.mark += c.relic === R.PRISM ? 3 : 2;
      break;
    case C.ECHO_STRIKE:
      deal(c, 7 + c.charge * 2 + c.mark, true);
      break;
    case C.ECHO_FORM:
      c.echo += 1;
      break;
    case C.DELAYED:
      c.pending += 18 + c.mark * 4 + (c.relic === R.RESONATOR && !c.resonatorUsed ? 12 : 0);
      if (c.relic === R.RESONATOR) c.resonatorUsed = 1;
      break;
    case C.OVERLOAD:
      deal(c, 12 + c.charge * 3, true);
      c.heat += 2;
      break;
    case C.VENT:
      c.heat = Math.max(0, c.heat - 2);
      c.block += 6;
      break;
    case C.BARRIER:
      c.block += 8 + Math.min(6, c.charge);
      break;
    case C.FOCUS:
      c.charge += 2;
      c.mark += c.relic === R.PRISM ? 2 : 1;
      break;
    case C.FINISHER:
      if (c.enemy <= 32 + c.mark * 4) {
        c.enemy = 0;
        enforcePhaseGate(c);
      }
      else deal(c, 8 + c.mark, true);
      break;
    case C.MIRROR:
      c.echo += 1;
      c.charge += 1;
      break;
    case C.BURN:
      deal(c, 6 + c.mark, true);
      c.heat += 1;
      break;
    case C.COOL:
      c.block += 5;
      c.charge += 1;
      c.heat = Math.max(0, c.heat - 1);
      break;
    case C.NULL:
      c.block += 2;
      if (c.heat > 0) c.mark = Math.max(0, c.mark - 1);
      break;
    case C.SPIKE:
      c.mark += c.relic === R.PRISM ? 2 : 1;
      deal(c, 5, true);
      break;
    case C.STRIKE:
      deal(c, 6, true);
      break;
    case C.DEFEND:
      c.block += 5;
      break;
  }
  return c;
}

function endContexts(k) {
  let base = {
    hand: clone(k.hand),
    enemy: k.enemy,
    armor: k.armor,
    hp: k.hp,
    turn: k.turn,
    charge: k.charge + (k.relic === R.CHRONO ? 1 : 0),
    mark: k.mark,
    heat: k.heat,
    pending: k.pending,
    potionUsed: k.potionUsed,
    potion: k.potion,
    relic: k.relic,
    echo: 0,
    resonatorUsed: k.resonatorUsed,
    energy: 3,
    block: 0,
    plays: 0,
  };
  if (base.pending > 0) {
    deal(base, base.pending, false);
    base.pending = 0;
  }
  const starts = [base];
  if (!base.potionUsed) {
    const p = { ...base, hand: clone(base.hand), potionUsed: 1 };
    if (k.potion === P.TIME) {
      p.charge += 6;
      p.echo += 1;
    }
    else if (k.potion === P.ECHO) p.echo += 2;
    else if (k.potion === P.SHATTER) {
      p.armor = Math.max(0, p.armor - 30);
      p.mark += 2;
    }
    starts.push(p);
  }
  const finals = [];
  const seen = new Set();

  function pushFinal(c) {
    const key = keyOf([c.hand, c.enemy, c.armor, c.hp, c.charge, c.mark, c.heat, c.pending, c.potionUsed, c.relic, c.echo, c.resonatorUsed, c.energy, c.block, c.plays]);
    if (seen.has(key)) return;
    seen.add(key);
    finals.push(c);
  }

  function bestFromCtx(ctx) {
    pushFinal(ctx);
    if (ctx.enemy <= 0 || ctx.plays >= PLAY_LIMIT) return;
    for (let card = 0; card < 18; card++) {
      const out = playCard(ctx, card);
      if (out) bestFromCtx(out);
    }
  }

  for (const s of starts) bestFromCtx({ ...s, hand: clone(s.hand) });
  return finals;
}

const memo = new Map();
let activeDeck = null;
function solve(k) {
  if (k.hp <= 0) return fail();
  if (k.enemy <= 0) return kill(k.turn);
  const kk = keyOf(k);
  if (memo.has(kk)) return memo.get(kk);
  let best = null;
  for (const c of endContexts(k)) {
    let cand;
    if (c.enemy <= 0) cand = kill(k.turn);
    else {
      const heatLoss = c.heat * 4;
      const hp2 = k.hp - Math.max(0, ENEMY_DMG[k.turn - 1] + heatLoss - c.block);
      const armor2 = c.armor + ENEMY_ARMOR[k.turn - 1];
      const mark2 = Math.max(0, c.mark - (k.turn === 3 ? 3 : 0));
      if (k.turn >= LIMIT || hp2 <= 0) cand = fail();
      else {
        const items = [];
        for (const d of drawOutcomes(activeDeck, DRAW_COUNT)) {
          items.push([d.prob, solve({
            turn: k.turn + 1,
            hp: hp2,
            enemy: c.enemy,
            armor: armor2,
            charge: c.charge,
            mark: mark2,
            heat: c.heat,
            pending: c.pending,
            potionUsed: c.potionUsed,
            potion: k.potion,
            relic: k.relic,
            resonatorUsed: c.resonatorUsed,
            hand: d.hand,
          })]);
        }
        cand = addVec(items);
      }
    }
    if (better(cand, best)) best = cand;
  }
  if (!best) best = fail();
  memo.set(kk, best);
  return best;
}

function resultFor(deck, potion, relic) {
  activeDeck = deck;
  memo.clear();
  const items = [];
  for (const d of drawOutcomes(deck, DRAW_COUNT)) {
    items.push([d.prob, solve({
      turn: 1,
      hp: PLAYER_HP,
      enemy: ENEMY_HP,
      armor: 0,
      charge: 0,
      mark: 0,
      heat: 0,
      pending: 0,
      potionUsed: 0,
      potion,
      relic,
      resonatorUsed: 0,
      hand: d.hand,
    })]);
  }
  return addVec(items);
}

function legal(deck) {
  const charge = deck[C.TIME_SIGIL] + deck[C.CHARGE] + deck[C.FOCUS] + deck[C.MIRROR] + deck[C.COOL];
  const mark = deck[C.RIFT] + deck[C.FOCUS] + deck[C.SPIKE];
  const attack = deck[C.ECHO_STRIKE] + deck[C.DELAYED] + deck[C.OVERLOAD] + deck[C.FINISHER] + deck[C.BURN] + deck[C.SPIKE] + deck[C.STRIKE];
  const defense = deck[C.CHARGE] + deck[C.VENT] + deck[C.BARRIER] + deck[C.COOL] + deck[C.NULL] + deck[C.DEFEND];
  const traps = deck[C.NULL] + deck[C.STRIKE] + deck[C.DEFEND];
  return total(deck) === 16 &&
    deck[C.TIME_SIGIL] === 1 &&
    deck[C.ECHO_FORM] === 1 &&
    deck[C.DELAYED] === 1 &&
    deck[C.FOCUS] === 2 &&
    deck[C.FINISHER] === 1 &&
    deck[C.SPIKE] >= 2 &&
    charge >= 5 &&
    mark >= 4 &&
    attack >= 7 &&
    defense >= 5 &&
    traps >= 4;
}

function genDecks(i = 0, left = 0, cur = Array(18).fill(0), out = []) {
  if (!genDecks.suffixMax) {
    genDecks.suffixMax = Array(19).fill(0);
    for (let j = 17; j >= 0; j--) genDecks.suffixMax[j] = genDecks.suffixMax[j + 1] + POOL[j];
  }
  if (left > 16 || left + genDecks.suffixMax[i] < 16) return out;
  if (i > C.TIME_SIGIL && cur[C.TIME_SIGIL] !== 1) return out;
  if (i > C.FINISHER && cur[C.FINISHER] !== 1) return out;
  if (i === 18) {
    if (left === 16 && legal(cur)) out.push(clone(cur));
    return out;
  }
  for (let c = 0; c <= POOL[i] && left + c <= 16; c++) {
    cur[i] = c;
    genDecks(i + 1, left + c, cur, out);
  }
  cur[i] = 0;
  return out;
}

function deckStr(deck) {
  const parts = [];
  for (let i = 0; i < deck.length; i++) if (deck[i]) parts.push(`${CN[i]}${deck[i] > 1 ? ` x${deck[i]}` : ""}`);
  return parts.join("、");
}

function family(deck, potion, relic) {
  if (potion === P.ECHO && deck[C.ECHO_FORM] && deck[C.ECHO_STRIKE] >= 2) return "回声复制爆发线";
  if (deck[C.DELAYED] >= 2 && relic === R.RESONATOR) return "谐振延迟爆破线";
  if (deck[C.FINISHER] && potion === P.SHATTER) return "碎甲终局线";
  if (deck[C.OVERLOAD] >= 2 && deck[C.VENT] >= 1) return "过载排热线";
  if (relic === R.CHRONO && deck[C.CHARGE] >= 2) return "怀表充能线";
  return "混合线";
}

const decks = genDecks();
console.log(`generated_legal_decks ${decks.length}`);
const rows = [];
let seen = 0;
let done = 0;
const totalBuilds = decks.length * 9;
for (const deck of decks) {
  for (let potion = 0; potion < 3; potion++) {
    for (let relic = 0; relic < 3; relic++) {
      if (seen++ < skipBuilds) continue;
      const vec = resultFor(deck, potion, relic);
      rows.push({
        deck,
        potion,
        relic,
        first_turn: firstTurn(vec),
        kill_vector: vec.map((x) => x * 100),
        success: success(vec) * 100,
        family: family(deck, potion, relic),
        build_display: `${deckStr(deck)}；${POTION_CN[potion]}；${RELIC_CN[relic]}`,
      });
      done++;
      if (done % 20 === 0) {
        const best = rows.reduce((a, b) => (a.success > b.success ? a : b));
        console.log(`audited ${done}/${totalBuilds} best_so_far ${best.success.toFixed(4)} first ${best.first_turn} ${best.family}`);
      }
      if (maxBuilds && done >= maxBuilds) break;
    }
    if (maxBuilds && done >= maxBuilds) break;
  }
  if (maxBuilds && done >= maxBuilds) break;
}

rows.sort((a, b) => b.success - a.success || (a.first_turn || 99) - (b.first_turn || 99));
const perfect = rows.filter((r) => r.success > 99.999999).length;
const bestByFirst = {};
const bestByFamily = {};
for (const r of rows) {
  const t = String(r.first_turn);
  if (!bestByFirst[t] || r.success > bestByFirst[t].success) bestByFirst[t] = r;
  if (!bestByFamily[r.family] || r.success > bestByFamily[r.family].success) bestByFamily[r.family] = r;
}

const out = {
  enemy_hp: ENEMY_HP,
  player_hp: PLAYER_HP,
  decision_model: "full_action_search",
  legal_deck_count: decks.length,
  total_build_count: totalBuilds,
  skip_builds: skipBuilds,
  legal_build_count: rows.length,
  perfect_success_count: perfect,
  top30: rows.slice(0, 30),
  best_by_first_turn: bestByFirst,
  best_by_family: bestByFamily,
};
const suffix = skipBuilds || maxBuilds ? `_part_${skipBuilds}_${rows.length}` : "";
if (!noWrite) fs.writeFileSync(`difficulty10_time_rift_audit${suffix}.json`, JSON.stringify(out, null, 2), "utf8");

if (rows.length) {
  const b = rows[0];
  console.log(`enemy_hp ${ENEMY_HP} player_hp ${PLAYER_HP}`);
  console.log(`legal_deck_count ${decks.length}`);
  console.log(`legal_build_count ${rows.length}`);
  console.log(`perfect_success_count ${perfect}`);
  console.log(`best ${b.success.toFixed(4)} first ${b.first_turn} ${b.family} [${b.kill_vector.map((x) => x.toFixed(4)).join(",")}] ${b.build_display}`);
  console.log("best_by_first_turn");
  for (const t of Object.keys(bestByFirst).sort((a, b) => Number(a) - Number(b))) {
    const r = bestByFirst[t];
    console.log(`${t} ${r.success.toFixed(4)} ${r.family} [${r.kill_vector.map((x) => x.toFixed(4)).join(",")}] ${r.build_display}`);
  }
  console.log("best_by_family");
  for (const f of Object.keys(bestByFamily).sort()) {
    const r = bestByFamily[f];
    console.log(`${f} ${r.success.toFixed(4)} first ${r.first_turn} [${r.kill_vector.map((x) => x.toFixed(4)).join(",")}] ${r.build_display}`);
  }
}
