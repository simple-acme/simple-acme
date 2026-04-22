export interface CertificateEvent {
  event: 'certificate.issued' | 'certificate.renewed' | 'certificate.failed';
  renewal_id: string;
  deployment_policy_id: string;
  domain: string;
  cert_path: string;
  key_path: string;
  fullchain_path: string;
  thumbprint: string;
  issuer: string;
  not_before: string;
  not_after: string;
}

export type ConnectorStep = 'probe' | 'deploy' | 'bind' | 'activate' | 'verify' | 'rollback';

export type JobStatus = 'pending' | 'running' | 'succeeded' | 'failed' | 'rolled_back' | 'rolled_back_failed';

export interface ConnectorJobRecord {
  job_id: string;
  renewal_id: string;
  deployment_policy_id: string;
  connector_type: string;
  step: ConnectorStep;
  status: JobStatus;
  artifact_ref: string | null;
  previous_artifact_ref: string | null;
  attempt: number;
  error_detail: string | null;
  created_at: string;
  updated_at: string;
}

export type FanoutPolicy = 'fail-fast' | 'best-effort' | 'quorum';

export interface DeploymentPolicy {
  policy_id: string;
  connectors: ConnectorConfig[];
  fanout_policy: FanoutPolicy;
  quorum_threshold?: number;
}

export interface ConnectorConfig {
  connector_type: string;
  label: string;
  settings: Record<string, string>;
}

export interface ConnectorContext {
  job_id: string;
  event: CertificateEvent;
  config: ConnectorConfig;
  artifact_ref: string | null;
  previous_artifact_ref: string | null;
}

export interface ProbeResult { reachable: boolean; auth_valid: boolean; detail?: string }
export interface DeployResult { artifact_ref: string; detail?: string }
export interface BindResult { detail?: string }
export interface ActivateResult { detail?: string }
export interface VerifyResult { verified: boolean; detail?: string }
export interface RollbackResult { restored: boolean; detail?: string }
