import {
  executeRollback,
  type CertificateEvent,
  type Connector,
  type ConnectorContext,
  type ConnectorJobRecord,
  type ConnectorStep,
  RollbackReason,
  StateStore,
  shouldRollback,
  withRetry
} from '@orchestrator/core';

/** Runs a connector job through probe/deploy/bind/activate/verify. */
export async function runJob(job: ConnectorJobRecord, connector: Connector, event: CertificateEvent, store: StateStore): Promise<void> {
  const verifyMaxAttempts = Number(process.env.VERIFY_MAX_ATTEMPTS ?? '3');
  const activateTimeoutMs = Number(process.env.ACTIVATE_TIMEOUT_MS ?? '120000');

  const context: ConnectorContext = {
    job_id: job.job_id,
    event,
    config: {
      connector_type: job.connector_type,
      label: job.connector_type,
      settings: {}
    },
    artifact_ref: job.artifact_ref,
    previous_artifact_ref: job.previous_artifact_ref
  };

  const steps: ConnectorStep[] = ['probe', 'deploy', 'bind', 'activate', 'verify'];

  for (const step of steps) {
    try {
      store.updateStep(job.job_id, step, 'running');
      if (step === 'probe') {
        await withRetry(() => connector.probe(context), { label: `${job.job_id}:probe` });
      } else if (step === 'deploy') {
        const deployResult = await withRetry(() => connector.deploy(context), { label: `${job.job_id}:deploy` });
        context.artifact_ref = deployResult.artifact_ref;
        store.updateStep(job.job_id, 'deploy', 'running', {
          artifact_ref: deployResult.artifact_ref,
          previous_artifact_ref: deployResult.artifact_ref
        });
        const probeAfterDeploy = await withRetry(() => connector.probe(context), { label: `${job.job_id}:probe-post-deploy` });
        if (!probeAfterDeploy.reachable && shouldRollback(RollbackReason.DeployArtifactInvalid)) {
          throw new Error('deployed artifact is invalid: target unreachable');
        }
      } else if (step === 'bind') {
        await withRetry(() => connector.bind(context), { label: `${job.job_id}:bind` });
      } else if (step === 'activate') {
        await withRetry(
          () => Promise.race([
            connector.activate(context),
            new Promise<never>((_, reject) => setTimeout(() => reject(new Error('activate timeout')), activateTimeoutMs))
          ]),
          { label: `${job.job_id}:activate` }
        );
      } else if (step === 'verify') {
        const verify = await withRetry(() => connector.verify(context), {
          label: `${job.job_id}:verify`,
          maxAttempts: verifyMaxAttempts
        });
        if (!verify.verified) {
          throw new Error(`verification returned false: ${verify.detail ?? 'unknown'}`);
        }
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      store.updateStep(job.job_id, step, 'failed', { error_detail: message });
      const reason = step === 'verify'
        ? RollbackReason.VerifyRetryExhausted
        : step === 'activate' && message.includes('timeout')
          ? RollbackReason.ActivateTimeout
          : RollbackReason.DeployArtifactInvalid;
      if (shouldRollback(reason)) {
        await executeRollback(context, connector, store);
      }
      return;
    }
  }

  store.updateStep(job.job_id, 'verify', 'succeeded', { artifact_ref: context.artifact_ref });
}
