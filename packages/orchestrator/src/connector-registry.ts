import type { Connector } from '@orchestrator/core';
import { UnknownConnectorError } from '@orchestrator/core';

/** Runtime connector registry. */
export class ConnectorRegistry {
  private readonly registry = new Map<string, Connector>();

  /** Registers a connector implementation by type. */
  public register(type: string, connector: Connector): void {
    this.registry.set(type, connector);
  }

  /** Resolves a connector by type or throws. */
  public resolve(type: string): Connector {
    const connector = this.registry.get(type);
    if (!connector) {
      throw new UnknownConnectorError(type);
    }
    return connector;
  }
}
