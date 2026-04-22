export type LogLevel = 'info' | 'warn' | 'error';

/** Logs with a stable orchestrator prefix. */
export function log(level: LogLevel, message: string): void {
  process.stdout.write(`[orchestrator] [${level.toUpperCase()}] ${message}\n`);
}
