// Checks the APIOps policy files without Azure: well-formed once policy expressions are blanked,
// four sections with <base /> in each, and the two order API copies identical.
// API Management publishes no XSD for policies, so this is structure only; the real validation is the PUT.
// Usage: node check-policies.mjs <apim-artifacts folder>
import { readFileSync, readdirSync } from 'node:fs'
import { basename, join } from 'node:path'

const SECTIONS = ['inbound', 'backend', 'outbound', 'on-error']
// Both versions of the order API run one policy (Part 4 loaded the same file for both).
const MUST_MATCH = [['apis/orders/policy.xml', 'apis/orders-v2/policy.xml']]

const NAME = '[A-Za-z_][\\w.:-]*'
const TAG = new RegExp(`<(/?)(${NAME})(?:\\s+${NAME}\\s*=\\s*(?:"[^"<]*"|'[^'<]*'))*\\s*(/?)>`, 'y')
const SKIP = /<!--[\s\S]*?-->|<!\[CDATA\[[\s\S]*?\]\]>|<\?[\s\S]*?\?>/y
const TEXT = /[^<]+/y
const BARE_AMPERSAND = /&(?!(?:[a-z]+|#\d+|#x[\da-f]+);)/i

const lineOf = (src, index) => src.slice(0, index).split('\n').length

class PolicyError extends Error {
  constructor(src, index, message) {
    super(`line ${lineOf(src, index)}: ${message}`)
  }
}

function skipLiteral(src, start) {
  for (let i = start + 1; i < src.length; i++) {
    if (src[i] === '\\') i++
    else if (src[i] === src[start]) return i
  }
  throw new PolicyError(src, start, 'unclosed string in a policy expression')
}

// Expressions are C#, and policies travel as rawxml, so condition="@(x == "POST")" is a valid policy
// and invalid XML. Replace each @(...) and @{...} with spaces, keeping line breaks for line numbers.
function blankExpressions(src) {
  let out = ''
  for (let i = 0; i < src.length; i++) {
    const close = src[i] === '@' && { '(': ')', '{': '}' }[src[i + 1]]
    if (!close) {
      out += src[i]
      continue
    }
    let depth = 0
    let end = -1
    for (let j = i + 1; j < src.length && end < 0; j++) {
      if (src[j] === '"' || src[j] === "'") j = skipLiteral(src, j)
      else if (src[j] === src[i + 1]) depth++
      else if (src[j] === close && --depth === 0) end = j
    }
    if (end < 0) throw new PolicyError(src, i, 'unclosed policy expression')
    out += src.slice(i, end + 1).replace(/[^\n]/g, ' ')
    i = end
  }
  return out
}

function parse(xml) {
  const root = { children: [] }
  const stack = [root]
  let i = 0
  const at = (re) => {
    re.lastIndex = i
    const m = re.exec(xml)
    if (m) i = re.lastIndex
    return m
  }
  while (i < xml.length) {
    const start = i
    let m
    if ((m = at(TEXT))) {
      if (BARE_AMPERSAND.test(m[0])) throw new PolicyError(xml, start, 'unescaped & outside a policy expression')
      if (stack.length === 1 && m[0].trim()) throw new PolicyError(xml, start, 'text outside <policies>')
    } else if (at(SKIP)) {
      continue
    } else if ((m = at(TAG))) {
      const [, closing, name, selfClosing] = m
      if (closing) {
        const open = stack.pop()
        if (open.name !== name) throw new PolicyError(xml, start, `</${name}> closes <${open.name ?? 'nothing'}>`)
      } else {
        const element = { name, children: [] }
        stack.at(-1).children.push(element)
        if (!selfClosing) stack.push(element)
      }
    } else {
      throw new PolicyError(xml, start, 'malformed markup (unquoted attribute, or < in an attribute value?)')
    }
  }
  if (stack.length > 1) throw new PolicyError(xml, xml.length, `<${stack.at(-1).name}> is never closed`)
  if (root.children.length !== 1) throw new PolicyError(xml, 0, 'expected exactly one root element')
  return root.children[0]
}

function structureProblems(root) {
  if (root.name !== 'policies') return [`root element is <${root.name}>, expected <policies>`]
  return SECTIONS.flatMap((name) => {
    const found = root.children.filter((c) => c.name === name)
    if (found.length !== 1) return [`expected one <${name}>, found ${found.length}`]
    if (!found[0].children.some((c) => c.name === 'base')) {
      return [`<${name}> has no <base />, so the broader scope's ${name} section never runs`]
    }
    return []
  })
}

const dir = process.argv[2] ?? 'apim-artifacts'
// A policy.xml at the root is the global policy, which has no broader scope to call.
const files = readdirSync(dir, { recursive: true })
  .filter((f) => basename(f) === 'policy.xml' && f !== 'policy.xml')
  .sort()

let failed = false
const report = (file, message) => {
  console.error(`${join(dir, file)}: ${message}`)
  failed = true
}

for (const file of files) {
  try {
    structureProblems(parse(blankExpressions(readFileSync(join(dir, file), 'utf8')))).forEach((p) => report(file, p))
  } catch (e) {
    if (!(e instanceof PolicyError)) throw e
    report(file, e.message)
  }
}

for (const [a, b] of MUST_MATCH) {
  try {
    if (readFileSync(join(dir, a), 'utf8') !== readFileSync(join(dir, b), 'utf8')) report(a, `differs from ${b}; change both`)
  } catch (e) {
    if (e.code !== 'ENOENT') throw e
    report(a, `${a} and ${b} must both exist`)
  }
}

if (failed) process.exit(1)
console.log(`${files.length} policy files OK`)
