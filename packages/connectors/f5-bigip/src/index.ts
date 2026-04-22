import { readFile } from 'node:fs/promises';
import type { ActivateResult, BindResult, Connector, ConnectorContext, DeployResult, ProbeResult, RollbackResult, VerifyResult } from '@orchestrator/core';

async function ensureOk(response: Response, label: string): Promise<Response> {
  if (!response.ok) {
    const body = await response.text();
    throw new Error(`${label} failed (${response.status}): ${body}`);
  }
  return response;
}

export class F5BigIpConnector implements Connector {
  private getBaseUrl(ctx: ConnectorContext): string {
    return `https://${ctx.config.settings.host}/mgmt/tm`;
  }

  private getHeaders(ctx: ConnectorContext): HeadersInit {
    return { Authorization: `Bearer ${ctx.config.settings.token_env}`, 'Content-Type': 'application/json' };
  }

  /** Connectivity and auth probe. */
  public async probe(ctx: ConnectorContext): Promise<ProbeResult> {
    const response = await fetch(`${this.getBaseUrl(ctx)}/sys/version`, { headers: this.getHeaders(ctx) });
    return { reachable: response.ok, auth_valid: response.ok, detail: response.statusText };
  }

  /** Imports cert/key and returns cert artifact name. */
  public async deploy(ctx: ConnectorContext): Promise<DeployResult> {
    let certPem: string;
    let keyPem: string;
    try {
      certPem = await readFile(ctx.event.cert_path, 'utf8');
      keyPem = await readFile(ctx.event.key_path, 'utf8');
    } catch (error) {
      throw new Error(`Failed to read certificate input files: ${String(error)}`);
    }
    const artifactRef = `${ctx.event.domain}-${ctx.event.thumbprint}`;
    await ensureOk(await fetch(`${this.getBaseUrl(ctx)}/sys/crypto/cert`, {
      method: 'POST', headers: this.getHeaders(ctx), body: JSON.stringify({ name: artifactRef, sourceText: certPem })
    }), 'f5 cert import');
    await ensureOk(await fetch(`${this.getBaseUrl(ctx)}/sys/crypto/key`, {
      method: 'POST', headers: this.getHeaders(ctx), body: JSON.stringify({ name: artifactRef, sourceText: keyPem })
    }), 'f5 key import');
    return { artifact_ref: artifactRef } as unknown as DeployResult;
  }

  /** Binds imported artifact to SSL profile. */
  public async bind(ctx: ConnectorContext): Promise<BindResult> {
    const profile = ctx.config.settings.ssl_profile;
    const certRef = ctx.artifact_ref;
    await ensureOk(await fetch(`${this.getBaseUrl(ctx)}/ltm/profile/client-ssl/${profile}`, {
      method: 'PATCH', headers: this.getHeaders(ctx), body: JSON.stringify({ cert: certRef, key: certRef })
    }), 'f5 bind');
    return { detail: 'bound' };
  }

  /** Saves running configuration. */
  public async activate(ctx: ConnectorContext): Promise<ActivateResult> {
    await ensureOk(await fetch(`${this.getBaseUrl(ctx)}/sys/config`, {
      method: 'POST', headers: this.getHeaders(ctx), body: JSON.stringify({ command: 'save' })
    }), 'f5 save');
    return { detail: 'saved' };
  }

  /** Confirms profile references the new certificate. */
  public async verify(ctx: ConnectorContext): Promise<VerifyResult> {
    const profile = ctx.config.settings.ssl_profile;
    const response = await ensureOk(await fetch(`${this.getBaseUrl(ctx)}/ltm/profile/client-ssl/${profile}`, {
      headers: this.getHeaders(ctx)
    }), 'f5 verify');
    const body = await response.json() as { cert?: string };
    return { verified: body.cert === ctx.artifact_ref, detail: `actual=${body.cert ?? 'unset'}` };
  }

  /** Restores previously bound artifact. */
  public async rollback(ctx: ConnectorContext): Promise<RollbackResult> {
    const previous = ctx.previous_artifact_ref;
    if (!previous) {
      return { restored: false, detail: 'no previous artifact' };
    }
    await ensureOk(await fetch(`${this.getBaseUrl(ctx)}/ltm/profile/client-ssl/${ctx.config.settings.ssl_profile}`, {
      method: 'PATCH', headers: this.getHeaders(ctx), body: JSON.stringify({ cert: previous, key: previous })
    }), 'f5 rollback');
    return { restored: true };
  }
}
