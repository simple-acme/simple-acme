import type {
  ActivateResult,
  BindResult,
  ConnectorContext,
  DeployResult,
  ProbeResult,
  RollbackResult,
  VerifyResult
} from './types.js';

export interface Connector {
  probe(ctx: ConnectorContext): Promise<ProbeResult>;
  deploy(ctx: ConnectorContext): Promise<DeployResult>;
  bind(ctx: ConnectorContext): Promise<BindResult>;
  activate(ctx: ConnectorContext): Promise<ActivateResult>;
  verify(ctx: ConnectorContext): Promise<VerifyResult>;
  rollback(ctx: ConnectorContext): Promise<RollbackResult>;
}
