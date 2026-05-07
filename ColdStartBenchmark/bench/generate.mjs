#!/usr/bin/env node
// Generates EndpointNN.cs files for Functions05/Functions15/Functions30.
// Idempotent: re-running produces identical output. Run from any CWD.

import { mkdirSync, writeFileSync, readdirSync, unlinkSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, "..");

const projects = [
  { name: "Functions05", count: 5 },
  { name: "Functions15", count: 15 },
  { name: "Functions30", count: 30 },
];

function template(namespace, n) {
  const pad = String(n).padStart(2, "0");
  return `using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ${namespace};

public sealed class Endpoint${pad}
{
    [Function("Endpoint${pad}")]
    public static IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "endpoint${pad}")] HttpRequest req)
        => new OkObjectResult(new { id = ${n} });
}
`;
}

let written = 0;
let removed = 0;

for (const { name, count } of projects) {
  const dir = join(root, name, "Endpoints");
  mkdirSync(dir, { recursive: true });

  // Remove any stale EndpointNN.cs files outside the requested range
  const wanted = new Set(
    Array.from({ length: count }, (_, i) => `Endpoint${String(i + 1).padStart(2, "0")}.cs`),
  );
  for (const file of readdirSync(dir)) {
    if (file.startsWith("Endpoint") && file.endsWith(".cs") && !wanted.has(file)) {
      unlinkSync(join(dir, file));
      removed += 1;
    }
  }

  for (let i = 1; i <= count; i++) {
    const pad = String(i).padStart(2, "0");
    const path = join(dir, `Endpoint${pad}.cs`);
    writeFileSync(path, template(name, i));
    written += 1;
  }
}

console.log(`Wrote ${written} endpoint file(s); removed ${removed} stale file(s).`);
