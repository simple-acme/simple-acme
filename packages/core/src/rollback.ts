import type { Connector } from './connector.js';
import { log } from './logger.js';
import { StateStore } from './state-store.js';
import type { ConnectorContext, ConnectorStep } from './types.js';

export enum RollbackReason {
  VerifyRetryExhausted = 'verify_retry_exhausted',
  ActivateTimeout = 'activate_timeout',
  DeployArtifactInvalid = 'deploy_artifact_invalid',
  FailFastSiblingFailure = 'fail_fast_sibling_failure'
}

/** Rollback decision helper. */
export function shouldRollback(reason: RollbackReason): boolean {
  return Object.values(RollbackReason).includes(reason);
}

/** Executes idempotent rollback and persists final status. */
export async function executeRollback(ctx: ConnectorContext, connector: Connector, store: StateStore): Promise<void> {
  const existing = store.getJob(ctx.job_id);
  if (!existing) {
    return;
  }
  if (existing.status === 'rolled_back' || existing.status === 'rolled_back_failed') {
    return;
  }

  try {
    await connector.rollback(ctx);
    store.updateStep(ctx.job_id, 'rollback' satisfies ConnectorStep, 'rolled_back');
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    store.updateStep(ctx.job_id, 'rollback', 'rolled_back_failed', { error_detail: message });
    log('error', `Rollback failed for job ${ctx.job_id}: ${message}`);
  }
}
