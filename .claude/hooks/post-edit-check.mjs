#!/usr/bin/env node
// PostToolUse hook: valida o código logo após o Claude editar um arquivo.
//  - .cs/.csproj/.props -> dotnet build da solução (erros de compilação voltam para o Claude)
//  - .ts/.tsx           -> npm run typecheck no frontend
// exit 2 + stderr = o Claude recebe o erro e corrige na mesma sessão. exit 0 = segue.
import { readFileSync, existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import os from 'node:os';

let input;
try {
  input = JSON.parse(readFileSync(0, 'utf8'));
} catch {
  process.exit(0);
}

const root = process.env.CLAUDE_PROJECT_DIR ?? process.cwd();
const file = String(input.tool_input?.file_path ?? '').replace(/\\/g, '/');
if (!file) process.exit(0);

const isWin = process.platform === 'win32';
const run = (cmd, args, cwd) =>
  spawnSync(cmd, args, { cwd, encoding: 'utf8', shell: isWin, timeout: 170_000 });
const tail = (text, n = 30) => text.split(/\r?\n/).filter((l) => l.trim()).slice(-n).join('\n');
const fail = (msg) => {
  process.stderr.write(`${msg}\n`);
  process.exit(2);
};

/** Localiza o dotnet: PATH, DOTNET_ROOT ou instalação por usuário (dotnet-install). */
function findDotnet() {
  const candidates = [
    process.env.DOTNET_ROOT && path.join(process.env.DOTNET_ROOT, isWin ? 'dotnet.exe' : 'dotnet'),
    isWin && process.env.LOCALAPPDATA && path.join(process.env.LOCALAPPDATA, 'Microsoft', 'dotnet', 'dotnet.exe'),
    !isWin && path.join(os.homedir(), '.dotnet', 'dotnet'),
  ].filter(Boolean);
  const probe = spawnSync('dotnet', ['--version'], { encoding: 'utf8', shell: isWin });
  if (probe.status === 0) return 'dotnet';
  return candidates.find((c) => existsSync(c));
}

if (/\/backend\/.*\.(cs|csproj|props)$/.test(file)) {
  const sln = path.join(root, 'backend', 'Pedidos.sln');
  if (!existsSync(sln) || !/\.csproj"/.test(readFileSync(sln, 'utf8'))) process.exit(0); // solução ainda vazia
  const dotnet = findDotnet();
  if (!dotnet) {
    process.stderr.write('[post-edit] dotnet não encontrado; build ignorado.\n');
    process.exit(0);
  }
  const r = run(`"${dotnet}"`, ['build', `"${sln}"`, '--nologo', '-v', 'q', '-clp:ErrorsOnly;NoSummary'], root);
  if (r.status !== 0) {
    const out = `${r.stdout ?? ''}${r.stderr ?? ''}`;
    const errors = [...new Set(out.split(/\r?\n/).filter((l) => /\berror\b/i.test(l)).map((l) => l.trim()))];
    fail(`[post-edit] dotnet build falhou após editar ${path.basename(file)}:\n${tail(errors.join('\n') || out || String(r.error ?? 'sem saída'))}`);
  }
}

if (/\/frontend\/(src|e2e)\/.*\.(ts|tsx)$/.test(file)) {
  const fe = path.join(root, 'frontend');
  if (!existsSync(path.join(fe, 'node_modules'))) process.exit(0);
  const r = run('npm', ['run', '--silent', 'typecheck'], fe);
  if (r.status !== 0) {
    fail(`[post-edit] typecheck falhou após editar ${path.basename(file)}:\n${tail(`${r.stdout ?? ''}${r.stderr ?? ''}`)}`);
  }
}

process.exit(0);
