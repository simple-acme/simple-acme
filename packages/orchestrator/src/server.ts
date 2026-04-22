import Fastify from 'fastify';
import { PolicyNotFoundError, StateStore, log, type CertificateEvent } from '@orchestrator/core';
import { ConnectorRegistry } from './connector-registry.js';
import { runFanout } from './fanout-runner.js';
import { resolvePolicy } from './policy-resolver.js';

const certificateEventSchema = {
  type: 'object',
  required: [
    'event', 'renewal_id', 'deployment_policy_id', 'domain', 'cert_path', 'key_path', 'fullchain_path', 'thumbprint', 'issuer', 'not_before', 'not_after'
  ],
  properties: {
    event: { type: 'string', enum: ['certificate.issued', 'certificate.renewed', 'certificate.failed'] },
    renewal_id: { type: 'string' },
    deployment_policy_id: { type: 'string' },
    domain: { type: 'string' },
    cert_path: { type: 'string' },
    key_path: { type: 'string' },
    fullchain_path: { type: 'string' },
    thumbprint: { type: 'string' },
    issuer: { type: 'string' },
    not_before: { type: 'string' },
    not_after: { type: 'string' }
  }
} as const;

/** Creates the Fastify server. */
export function buildServer(store: StateStore, registry: ConnectorRegistry) {
  const app = Fastify();

  app.post<{ Body: CertificateEvent }>('/events', { schema: { body: certificateEventSchema } }, async (request, reply) => {
    const event = request.body;
    reply.code(202).send({ accepted: true, renewal_id: event.renewal_id });
    void (async () => {
      try {
        const policy = await resolvePolicy(event.deployment_policy_id);
        await runFanout(event, policy, registry, store);
      } catch (error) {
        log('error', `background event execution failed: ${String(error)}`);
      }
    })();
  });

  app.get('/jobs/:renewal_id', async (request, reply) => {
    const renewalId = (request.params as { renewal_id: string }).renewal_id;
    reply.send(store.getJobsByRenewal(renewalId));
  });

  app.get('/jobs/status/:job_id', async (request, reply) => {
    const jobId = (request.params as { job_id: string }).job_id;
    const job = store.getJob(jobId);
    if (!job) {
      reply.code(404).send({ error: 'job not found' });
      return;
    }
    reply.send(job);
  });

  app.get('/health', async () => ({ status: 'ok' }));

  app.setErrorHandler((error, _request, reply) => {
    if (error.validation) {
      reply.code(400).send({ error: error.message });
      return;
    }
    if (error instanceof PolicyNotFoundError) {
      reply.code(404).send({ error: 'policy not found', policy_id: error.message.replace('Policy not found: ', '') });
      return;
    }
    log('error', error.stack ?? error.message);
    reply.code(500).send({ error: 'internal error' });
  });

  return app;
}
