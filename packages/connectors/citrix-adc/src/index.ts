import type { ActivateResult, BindResult, Connector, ConnectorContext, DeployResult, ProbeResult, RollbackResult, VerifyResult } from '@orchestrator/core';

async function ensureOk(response: Response, label: string): Promise<Response> {
  if (!response.ok) {
    const body = await response.text();
    throw new Error(`${label} failed (${response.status}): ${body}`);
  }
  return response;
}

export class CitrixAdcConnector implements Connector {
  private sessionToken: string | null = null;

  private baseUrl(ctx: ConnectorContext): string {
    return `https://${ctx.config.settings.host}/nitro/v1`;
  }

  private async token(ctx: ConnectorContext): Promise<string> {
    if (this.sessionToken) {
      return this.sessionToken;
    }
    const response = await ensureOk(await fetch(`${this.baseUrl(ctx)}/config/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ login: { username: ctx.config.settings.user_env, password: ctx.config.settings.password_env } })
    }), 'citrix login');
    const raw = await response.json() as { sessionid?: string };
    this.sessionToken = raw.sessionid ?? '';
    return this.sessionToken;
  }

  private async headers(ctx: ConnectorContext): Promise<HeadersInit> {
    return { 'Content-Type': 'application/json', Cookie: `NITRO_AUTH_TOKEN=${await this.token(ctx)}` };
  }

  /** Probes appliance version endpoint. */
  public async probe(ctx: ConnectorContext): Promise<ProbeResult> {
    const response = await fetch(`${this.baseUrl(ctx)}/config/nsversion`, { headers: await this.headers(ctx) });
    return { reachable: response.ok, auth_valid: response.ok, detail: response.statusText };
  }

  /** Creates/updates certkey object and returns thumbprint reference. */
  public async deploy(ctx: ConnectorContext): Promise<DeployResult> {
    await ensureOk(await fetch(`${this.baseUrl(ctx)}/config/sslcertkey`, {
      method: 'POST',
      headers: await this.headers(ctx),
      body: JSON.stringify({ certkey: { certkey: ctx.event.thumbprint, cert: ctx.event.cert_path, key: ctx.event.key_path } })
    }), 'citrix deploy');
    return { artifact_ref: ctx.event.thumbprint } as unknown as DeployResult;
  }

  /** Binds certkey to vserver. */
  public async bind(ctx: ConnectorContext): Promise<BindResult> {
    await ensureOk(await fetch(`${this.baseUrl(ctx)}/config/sslvserver_sslcertkey_binding/${ctx.config.settings.vserver}`, {
      method: 'PUT',
      headers: await this.headers(ctx),
      body: JSON.stringify({ sslvserver_sslcertkey_binding: { certkeyname: ctx.artifact_ref } })
    }), 'citrix bind');
    return { detail: 'bound' };
  }

  /** Saves appliance config. */
  public async activate(ctx: ConnectorContext): Promise<ActivateResult> {
    await ensureOk(await fetch(`${this.baseUrl(ctx)}/config/nsconfig?action=save`, {
      method: 'POST',
      headers: await this.headers(ctx)
    }), 'citrix save');
    return { detail: 'saved' };
  }

  /** Verifies binding certkey. */
  public async verify(ctx: ConnectorContext): Promise<VerifyResult> {
    const response = await ensureOk(await fetch(`${this.baseUrl(ctx)}/config/sslvserver_sslcertkey_binding/${ctx.config.settings.vserver}`, {
      headers: await this.headers(ctx)
    }), 'citrix verify');
    const body = await response.text();
    return { verified: body.includes(ctx.artifact_ref ?? ''), detail: body };
  }

  /** Restores previous certkey binding. */
  public async rollback(ctx: ConnectorContext): Promise<RollbackResult> {
    if (!ctx.previous_artifact_ref) {
      return { restored: false, detail: 'no previous certkey' };
    }
    await ensureOk(await fetch(`${this.baseUrl(ctx)}/config/sslvserver_sslcertkey_binding/${ctx.config.settings.vserver}`, {
      method: 'PUT',
      headers: await this.headers(ctx),
      body: JSON.stringify({ sslvserver_sslcertkey_binding: { certkeyname: ctx.previous_artifact_ref } })
    }), 'citrix rollback');
    return { restored: true };
  }
}
