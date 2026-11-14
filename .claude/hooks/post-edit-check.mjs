#!/usr/bin/env node
// PostToolUse hook: valida o código logo após o Claude editar um arquivo.
//  - .cs/.csproj  -> dotnet build da solução (erros de compilação voltam para o Claude)
//  - .ts/.tsx     -> tsc --noEmit no frontend
// exit 2 + stderr = o Claude recebe o erro e corrige na mesma sessão.
import { readFileSync, existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import path from 'node:path';

let input;
try {
  input = JSON.parse(readFileSync(0, 'utf8'));
} catch {
  process.exit(0);
}

const root = process.env.CLAUDE_PROJECT_DIR ?? process.cwd();
const file = String(input.tool_input?.file_path ?? '').replace(/\\/g, '/');
if (!file) process.exit(0);

const run = (cmd, args, cwd) =>
  spawnSync(cmd, args, { cwd, encoding: 'utf8', shell: process.platform === 'win32', timeout: 170_000 });

const tail = (text, n = 40) => text.split(/\r?\n/).filter(Boolean).slice(-n).join('\n');

if (/\/backend\/.*\.(cs|csproj)$/.test(file)) {
  const sln = path.join(root, 'backend', 'Pedidos.sln');
  if (!existsSync(sln)) process.exit(0);
  const r = run('dotnet', ['build', sln, '--nologo', '-v', 'q', '-clp:ErrorsOnly'], root);
  if (r.status !== 0) {
    const errors = (r.stdout + r.stderr).split(/\r?\n/).filter((l) => /error/i.test(l));
    process.stderr.write(`[post-edit] dotnet build falhou após editar ${path.basename(file)}:\n${tail(errors.join('\n') || r.stdout)}\n`);
    process.exit(2);
  }
}

if (/\/frontend\/(src|e2e)\/.*\.(ts|tsx)$/.test(file)) {
  const fe = path.join(root, 'frontend');
  if (!existsSync(path.join(fe, 'node_modules'))) process.exit(0);
  const r = run('npm', ['run', '--silent', 'typecheck'], fe);
  if (r.status !== 0) {
    process.stderr.write(`[post-edit] typecheck falhou após editar ${path.basename(file)}:\n${tail(r.stdout + r.stderr)}\n`);
    process.exit(2);
  }
}

process.exit(0);
