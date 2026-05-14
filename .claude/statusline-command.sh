#!/usr/bin/env bash
# Claude Code status line: dir | branch | model | ctx% | 5h% | 7d%
input=$(cat)
printf '%s' "$input" | python -c "
import sys, json, os, subprocess, datetime

d = json.load(sys.stdin)

GREEN  = '\033[32m'
YELLOW = '\033[33m'
RED    = '\033[31m'
DIM    = '\033[2m'
RESET  = '\033[0m'

def color_for(pct):
    if pct is None:
        return DIM
    if pct >= 90:
        return RED
    if pct >= 70:
        return YELLOW
    return GREEN

def fmt_pct(label, pct):
    if pct is None:
        return None
    return f'{color_for(pct)}{label} {int(pct)}%{RESET}'

cwd = (d.get('workspace') or {}).get('current_dir') or d.get('cwd') or ''
model = (d.get('model') or {}).get('display_name', '')
dir_name = os.path.basename(cwd.rstrip('/\\\\')) if cwd else ''

branch = ''
if cwd:
    try:
        r = subprocess.run(['git', '-C', cwd, 'symbolic-ref', '--short', 'HEAD'],
                           capture_output=True, text=True, timeout=2)
        if r.returncode == 0:
            branch = r.stdout.strip()
        else:
            r = subprocess.run(['git', '-C', cwd, 'rev-parse', '--short', 'HEAD'],
                               capture_output=True, text=True, timeout=2)
            if r.returncode == 0:
                branch = r.stdout.strip()
    except Exception:
        pass

ctx = d.get('context_window') or {}
ctx_pct = ctx.get('used_percentage')
ctx_used_tokens = ctx.get('total_input_tokens')
ctx_window_size = ctx.get('context_window_size')

def fmt_tokens(n):
    if n is None:
        return ''
    if n >= 1_000_000:
        return f'{n/1_000_000:.1f}M'.replace('.0M', 'M')
    if n >= 1000:
        return f'{n/1000:.1f}k'.replace('.0k', 'k')
    return str(n)
rate = d.get('rate_limits') or {}
five_h = (rate.get('five_hour') or {}).get('used_percentage')
five_h_resets = (rate.get('five_hour') or {}).get('resets_at')
seven_d = (rate.get('seven_day') or {}).get('used_percentage')

def fmt_reset(epoch):
    if epoch is None:
        return ''
    try:
        return datetime.datetime.fromtimestamp(int(epoch)).strftime('%H:%M')
    except Exception:
        return ''

parts = []
if dir_name:
    parts.append(dir_name)
if branch:
    parts.append(branch)
if model:
    parts.append(model)
ctx_part = fmt_pct('ctx', ctx_pct)
if ctx_part:
    used_str = fmt_tokens(ctx_used_tokens)
    total_str = fmt_tokens(ctx_window_size)
    if used_str and total_str:
        ctx_part = f'{ctx_part} {DIM}({used_str}/{total_str}){RESET}'
    elif used_str:
        ctx_part = f'{ctx_part} {DIM}({used_str}){RESET}'
    parts.append(ctx_part)
five_part = fmt_pct('5h', five_h)
if five_part:
    reset_str = fmt_reset(five_h_resets)
    if reset_str:
        five_part = f'{five_part} {DIM}({reset_str}){RESET}'
    parts.append(five_part)
seven_part = fmt_pct('7d', seven_d)
if seven_part:
    parts.append(seven_part)

sys.stdout.write(' | '.join(parts))
"
