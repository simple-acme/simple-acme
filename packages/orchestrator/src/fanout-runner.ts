import { randomUUID } from 'node:crypto';
import { executeRollback, log, RollbackReason, StateStore, shouldRollback, type CertificateEvent, type ConnectorJobRecord, type DeploymentPolicy } from '@orchestrator/core';
import { ConnectorRegistry } from './connector-registry.js';
import { runJob } from './job-runner.js';

/** Runs connector jobs according to fan-out policy. */
export async function runFanout(
  event: CertificateEvent,
  policy: DeploymentPolicy,
  connectorRegistry: ConnectorRegistry,
  store: StateStore
): Promise<void> {
  const fanoutPolicy = policy.fanout_policy ?? 'fail-fast';
  const jobs: ConnectorJobRecord[] = policy.connectors.map((config) => ({
    job_id: randomUUID(),
    renewal_id: event.renewal_id,
    deployment_policy_id: policy.policy_id,
    connector_type: config.connector_type,
    step: 'probe',
    status: 'pending',
    artifact_ref: null,
    previous_artifact_ref: null,
    attempt: 0,
    error_detail: null,
    created_at: '',
    updated_at: ''
  }));

  jobs.forEach((job) => store.createJob({ ...job, created_at: undefined as never, updated_at: undefined as never }));

  if (fanoutPolicy === 'fail-fast') {
    const succeeded: ConnectorJobRecord[] = [];
    for (const job of jobs) {
      const connector = connectorRegistry.resolve(job.connector_type);
      await runJob(job, connector, event, store);
      const latest = store.getJob(job.job_id);
      if (latest?.status === 'succeeded') {
        succeeded.push({ ...job, ...latest });
      } else if (latest?.status !== 'succeeded') {
        if (shouldRollback(RollbackReason.FailFastSiblingFailure)) {
          await Promise.all(succeeded.map(async (doneJob) => {
            const doneConnector = connectorRegistry.resolve(doneJob.connector_type);
            await executeRollback({
              job_id: doneJob.job_id,
              event,
              config: { connector_type: doneJob.connector_type, label: doneJob.connector_type, settings: {} },
              artifact_ref: doneJob.artifact_ref,
              previous_artifact_ref: doneJob.previous_artifact_ref
            }, doneConnector, store);
          }));
        }
        break;
      }
    }
    return;
  }

  const settled = await Promise.allSettled(jobs.map(async (job) => {
    const connector = connectorRegistry.resolve(job.connector_type);
    await runJob(job, connector, event, store);
    return store.getJob(job.job_id);
  }));

  if (fanoutPolicy === 'best-effort') {
    settled.forEach((result) => {
      if (result.status === 'rejected') {
        log('warn', `best-effort connector failed: ${String(result.reason)}`);
      }
    });
    return;
  }

  const successJobs = settled
    .filter((result): result is PromiseFulfilledResult<ConnectorJobRecord | undefined> => result.status === 'fulfilled')
    .map((result) => result.value)
    .filter((job): job is ConnectorJobRecord => Boolean(job && job.status === 'succeeded'));

  const threshold = policy.quorum_threshold ?? 1;
  if (successJobs.length < threshold) {
    await Promise.all(successJobs.map(async (job) => {
      const connector = connectorRegistry.resolve(job.connector_type);
      await executeRollback({
        job_id: job.job_id,
        event,
        config: { connector_type: job.connector_type, label: job.connector_type, settings: {} },
        artifact_ref: job.artifact_ref,
        previous_artifact_ref: job.previous_artifact_ref
      }, connector, store);
    }));
  }
}
