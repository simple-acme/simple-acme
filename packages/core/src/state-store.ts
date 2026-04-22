import Database from 'better-sqlite3';
import type { ConnectorJobRecord, ConnectorStep, JobStatus } from './types.js';

/** SQLite-backed connector job store. */
export class StateStore {
  private readonly db: Database.Database;

  public constructor(dbPath: string) {
    this.db = new Database(dbPath);
    this.db.prepare(`
      CREATE TABLE IF NOT EXISTS connector_jobs (
        job_id TEXT PRIMARY KEY,
        renewal_id TEXT NOT NULL,
        deployment_policy_id TEXT NOT NULL,
        connector_type TEXT NOT NULL,
        step TEXT NOT NULL,
        status TEXT NOT NULL,
        artifact_ref TEXT,
        previous_artifact_ref TEXT,
        attempt INTEGER NOT NULL,
        error_detail TEXT,
        created_at TEXT NOT NULL,
        updated_at TEXT NOT NULL
      )
    `).run();
  }

  /** Creates a connector job row with generated timestamps. */
  public createJob(record: Omit<ConnectorJobRecord, 'created_at' | 'updated_at'>): void {
    const now = new Date().toISOString();
    this.db.prepare(`
      INSERT INTO connector_jobs (
        job_id, renewal_id, deployment_policy_id, connector_type, step,
        status, artifact_ref, previous_artifact_ref, attempt, error_detail, created_at, updated_at
      ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    `).run(
      record.job_id,
      record.renewal_id,
      record.deployment_policy_id,
      record.connector_type,
      record.step,
      record.status,
      record.artifact_ref,
      record.previous_artifact_ref,
      record.attempt,
      record.error_detail,
      now,
      now
    );
  }

  /** Updates a job's step/status and optional fields. */
  public updateStep(
    jobId: string,
    step: ConnectorStep,
    status: JobStatus,
    updates?: Partial<Pick<ConnectorJobRecord, 'artifact_ref' | 'previous_artifact_ref' | 'attempt' | 'error_detail'>>
  ): void {
    const current = this.getJob(jobId);
    if (!current) {
      return;
    }

    this.db.prepare(`
      UPDATE connector_jobs
      SET step = ?, status = ?, artifact_ref = ?, previous_artifact_ref = ?, attempt = ?, error_detail = ?, updated_at = ?
      WHERE job_id = ?
    `).run(
      step,
      status,
      updates?.artifact_ref ?? current.artifact_ref,
      updates?.previous_artifact_ref ?? current.previous_artifact_ref,
      updates?.attempt ?? current.attempt,
      updates?.error_detail ?? current.error_detail,
      new Date().toISOString(),
      jobId
    );
  }

  /** Fetches a job by id. */
  public getJob(jobId: string): ConnectorJobRecord | undefined {
    return this.db.prepare('SELECT * FROM connector_jobs WHERE job_id = ?').get(jobId) as ConnectorJobRecord | undefined;
  }

  /** Fetches jobs by renewal id. */
  public getJobsByRenewal(renewalId: string): ConnectorJobRecord[] {
    return this.db.prepare('SELECT * FROM connector_jobs WHERE renewal_id = ? ORDER BY created_at ASC').all(renewalId) as ConnectorJobRecord[];
  }

  /** Returns pending jobs only. */
  public getPendingJobs(): ConnectorJobRecord[] {
    return this.db.prepare("SELECT * FROM connector_jobs WHERE status = 'pending' ORDER BY created_at ASC").all() as ConnectorJobRecord[];
  }
}
