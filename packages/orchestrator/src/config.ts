import { ConfigValidationError } from '@orchestrator/core';

export interface OrchestratorConfig {
  policyFile: string;
  verifyMaxAttempts: number;
  activateTimeoutMs: number;
  dbPath: string;
  host: string;
  port: number;
}

/** Reads and validates startup config from process.env once. */
export function loadConfig(env: NodeJS.ProcessEnv = process.env): OrchestratorConfig {
  const missing: string[] = [];
  if (!env.DB_PATH) missing.push('DB_PATH');
  if (missing.length > 0) {
    throw new ConfigValidationError(`Missing required environment variables: ${missing.join(', ')}`);
  }

  return {
    policyFile: env.POLICY_FILE ?? './policies.json',
    verifyMaxAttempts: Number(env.VERIFY_MAX_ATTEMPTS ?? '3'),
    activateTimeoutMs: Number(env.ACTIVATE_TIMEOUT_MS ?? '120000'),
    dbPath: env.DB_PATH,
    host: env.HOST ?? '0.0.0.0',
    port: Number(env.PORT ?? '3000')
  };
}
