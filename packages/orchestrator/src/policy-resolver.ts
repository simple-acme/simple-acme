import { readFile } from 'node:fs/promises';
import { ConfigValidationError, PolicyNotFoundError } from '@orchestrator/core';
import type { ConnectorConfig, DeploymentPolicy, FanoutPolicy } from '@orchestrator/core';

function isFanoutPolicy(value: string): value is FanoutPolicy {
  return value === 'fail-fast' || value === 'best-effort' || value === 'quorum';
}

/** Resolves a deployment policy from the configured policy file. */
export async function resolvePolicy(deploymentPolicyId: string): Promise<DeploymentPolicy> {
  const policyFile = process.env.POLICY_FILE ?? './policies.json';
  let raw: string;
  try {
    raw = await readFile(policyFile, 'utf8');
  } catch (error) {
    throw new ConfigValidationError(`Unable to read policy file ${policyFile}: ${String(error)}`);
  }

  const parsed = JSON.parse(raw) as unknown;
  if (!Array.isArray(parsed)) {
    throw new ConfigValidationError('Policy file must contain a top-level array');
  }

  const policy = parsed.find((item) => {
    if (typeof item !== 'object' || item === null) return false;
    const candidate = item as { policy_id?: string };
    return candidate.policy_id === deploymentPolicyId;
  });

  if (!policy || typeof policy !== 'object') {
    throw new PolicyNotFoundError(deploymentPolicyId);
  }

  const p = policy as {
    policy_id?: string;
    connectors?: ConnectorConfig[];
    fanout_policy?: string;
    quorum_threshold?: number;
  };

  if (!p.policy_id || !Array.isArray(p.connectors) || !p.fanout_policy || !isFanoutPolicy(p.fanout_policy)) {
    throw new ConfigValidationError(`Invalid policy schema for ${deploymentPolicyId}`);
  }

  const connectors = p.connectors.map((connector) => {
    const resolvedSettings: Record<string, string> = {};
    for (const [key, value] of Object.entries(connector.settings)) {
      if (key.endsWith('_env')) {
        const envValue = process.env[value];
        if (!envValue) {
          throw new ConfigValidationError(`Missing environment variable for ${key}: ${value}`);
        }
        resolvedSettings[key] = envValue;
      } else {
        resolvedSettings[key] = value;
      }
    }

    return {
      connector_type: connector.connector_type,
      label: connector.label,
      settings: resolvedSettings
    };
  });

  return {
    policy_id: p.policy_id,
    connectors,
    fanout_policy: p.fanout_policy,
    quorum_threshold: p.quorum_threshold
  };
}
