import type { ActivateResult, BindResult, Connector, ConnectorContext, DeployResult, ProbeResult, RollbackResult, VerifyResult } from '@orchestrator/core';

export interface MockBehavior {
  probe?: 'succeed' | 'fail';
  deploy?: 'succeed' | 'fail';
  bind?: 'succeed' | 'fail';
  activate?: 'succeed' | 'fail';
  verify?: 'succeed' | 'fail';
  rollback?: 'succeed' | 'fail';
}

export class MockConnector implements Connector {
  public rollbackCalls = 0;

  public constructor(private readonly behavior: MockBehavior = {}) {}

  private shouldFail(step: keyof MockBehavior): boolean {
    return (this.behavior[step] ?? 'succeed') === 'fail';
  }

  public async probe(_ctx: ConnectorContext): Promise<ProbeResult> {
    if (this.shouldFail('probe')) throw new Error('mock step failure');
    return { reachable: true, auth_valid: true };
  }

  public async deploy(ctx: ConnectorContext): Promise<DeployResult> {
    if (this.shouldFail('deploy')) throw new Error('mock step failure');
    return { artifact_ref: `${ctx.event.thumbprint}-artifact` };
  }

  public async bind(_ctx: ConnectorContext): Promise<BindResult> {
    if (this.shouldFail('bind')) throw new Error('mock step failure');
    return { detail: 'bound' };
  }

  public async activate(_ctx: ConnectorContext): Promise<ActivateResult> {
    if (this.shouldFail('activate')) throw new Error('mock step failure');
    return { detail: 'activated' };
  }

  public async verify(_ctx: ConnectorContext): Promise<VerifyResult> {
    if (this.shouldFail('verify')) throw new Error('mock step failure');
    return { verified: true };
  }

  public async rollback(_ctx: ConnectorContext): Promise<RollbackResult> {
    this.rollbackCalls += 1;
    if (this.shouldFail('rollback')) throw new Error('mock step failure');
    return { restored: true };
  }
}
