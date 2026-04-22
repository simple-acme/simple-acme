import { describe, expect, it } from 'vitest';
import { StateStore } from '../src/state-store.js';

describe('StateStore', () => {
  it('creates jobs and fetches by id', () => {
    const store = new StateStore(':memory:');
    store.createJob({
      job_id: 'job-1',
      renewal_id: 'r1',
      deployment_policy_id: 'p1',
      connector_type: 'f5_bigip',
      step: 'probe',
      status: 'pending',
      artifact_ref: null,
      previous_artifact_ref: null,
      attempt: 0,
      error_detail: null
    });

    const job = store.getJob('job-1');
    expect(job?.status).toBe('pending');
  });

  it('updates step and status transitions', () => {
    const store = new StateStore(':memory:');
    store.createJob({
      job_id: 'job-2',
      renewal_id: 'r2',
      deployment_policy_id: 'p1',
      connector_type: 'kemp',
      step: 'probe',
      status: 'pending',
      artifact_ref: null,
      previous_artifact_ref: null,
      attempt: 0,
      error_detail: null
    });

    store.updateStep('job-2', 'deploy', 'running', { attempt: 1 });
    store.updateStep('job-2', 'verify', 'succeeded', { artifact_ref: 'cert.pem' });
    const job = store.getJob('job-2');
    expect(job?.step).toBe('verify');
    expect(job?.status).toBe('succeeded');
    expect(job?.artifact_ref).toBe('cert.pem');
  });

  it('gets jobs by renewal id', () => {
    const store = new StateStore(':memory:');
    for (let i = 0; i < 3; i += 1) {
      store.createJob({
        job_id: `job-r3-${i}`,
        renewal_id: 'r3',
        deployment_policy_id: 'p1',
        connector_type: 'citrix_adc',
        step: 'probe',
        status: i === 0 ? 'pending' : 'running',
        artifact_ref: null,
        previous_artifact_ref: null,
        attempt: 0,
        error_detail: null
      });
    }
    expect(store.getJobsByRenewal('r3')).toHaveLength(3);
  });

  it('returns only pending jobs', () => {
    const store = new StateStore(':memory:');
    store.createJob({
      job_id: 'job-p',
      renewal_id: 'rp',
      deployment_policy_id: 'p1',
      connector_type: 'f5_bigip',
      step: 'probe',
      status: 'pending',
      artifact_ref: null,
      previous_artifact_ref: null,
      attempt: 0,
      error_detail: null
    });
    store.createJob({
      job_id: 'job-np',
      renewal_id: 'rp',
      deployment_policy_id: 'p1',
      connector_type: 'f5_bigip',
      step: 'probe',
      status: 'running',
      artifact_ref: null,
      previous_artifact_ref: null,
      attempt: 0,
      error_detail: null
    });

    expect(store.getPendingJobs().map((j) => j.job_id)).toEqual(['job-p']);
  });
});
