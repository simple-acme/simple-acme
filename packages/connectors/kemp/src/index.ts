import { readFile } from 'node:fs/promises';
import { basename } from 'node:path';
import type { ActivateResult, BindResult, Connector, ConnectorContext, DeployResult, ProbeResult, RollbackResult, VerifyResult } from '@orchestrator/core';

async function ensureOk(response: Response, label: string): Promise<Response> {
  if (!response.ok) {
    const body = await response.text();
    throw new Error(`${label} failed (${response.status}): ${body}`);
  }
  return response;
}

export class KempConnector implements Connector {
  private getBaseUrl(ctx: ConnectorContext): string { return `https://${ctx.config.settings.host}/access`; }
  private getAuth(ctx: ConnectorContext): string {
    return Buffer.from(`${ctx.config.settings.user_env}:${ctx.config.settings.password_env}`).toString('base64');
  }

  /** Validates appliance reachability. */
  public async probe(ctx: ConnectorContext): Promise<ProbeResult> {
    const response = await fetch(`${this.getBaseUrl(ctx)}/stats`, { headers: { Authorization: `Basic ${this.getAuth(ctx)}` } });
    return { reachable: response.ok, auth_valid: response.ok, detail: response.statusText };
  }

  /** Uploads certificate and returns cert filename artifact reference. */
  public async deploy(ctx: ConnectorContext): Promise<DeployResult> {
    let fullchain: string;
    let key: string;
    try {
      fullchain = await readFile(ctx.event.fullchain_path, 'utf8');
      key = await readFile(ctx.event.key_path, 'utf8');
    } catch (error) {
      throw new Error(`Failed to read Kemp cert files: ${String(error)}`);
    }

    const form = new FormData();
    form.set('cert', fullchain);
    form.set('key', key);
    form.set('replace', '1');

    await ensureOk(await fetch(`${this.getBaseUrl(ctx)}/addcert`, {
      method: 'POST', headers: { Authorization: `Basic ${this.getAuth(ctx)}` }, body: form
    }), 'kemp deploy');

    return { artifact_ref: basename(ctx.event.fullchain_path) } as unknown as DeployResult;
  }

  /** Binds cert to target virtual service. */
  public async bind(ctx: ConnectorContext): Promise<BindResult> {
    await ensureOk(await fetch(`${this.getBaseUrl(ctx)}/modvs?vs=${ctx.config.settings.vs_id}&cert=${ctx.artifact_ref ?? ''}`, {
      headers: { Authorization: `Basic ${this.getAuth(ctx)}` }
    }), 'kemp bind');
    return { detail: 'bound' };
  }

  /** Kemp activation is immediate and does not require commit. */
  public async activate(_ctx: ConnectorContext): Promise<ActivateResult> {
    return { detail: 'noop' };
  }

  /** Verifies cert assignment. */
  public async verify(ctx: ConnectorContext): Promise<VerifyResult> {
    const response = await ensureOk(await fetch(`${this.getBaseUrl(ctx)}/showvs?vs=${ctx.config.settings.vs_id}`, {
      headers: { Authorization: `Basic ${this.getAuth(ctx)}` }
    }), 'kemp verify');
    const text = await response.text();
    return { verified: text.includes(`CertFile=${ctx.artifact_ref ?? ''}`), detail: text };
  }

  /** Rebinds previous cert. */
  public async rollback(ctx: ConnectorContext): Promise<RollbackResult> {
    if (!ctx.previous_artifact_ref) {
      return { restored: false, detail: 'no previous cert' };
    }
    await ensureOk(await fetch(`${this.getBaseUrl(ctx)}/modvs?vs=${ctx.config.settings.vs_id}&cert=${ctx.previous_artifact_ref}`, {
      headers: { Authorization: `Basic ${this.getAuth(ctx)}` }
    }), 'kemp rollback');
    return { restored: true };
  }
}
