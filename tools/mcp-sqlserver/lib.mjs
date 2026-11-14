// Lógica pura (sem I/O) — testada em test/lib.test.mjs.

// ---------- Validação (camada 1) ----------
const FORBIDDEN =
  /\b(INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|CREATE|TRUNCATE|EXEC|EXECUTE|GRANT|REVOKE|DENY|BACKUP|RESTORE|SHUTDOWN|KILL|DBCC|OPENROWSET|OPENQUERY|OPENDATASOURCE|BULK|WAITFOR|SP_\w+|XP_\w+)\b|\bINTO\b/i;

export function validateReadOnly(input) {
  const text = String(input ?? '')
    .replace(/--[^\n]*/g, ' ')
    .replace(/\/\*[\s\S]*?\*\//g, ' ')
    .replace(/'(?:[^']|'')*'/g, "''") // ignora conteúdo de literais
    .trim()
    .replace(/;\s*$/, '');
  if (!text) return 'Query vazia.';
  if (!/^(SELECT|WITH)\b/i.test(text)) return 'Apenas SELECT/WITH são permitidos.';
  if (text.includes(';')) return 'Apenas um statement por chamada.';
  if (/^\s*GO\s*$/im.test(text)) return 'Separador GO não é permitido.';
  const m = text.match(FORBIDDEN);
  if (m) return `Palavra-chave não permitida em modo somente leitura: ${m[0].toUpperCase()}`;
  return null;
}

// ---------- Plano de execução ----------
export function summarizePlan(xml) {
  const ops = {};
  for (const m of xml.matchAll(/<RelOp [^>]*PhysicalOp="([^"]+)"[^>]*EstimateRows="([^"]+)"/g)) {
    ops[m[1]] = (ops[m[1]] ?? 0) + 1;
  }
  const attr = (tag, name) => tag.match(new RegExp(`${name}="\\[([^\\]]+)\\]"`))?.[1];
  const objects = [...new Set([...xml.matchAll(/<Object [^>]*>/g)]
    .map(([tag]) => [attr(tag, 'Table'), attr(tag, 'Index')])
    .filter(([t]) => t)
    .map(([t, i]) => (i ? `${t}.${i}` : t)))];
  const cost = xml.match(/StatementSubTreeCost="([^"]+)"/)?.[1];
  const warnings = [...xml.matchAll(/<(PlanAffectingConvert|NoJoinPredicate|SpillToTempDb|ColumnsWithNoStatistics)\b[^>]*>/g)].map((m) => m[1]);
  const missing = [...xml.matchAll(/<MissingIndexGroup Impact="([^"]+)">[\s\S]*?<MissingIndex [^>]*Table="\[([^\]]+)\]"[\s\S]*?<\/MissingIndex>/g)]
    .map((m) => ({ tabela: m[2], impactoEstimado: `${Number(m[1]).toFixed(1)}%` }));
  return { custoEstimado: cost ? Number(cost) : null, operadores: ops, objetosAcessados: objects, avisos: warnings, indicesFaltantes: missing };
}
