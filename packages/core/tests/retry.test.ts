import { describe, expect, it } from 'vitest';
import { withRetry } from '../src/retry.js';

describe('withRetry', () => {
  it('retries and succeeds', async () => {
    let count = 0;
    const result = await withRetry(async () => {
      count += 1;
      if (count < 2) {
        throw new Error('first failure');
      }
      return 'ok';
    }, { label: 'retry-test', maxAttempts: 3, backoffMs: 1 });

    expect(result).toBe('ok');
    expect(count).toBe(2);
  });

  it('throws after final attempt', async () => {
    await expect(withRetry(async () => {
      throw new Error('always fails');
    }, { label: 'retry-fail', maxAttempts: 2, backoffMs: 1 })).rejects.toThrow('always fails');
  });
});
