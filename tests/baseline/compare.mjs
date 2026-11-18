// Compares a replay (BASELINE_OUT=replay node capture.mjs) with the recorded v1 baseline.
// Usage: node tests/baseline/compare.mjs [replayDir]   — exits 1 when an unexpected difference is found.
import { readFileSync, readdirSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const baselineDir = join(here, 'cases');
const replayDir = resolve(here, process.argv[2] ?? 'replay');
const deviations = JSON.parse(readFileSync(join(here, 'deviations.json'), 'utf8'));

const UUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;
const TIMESTAMP = /\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\+00:00/g;
const STORED = /[0-9a-f]{48}\.(webp|png|jpg|pdf)/g;
const VOLATILE_KEYS = new Set(['today']);
const COMPARED_HEADERS = ['content-type', 'content-disposition', 'location', 'x-content-type-options', 'x-frame-options', 'referrer-policy', 'permissions-policy'];

// Fields added after v1 at the end of a row (D-27 booking_channel); everything else in the row is still compared.
const ADDED_KEYS = new Set(['booking_channel']);

const mask = value => {
  if (typeof value === 'string') return value.replace(UUID, '<uuid>').replace(TIMESTAMP, '<ts>').replace(STORED, '<stored>');
  if (Array.isArray(value)) return value.map(mask);
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).filter(([k]) => !ADDED_KEYS.has(k)).map(([k, v]) => [k, VOLATILE_KEYS.has(k) ? '<volatile>' : mask(v)]));
  }
  return value;
};
// Stays are listed check_in_date DESC, created_at DESC, then in insertion order. created_at has second precision, so
// whether two bookings saved during a run tie depends on timing: in the v1 recording "ทดสอบ Baseline" and "Pet Owner"
// were saved in the same second (insertion order wins); in a slower replay they are not, and the newer one comes first,
// as it would in v1. So stays that share a check-in date are compared as a set (snapshot, search, CSV), and the
// replay's own order is checked against the rule instead. The insertion-order tie-break has a data test.
const byText = (a, b) => (a < b ? -1 : a > b ? 1 : 0);
const sameDayRuns = (rows, day) => rows.reduce((runs, row) => {
  const last = runs.at(-1);
  if (last && day(last[0]) === day(row)) last.push(row); else runs.push([row]);
  return runs;
}, []);
const isStay = v => v !== null && typeof v === 'object' && !Array.isArray(v) && 'guest_name' in v && 'check_in_date' in v && 'created_at' in v;
const stayArrays = (value, found = []) => {
  if (Array.isArray(value)) {
    if (value.length && value.every(isStay)) found.push(value);
    value.forEach(v => stayArrays(v, found));
  } else if (value && typeof value === 'object') Object.values(value).forEach(v => stayArrays(v, found));
  return found;
};
const canonicalStays = value => {
  if (Array.isArray(value)) {
    const items = value.map(canonicalStays);
    if (!items.length || !items.every(isStay)) return items;
    return sameDayRuns(items, s => s.check_in_date)
      .flatMap(run => run.map(s => [JSON.stringify(mask(s)), s]).sort(([a], [b]) => byText(a, b)).map(([, s]) => s));
  }
  if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value).map(([k, v]) => [k, canonicalStays(v)]));
  return value;
};
const staysOutOfOrder = json => stayArrays(json).some(rows => rows.some((s, i) => i > 0 && (
  rows[i - 1].check_in_date < s.check_in_date || (rows[i - 1].check_in_date === s.check_in_date && rows[i - 1].created_at < s.created_at))));
const CSV_CHECK_IN = /,(\d{4}-\d{2}-\d{2}),\d{4}-\d{2}-\d{2},\d+,[^,]*$/;
const canonicalCsv = text => {
  const [head, ...rows] = text.split('\n');
  const tail = rows.at(-1) === '' ? rows.pop() : undefined;
  const sorted = sameDayRuns(rows, row => CSV_CHECK_IN.exec(row)?.[1] ?? row).flatMap(run => run.sort(byText));
  return [head, ...sorted, ...(tail === undefined ? [] : [tail])].join('\n');
};

const normalizeType = v => v?.toLowerCase().replace(/\s+/g, '');
const today = /\d{4}-\d{2}-\d{2}/g;
// The default snapshot range is the current month, so its dates depend on the day the replay runs (the baseline was
// recorded in October 2026). Check that it is a whole calendar month, then compare it without the month itself.
const wholeMonth = json => {
  const a = json?.data?.analytics;
  if (!a?.start) return json;
  const days = new Date(Date.UTC(Number(a.start.slice(0, 4)), Number(a.start.slice(5, 7)), 0)).getUTCDate();
  if (!a.start.endsWith('-01') || a.end !== `${a.start.slice(0, 8)}${days}` || a.day_count !== days || a.daily?.length !== days) return json;
  const daily = a.daily.slice(0, 28).map(d => ({ ...d, date: '<date>' }));
  return { ...json, data: { ...json.data, analytics: { ...a, start: '<month-start>', end: '<month-end>', day_count: '<days>', daily } } };
};

const differences = [];
for (const file of readdirSync(baselineDir).filter(f => f !== '_index.json').sort()) {
  const name = file.replace(/^\d+-/, '').replace(/\.json$/, '');
  let replay;
  try { replay = JSON.parse(readFileSync(join(replayDir, file), 'utf8')); } catch { differences.push([name, 'missing in replay']); continue; }
  const base = JSON.parse(readFileSync(join(baselineDir, file), 'utf8'));
  const allowed = deviations[name] ?? [];
  const report = (what, expected, actual) => {
    if (!allowed.includes(what)) differences.push([name, what, expected, actual]);
  };

  if (base.status !== replay.status) report('status', base.status, replay.status);
  for (const header of COMPARED_HEADERS) {
    if (header === 'content-type' && base.status >= 300 && base.status < 400) continue;
    let expected = base.headers[header];
    let actual = replay.headers[header];
    if (header === 'content-type') { expected = normalizeType(expected); actual = normalizeType(actual); }
    if (header === 'content-disposition') { expected = expected?.replace(today, '<date>'); actual = actual?.replace(today, '<date>'); }
    if (expected !== actual) report(`header:${header}`, expected, actual);
  }
  if (base.status >= 300 && base.status < 400 && base.status === replay.status) continue; // redirects: only status and location matter
  const b = base.body;
  const r = replay.body;
  if (b.kind !== r.kind) { report('body:kind', b.kind, r.kind); continue; }
  if (b.kind === 'json') {
    const month = name === 'snapshot-default-range' ? wholeMonth : json => json;
    const expected = JSON.stringify(mask(canonicalStays(month(b.json))));
    const actual = JSON.stringify(mask(canonicalStays(month(r.json))));
    if (expected !== actual) report('body:json', expected, actual);
    if (staysOutOfOrder(r.json)) report('order:stays', 'check_in_date DESC, created_at DESC', JSON.stringify(stayArrays(r.json).map(rows => rows.map(s => [s.guest_name, s.check_in_date, s.created_at]))));
  } else if (b.kind === 'html') {
    if (b.title !== r.title) report('body:title', b.title, r.title);
    const missingIds = b.ids.filter(id => !r.ids.includes(id));
    if (missingIds.length) report('body:ids', missingIds.join(','), '(missing)');
    const missingText = b.text.filter(t => !r.text.includes(t));
    if (missingText.length) report('body:text', missingText.join(' | '), '(missing)');
  } else if (b.kind === 'csv') {
    const expected = canonicalCsv(mask(b.text));
    const actual = canonicalCsv(mask(r.text));
    if (expected !== actual) report('body:csv', expected, actual);
  }
}

for (const [name, what, expected, actual] of differences) {
  console.log(`✗ ${name} — ${what}`);
  if (expected !== undefined) console.log(`    v1  : ${String(expected).slice(0, 600)}`);
  if (actual !== undefined) console.log(`    .NET: ${String(actual).slice(0, 600)}`);
}
console.log(differences.length ? `${differences.length} unexpected difference(s)` : 'No unexpected differences');
process.exit(differences.length ? 1 : 0);
