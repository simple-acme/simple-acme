import { describe, expect, it } from 'vitest';
import { executeRollback } from '../src/rollback.js';
import { StateStore } from '../src/state-store.js';
import type { Connector, ConnectorContext } from '../src/index.js';

function createContext(jobId: string): ConnectorContext {
  return {
    job_id: jobId,
    event: {
      event: 'certificate.renewed',
      renewal_id: 'r1',
      deployment_policy_id: 'p1',
      domain: 'example.com',
      cert_path: '/tmp/cert.pem',
      key_path: '/tmp/key.pem',
      fullchain_path: '/tmp/fullchain.pem',
      thumbprint: 'abc',
      issuer: 'issuer',
      not_before: new Date().toISOString(),
      not_after: new Date().toISOString()
    },
    config: { connector_type: 'mock', label: 'Mock', settings: {} },
    artifact_ref: 'new-cert',
    previous_artifact_ref: 'old-cert'
  };
}

describe('executeRollback', () => {
  it('sets rolled_back when rollback succeeds', async () => {
    const store = new StateStore(':memory:');
    store.createJob({
      job_id: 'job-rb-ok', renewal_id: 'r1', deployment_policy_id: 'p1', connector_type: 'mock',
      step: 'verify', status: 'failed', artifact_ref: 'new-cert', previous_artifact_ref: 'old-cert', attempt: 3, error_detail: 'x'
    });

    const connector: Connector = {
      probe: async () => ({ reachable: true, auth_valid: true }),
      deploy: async () => ({ artifact_ref: 'x' }),
      bind: async () => ({}),
      activate: async () => ({}),
      verify: async () => ({ verified: true }),
      rollback: async () => ({ restored: true })
    };

    await executeRollback(createContext('job-rb-ok'), connector, store);
    expect(store.getJob('job-rb-ok')?.status).toBe('rolled_back');
  });

  it('sets rolled_back_failed when rollback fails', async () => {
    const store = new StateStore(':memory:');
    store.createJob({
      job_id: 'job-rb-fail', renewal_id: 'r1', deployment_policy_id: 'p1', connector_type: 'mock',
      step: 'verify', status: 'failed', artifact_ref: 'new-cert', previous_artifact_ref: 'old-cert', attempt: 3, error_detail: 'x'
    });

    const connector: Connector = {
      probe: async () => ({ reachable: true, auth_valid: true }),
      deploy: async () => ({ artifact_ref: 'x' }),
      bind: async () => ({}),
      activate: async () => ({}),
      verify: async () => ({ verified: true }),
      rollback: async () => { throw new Error('rollback boom'); }
    };

    await executeRollback(createContext('job-rb-fail'), connector, store);
    expect(store.getJob('job-rb-fail')?.status).toBe('rolled_back_failed');
  });
});
