#!/usr/bin/env node
// PreToolUse guard for Bash / PowerShell (docs/standards/safety.md): never affect anything outside the task.
// Blocks blanket process kills and wide Docker clean-ups; asks before stopping/replacing compose services.
// Stopping one process by PID (`taskkill /PID 1234`, `kill 1234`, `Stop-Process -Id 1234`) stays allowed.
let input = ''
process.stdin.setEncoding('utf8')
process.stdin.on('data', (c) => (input += c))
process.stdin.on('end', () => {
  let command = ''
  try {
    command = String(JSON.parse(input)?.tool_input?.command ?? '')
  } catch {
    process.exit(0) // not our business; let the normal permission flow decide
  }

  const deny = [
    [/\btaskkill\b[^\n|;&]*\/IM\b/i, 'taskkill /IM kills every process with that image name'],
    [/\b(pkill|killall)\b/i, 'pkill/killall kill by name'],
    [/\bStop-Process\b[^\n|;&]*-(Name|ProcessName)\b/i, 'Stop-Process -Name kills by name'],
    [/\b(Get-Process|gps|ps|pgrep|tasklist)\b[^\n;]*\|[^\n;]*\b(Stop-Process|spps|kill|taskkill)\b/i, 'piping a process list into kill/Stop-Process kills by name'],
    [/\bwmic\b[^\n]*\bprocess\b[^\n]*\b(delete|terminate)\b/i, 'wmic process delete kills by query'],
    [/\bkill\b\s+(-\w+\s+)*(-1|0)\b/, 'kill -1 / kill 0 signals every process'],
    [/\bdocker\b[^\n]*\b(system|container|image|volume|network|builder)\s+prune\b/i, 'docker prune removes resources this task did not create'],
    [/\bdocker\b[^\n]*\b(rm|stop|kill)\b[^\n]*\$\(\s*docker\b/i, 'docker rm/stop/kill over $(docker ps …) hits every container'],
    [/\bdocker\b[^\n]*\bvolume\s+rm\b/i, 'removing Docker volumes deletes the relay\'s pairings'],
  ]
  const ask = [
    [/\bdocker[\s-]+compose\b[^\n]*\b(down|stop|restart|kill|rm|up)\b/i, 'this stops or replaces the user\'s running relay container'],
    [/\bStop-Service\b|\bRestart-Service\b|\bsc(\.exe)?\s+(stop|delete)\b/i, 'this stops a Windows service'],
  ]

  for (const [re, why] of deny) {
    if (re.test(command)) {
      process.stderr.write(
        `Blocked by .claude/hooks/guard-commands.mjs: ${why}. Stop only processes you started, by PID ` +
          '(see docs/standards/safety.md). If the user explicitly asked for this, they can run it themselves.\n',
      )
      process.exit(2)
    }
  }
  for (const [re, why] of ask) {
    if (re.test(command)) {
      process.stdout.write(
        JSON.stringify({
          hookSpecificOutput: {
            hookEventName: 'PreToolUse',
            permissionDecision: 'ask',
            permissionDecisionReason: `League Remote safety rules: ${why}.`,
          },
        }),
      )
      process.exit(0)
    }
  }
  process.exit(0)
})
