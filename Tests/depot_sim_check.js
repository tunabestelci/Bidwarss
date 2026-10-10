// Checks that the wiki's depot simulator (Site/kasa-defteri.html) draws exactly the depots the game does.
// Run: node Tests/depot_sim_check.js        (the C# side writes Tests/golden_depots.txt, see Tests/DomainTests.cs)
'use strict';
const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const html = fs.readFileSync(path.join(root, 'Site', 'kasa-defteri.html'), 'utf8');
const start = html.indexOf('/* DEPOT-SIM:BEGIN'), end = html.indexOf('/* DEPOT-SIM:END */');
if (start < 0 || end < 0) { console.error('FAIL depot simulator markers not found in Site/kasa-defteri.html'); process.exit(1); }
const DepotSim = new Function(html.slice(start, end) + '\nreturn DepotSim;')();
const catalog = JSON.parse(fs.readFileSync(path.join(root, 'Assets', 'Bidwarss', 'Data', 'ItemCatalog.json'), 'utf8'));

let checks = 0;
const failures = [];
function check(ok, message) { checks++; if (!ok) failures.push(message); }

// 1. Same depots as the game for the golden seeds.
const golden = fs.readFileSync(path.join(root, 'Tests', 'golden_depots.txt'), 'utf8').split('\n').filter(Boolean);
check(golden.length > 0, 'golden file is empty');
for (const line of golden) {
  const at = line.indexOf(';'), seed = parseInt(line.slice(0, at), 10);
  const mine = seed + ';' + DepotSim.summary(DepotSim.generate(catalog, seed));
  check(mine === line, 'seed ' + seed + ' differs from the game\n  site:   ' + mine + '\n  golden: ' + line);
}

// 2. Rules hold for many seeds.
const R = DepotSim.RULES, caps = catalog.items.map(DepotSim.maxCount);
const clock = catalog.items.findIndex(i => i.key === 'boy-saati');
check(caps[clock] === 5, 'a two metre clock is capped at five');
let crateSets = new Set(), totals = new Set();
for (let seed = -500; seed < 500; seed++) {
  const d = DepotSim.generate(catalog, seed);
  check(d.items.length >= 1 && d.items.length <= R.totalGroups * R.stackSize, 'item count in range');
  check(d.active.length >= R.minCrates && d.active.length <= R.crateCount, 'active crates in range');
  check(d.racks.length >= 1 && d.racks.length <= R.totalGroups, 'racks in range');
  check(d.counts.every((c, k) => c <= caps[k]), 'type limit respected');
  check(d.load.every(n => n <= R.crateLimit), 'crate load limit respected');
  check(d.active.every(c => d.load[c] > 0) && d.load.every((n, c) => n === 0 || d.active.includes(c)), 'only active crates hold items, none empty');
  check(d.racks.reduce((s, r) => s + r.capacity, 0) === d.items.length, 'rack capacities add up');
  check(d.items.every((it, i) => it.id === i), 'ids in order');
  crateSets.add(d.active.length); totals.add(d.items.length);
}
check(crateSets.size === R.crateCount - R.minCrates + 1, 'every crate count from min to max occurs');
check(totals.size >= 20, 'item totals vary');

// 2b. The wiki exports full class tables; they must give the same depots as the derived ones.
const exported = { items: catalog.items.map(it => {
  const t = DepotSim.table(it);
  return Object.assign({}, it, { classes: t.weight.map((w, k) => ({ chance: w / 1000, priceMin: t.min[k], priceMax: t.max[k] })) });
}) };
for (let seed = -50; seed < 50; seed++)
  check(DepotSim.summary(DepotSim.generate(exported, seed)) === DepotSim.summary(DepotSim.generate(catalog, seed)), 'exported tables differ from derived ones at seed ' + seed);

// 3. A table typed by hand is honoured, a missing one is derived.
const custom = { items: [{ key: 'a', heightCm: 10, classes: [0, 1, 2, 3, 4, 5, 6].map(k => ({ chance: k === 0 ? 100 : 0, priceMin: 5, priceMax: 6 })) },
                         { key: 'b', baseValue: 40, heightCm: 10 }] };
const d = DepotSim.generate(custom, 3);
check(d.items.filter(i => i.kind === 0).every(i => i.cond === 0 && i.dollars >= 5 && i.dollars <= 6), 'explicit table honoured');
let threw = false; try { DepotSim.generate({ items: [{ key: 'x', classes: [{ chance: 1, priceMin: 1, priceMax: 1 }] }] }, 1); } catch (e) { threw = true; }
check(threw, 'a table with the wrong number of rows is rejected');

if (failures.length) { failures.slice(0, 12).forEach(f => console.error('FAIL ' + f)); console.error(failures.length + ' failure(s) after ' + checks + ' checks.'); process.exit(1); }
console.log('PASS: ' + checks + ' checks; wiki simulator matches the game on ' + golden.length + ' golden depots.');
