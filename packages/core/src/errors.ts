export class BaseError extends Error {
  public readonly code: string;

  public constructor(code: string, message: string) {
    super(message);
    this.code = code;
    this.name = this.constructor.name;
  }
}

export class PolicyNotFoundError extends BaseError {
  public constructor(policyId: string) {
    super('POLICY_NOT_FOUND', `Policy not found: ${policyId}`);
  }
}

export class UnknownConnectorError extends BaseError {
  public constructor(type: string) {
    super('UNKNOWN_CONNECTOR', `Unknown connector type: ${type}`);
  }
}

export class RollbackFailedError extends BaseError {
  public constructor(message: string) {
    super('ROLLBACK_FAILED', message);
  }
}

export class ConfigValidationError extends BaseError {
  public constructor(message: string) {
    super('CONFIG_VALIDATION_ERROR', message);
  }
}
