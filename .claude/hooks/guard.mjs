#!/usr/bin/env node
// PreToolUse hook: bloqueia ações destrutivas ANTES de executarem.
// Contrato de hooks do Claude Code: recebe JSON no stdin; exit 2 = bloqueia e envia stderr ao Claude.
import { readFileSync } from 'node:fs';

let input;
try {
  input = JSON.parse(readFileSync(0, 'utf8'));
} catch {
  process.exit(0); // entrada inesperada: não atrapalha o fluxo
}

const tool = input.tool_name ?? '';
const ti = input.tool_input ?? {};

const block = (reason) => {
  process.stderr.write(`[guard] Bloqueado: ${reason}\n`);
  process.exit(2);
};

if (tool === 'Bash') {
  const cmd = String(ti.command ?? '');
  const rules = [
    [/git\s+push\s+.*(--force\b|-f\b|--force-with-lease)/i, 'push forçado reescreve o histórico (o teste exige histórico preservado).'],
    [/git\s+reset\s+--hard/i, 'git reset --hard descarta trabalho não commitado.'],
    [/git\s+rebase\b/i, 'rebase reescreve o histórico de commits.'],
    [/git\s+commit\s+.*--amend/i, 'amend reescreve o último commit; crie um novo commit.'],
    [/\brm\s+-rf?\s+(\/|~|\*|\.\s*$)/i, 'remoção recursiva perigosa.'],
    [/\b(DROP\s+(DATABASE|TABLE)|TRUNCATE\s+TABLE)\b/i, 'DDL destrutivo — altere o schema pelos scripts em database/init.'],
    [/docker\s+(compose\s+down|volume\s+rm).*\s-v\b/i, 'remove volumes de dados; peça confirmação ao usuário.'],
  ];
  for (const [re, why] of rules) if (re.test(cmd)) block(why);
}

if (['Edit', 'Write', 'MultiEdit'].includes(tool)) {
  const file = String(ti.file_path ?? '').replace(/\\/g, '/');
  if (/(^|\/)\.env$/.test(file)) block('arquivos .env contêm segredos; edite .env.example.');
  if (/\/(bin|obj|node_modules|dist)\//.test(file)) block('artefato gerado; edite o código-fonte.');
}

process.exit(0);
