import { describe, expect, it } from 'vitest';
import { StateStore, type CertificateEvent, type DeploymentPolicy } from '@orchestrator/core';
import { ConnectorRegistry } from '../../src/connector-registry.js';
import { runFanout } from '../../src/fanout-runner.js';
import { MockConnector } from './mock-connector.js';

const event: CertificateEvent = {
  event: 'certificate.renewed',
  renewal_id: 'renew-1',
  deployment_policy_id: 'policy-1',
  domain: 'example.com',
  cert_path: '/tmp/cert.pem',
  key_path: '/tmp/key.pem',
  fullchain_path: '/tmp/fullchain.pem',
  thumbprint: 'thumb-1',
  issuer: 'issuer',
  not_before: new Date().toISOString(),
  not_after: new Date().toISOString()
};

function policy(fanout: DeploymentPolicy['fanout_policy'], connectors: string[], threshold?: number): DeploymentPolicy {
  return {
    policy_id: 'policy-1',
    fanout_policy: fanout,
    quorum_threshold: threshold,
    connectors: connectors.map((type) => ({ connector_type: type, label: type, settings: {} }))
  };
}

describe('fanout integration', () => {
  it('happy path single connector succeeds', async () => {
    const store = new StateStore(':memory:');
    const registry = new ConnectorRegistry();
    registry.register('c1', new MockConnector());

    await runFanout(event, policy('fail-fast', ['c1']), registry, store);
    expect(store.getJobsByRenewal(event.renewal_id)[0]?.status).toBe('succeeded');
  });

  it('verify failure triggers rollback', async () => {
    const store = new StateStore(':memory:');
    const registry = new ConnectorRegistry();
    const connector = new MockConnector({ verify: 'fail' });
    registry.register('c1', connector);

    await runFanout(event, policy('fail-fast', ['c1']), registry, store);
    expect(store.getJobsByRenewal(event.renewal_id)[0]?.status).toBe('rolled_back');
    expect(connector.rollbackCalls).toBeGreaterThan(0);
  });

  it('rollback failure marks rolled_back_failed', async () => {
    const store = new StateStore(':memory:');
    const registry = new ConnectorRegistry();
    registry.register('c1', new MockConnector({ verify: 'fail', rollback: 'fail' }));

    await runFanout(event, policy('fail-fast', ['c1']), registry, store);
    expect(store.getJobsByRenewal(event.renewal_id)[0]?.status).toBe('rolled_back_failed');
  });

  it('fail-fast rolls back first when second fails', async () => {
    const store = new StateStore(':memory:');
    const registry = new ConnectorRegistry();
    const first = new MockConnector();
    const second = new MockConnector({ deploy: 'fail' });
    registry.register('c1', first);
    registry.register('c2', second);

    await runFanout(event, policy('fail-fast', ['c1', 'c2']), registry, store);
    const jobs = store.getJobsByRenewal(event.renewal_id);
    expect(jobs.some((j) => j.connector_type === 'c1' && j.status === 'rolled_back')).toBe(true);
    expect(jobs.some((j) => j.connector_type === 'c2' && j.status !== 'succeeded')).toBe(true);
  });

  it('best-effort keeps first succeeded when second fails', async () => {
    const store = new StateStore(':memory:');
    const registry = new ConnectorRegistry();
    registry.register('c1', new MockConnector());
    registry.register('c2', new MockConnector({ deploy: 'fail' }));

    await runFanout(event, policy('best-effort', ['c1', 'c2']), registry, store);
    const jobs = store.getJobsByRenewal(event.renewal_id);
    expect(jobs.some((j) => j.connector_type === 'c1' && j.status === 'succeeded')).toBe(true);
    expect(jobs.some((j) => j.connector_type === 'c2' && j.status !== 'succeeded')).toBe(true);
  });

  it('quorum met with two successes out of three', async () => {
    const store = new StateStore(':memory:');
    const registry = new ConnectorRegistry();
    registry.register('c1', new MockConnector());
    registry.register('c2', new MockConnector());
    registry.register('c3', new MockConnector({ deploy: 'fail' }));

    await runFanout(event, policy('quorum', ['c1', 'c2', 'c3'], 2), registry, store);
    const jobs = store.getJobsByRenewal(event.renewal_id);
    expect(jobs.filter((j) => j.status === 'succeeded')).toHaveLength(2);
    expect(jobs.some((j) => j.status !== 'succeeded')).toBe(true);
  });
});
