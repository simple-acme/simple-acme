import { log } from './logger.js';

export interface RetryOptions {
  maxAttempts?: number;
  backoffMs?: number;
  label: string;
}

/** Executes an async function with exponential backoff and jitter. */
export async function withRetry<T>(fn: () => Promise<T>, opts: RetryOptions): Promise<T> {
  const maxAttempts = opts.maxAttempts ?? 3;
  const backoffMs = opts.backoffMs ?? 1000;

  let attempt = 0;
  let lastError: Error | undefined;

  while (attempt < maxAttempts) {
    attempt += 1;
    try {
      return await fn();
    } catch (error) {
      lastError = error instanceof Error ? error : new Error(String(error));
      log('warn', `${opts.label} failed on attempt ${attempt}/${maxAttempts}: ${lastError.message}`);
      if (attempt >= maxAttempts) {
        break;
      }
      const base = backoffMs * (2 ** (attempt - 1));
      const jitterFactor = 0.8 + (Math.random() * 0.4);
      await new Promise<void>((resolve) => setTimeout(resolve, Math.floor(base * jitterFactor)));
    }
  }

  throw lastError ?? new Error(`${opts.label} failed`);
}
