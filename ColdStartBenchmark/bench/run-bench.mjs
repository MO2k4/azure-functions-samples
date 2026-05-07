#!/usr/bin/env node
// Cold-start benchmark for .NET 10 isolated worker Function Apps.
//
// For each project (Functions05, Functions15, Functions30) we:
//   1. Build (Debug) once.
//   2. Run N iterations: spawn `func start --port <P>`, poll the first endpoint
//      until it returns 200, kill the process, wait, repeat.
//   3. Compute median, p10, p90 of the time-to-first-response samples.
//   4. Write a markdown table to bench/results.md.
//
// What this measures: wall-clock from process spawn to first 200 OK on
// /api/endpoint01. Includes Core Tools (`func start`) overhead, .NET runtime
// startup, worker initialization, and host metadata loading. The variable that
// scales with function count is worker init + metadata loading; everything else
// is roughly constant across the three projects, so the *delta* between
// configurations is what matters, not the absolute number.
//
// Usage:
//   node bench/run-bench.mjs                # default 10 iterations
//   ITERATIONS=20 node bench/run-bench.mjs  # override

import { spawn } from "node:child_process";
import { copyFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { setTimeout as sleep } from "node:timers/promises";

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, "..");

const ITERATIONS = Number(process.env.ITERATIONS ?? 10);
const POLL_INTERVAL_MS = 50;
const REQUEST_TIMEOUT_MS = 60_000;
const COOLDOWN_MS = 1500;
const BIN_REL = "bin/Debug/net10.0";

const projects = [
  { name: "Functions05", port: 7080 },
  { name: "Functions15", port: 7081 },
  { name: "Functions30", port: 7082 },
];

async function buildAll() {
  await run("dotnet", ["build", "ColdStartBenchmark.slnx", "-c", "Debug", "--nologo", "-v", "q"], {
    cwd: root,
  });
  // Copy local.settings.json.example into each bin folder so `func start`
  // (run from the bin folder) finds the FUNCTIONS_WORKER_RUNTIME setting.
  for (const project of projects) {
    copyFileSync(
      join(root, project.name, "local.settings.json.example"),
      join(root, project.name, BIN_REL, "local.settings.json"),
    );
  }
}

function run(cmd, args, opts = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(cmd, args, { stdio: "inherit", ...opts });
    child.on("error", reject);
    child.on("exit", (code) => (code === 0 ? resolve() : reject(new Error(`${cmd} exited ${code}`))));
  });
}

async function pollUntil200(url, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const res = await fetch(url);
      if (res.status === 200) {
        await res.text();
        return true;
      }
    } catch {
      // connection refused / dns error while host is starting; ignore and retry
    }
    await sleep(POLL_INTERVAL_MS);
  }
  return false;
}

async function killProcessTree(child) {
  if (child.exitCode !== null) return;
  // We spawned with detached: true so the child has its own process group;
  // negating the pid signals the entire group, taking out the dotnet worker
  // child that `func start` itself would otherwise leak.
  try {
    process.kill(-child.pid, "SIGTERM");
  } catch {
    // group already gone
  }
  const exited = await Promise.race([
    new Promise((res) => child.once("exit", () => res(true))),
    sleep(5000).then(() => false),
  ]);
  if (!exited) {
    try {
      process.kill(-child.pid, "SIGKILL");
    } catch {
      // already gone
    }
    await new Promise((res) => child.once("exit", () => res()));
  }
}

async function measureOne(project) {
  // Spawn from bin/Debug/net10.0 so func finds .azurefunctions / functions.metadata
  // without invoking its own implicit build (which would add variable noise).
  const cwd = join(root, project.name, BIN_REL);
  const url = `http://localhost:${project.port}/api/endpoint01`;
  const started = Date.now();
  const child = spawn("func", ["start", "--port", String(project.port)], {
    cwd,
    stdio: ["ignore", "ignore", "ignore"],
    detached: true,
  });
  let elapsed = null;
  try {
    const ok = await pollUntil200(url, REQUEST_TIMEOUT_MS);
    elapsed = ok ? Date.now() - started : null;
  } finally {
    await killProcessTree(child);
  }
  await sleep(COOLDOWN_MS);
  return elapsed;
}

function pct(sorted, p) {
  if (sorted.length === 0) return null;
  const idx = Math.min(sorted.length - 1, Math.max(0, Math.floor((p / 100) * (sorted.length - 1))));
  return sorted[idx];
}

function summarise(samples) {
  const ok = samples.filter((s) => s !== null).sort((a, b) => a - b);
  const failed = samples.length - ok.length;
  if (ok.length === 0) return { failed, count: samples.length, median: null, p10: null, p90: null };
  return {
    failed,
    count: samples.length,
    median: pct(ok, 50),
    p10: pct(ok, 10),
    p90: pct(ok, 90),
  };
}

function fmt(ms) {
  return ms === null ? "—" : `${ms} ms`;
}

async function main() {
  console.log(`Building all projects...`);
  await buildAll();

  const rows = [];
  for (const project of projects) {
    console.log(`\n== ${project.name} (${ITERATIONS} iterations, port ${project.port}) ==`);
    const samples = [];
    for (let i = 1; i <= ITERATIONS; i++) {
      const ms = await measureOne(project);
      console.log(`  iter ${String(i).padStart(2, "0")}: ${ms === null ? "FAIL" : `${ms} ms`}`);
      samples.push(ms);
    }
    rows.push({ project: project.name, samples, ...summarise(samples) });
  }

  const baseline = rows[0]?.median ?? null;
  const table = [
    "| Functions | Median | p10 | p90 | Δ vs baseline | Iterations |",
    "| --- | --- | --- | --- | --- | --- |",
    ...rows.map((r) => {
      const delta =
        baseline !== null && r.median !== null
          ? `${r.median - baseline >= 0 ? "+" : ""}${r.median - baseline} ms`
          : "—";
      const okCount = r.count - r.failed;
      return `| ${r.project} | ${fmt(r.median)} | ${fmt(r.p10)} | ${fmt(r.p90)} | ${delta} | ${okCount}/${r.count} |`;
    }),
  ].join("\n");

  const md = `# Cold-Start Benchmark Results

_Generated by \`bench/run-bench.mjs\` on ${new Date().toISOString()}_

Wall-clock time from \`func start\` process spawn to first \`200 OK\` on
\`/api/endpoint01\`, repeated ${ITERATIONS} times per project.

${table}

**Baseline:** \`${rows[0]?.project ?? "n/a"}\` median.

The absolute numbers include \`func start\` (Core Tools) overhead, .NET runtime
startup, and host metadata loading. The variable that scales with function
count is worker init + metadata loading; the **delta column** is the signal,
not the absolute median.

## Environment

- Node: ${process.version}
- Platform: ${process.platform} ${process.arch}
- Iterations per project: ${ITERATIONS}
- Poll interval: ${POLL_INTERVAL_MS} ms
- Per-iteration cooldown: ${COOLDOWN_MS} ms

To re-run on your machine: \`node bench/run-bench.mjs\`.
`;

  const outPath = join(here, "results.md");
  writeFileSync(outPath, md);
  console.log(`\nWrote ${outPath}`);
  console.log("\n" + table);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
