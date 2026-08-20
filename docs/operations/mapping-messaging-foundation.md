# Mapping messaging foundation operations

## Local stack

Copy `.env.example` to `.env`, replace every placeholder with a local-only password, then run:

```powershell
docker compose up -d --wait
docker compose ps
```

Configure the API through environment variables or .NET user-secrets. Do not put credentials in tracked `appsettings*.json` files. The relevant keys are:

```text
ConnectionStrings__AgriDrone
RabbitMq__Enabled=true
RabbitMq__HostName=localhost
RabbitMq__VirtualHost=agridrone
RabbitMq__UserName
RabbitMq__Password
Redis__Enabled=true
Redis__ConnectionString=localhost:6379,password=<local-password>,abortConnect=false
```

After starting the API, verify:

```text
GET /health/live   -> process liveness
GET /health/ready  -> PostgreSQL, RabbitMQ and Redis readiness
```

Stop the local stack without deleting named volumes:

```powershell
docker compose down
```

Run the isolated Step 1 dependency suite (fixed localhost test ports,
temporary container storage) and the integration tests with:

```powershell
docker compose -f compose.step1-test.yaml up -d --wait
dotnet test backend/tests/AgriDrone.IntegrationTests/AgriDrone.IntegrationTests.csproj
docker compose -f compose.step1-test.yaml down
```

## Inbox and Outbox retention

`Messaging:Retention` controls cleanup. Cleanup only removes:

- Inbox records in `COMPLETED` or `FAILED` older than the configured retention.
- Outbox records in `PUBLISHED` older than the configured retention.

It never removes Inbox `PROCESSING`, Outbox `PENDING`, `PROCESSING`, `RETRY`, or `DEAD`. Cleanup is batched to avoid long-running delete locks.

## Redrive

Only a user satisfying the `Identity.SystemAdmin` policy can call the recovery endpoints.

```text
POST /api/system/messaging/outbox/{messageId}/redrive
POST /api/system/messaging/dead-letters/{consumerName}/redrive
Body: { "maximumMessages": 10 }
```

Outbox redrive changes only `DEAD` records back to `RETRY`, resets the attempt count and preserves the original `MessageId` and body. RabbitMQ DLQ redrive republishes the original body and `MessageId` to the original routing key and ACKs the DLQ delivery only after publisher confirmation.

Every redrive request is written to the append-only audit log with actor and correlation context. Review the DLQ payload and failure headers before redriving; do not repeatedly redrive a permanent schema or business error.

## Production requirements

The local Compose file is not a production deployment template. Production must provide:

- Separate non-default credentials stored in a secret manager and rotated regularly.
- TLS for PostgreSQL, RabbitMQ and Redis, plus private network isolation and least-privilege firewall rules.
- RabbitMQ users/vhosts scoped to the required exchanges and queues; Redis ACLs scoped to the application key prefix.
- Durable RabbitMQ queues, quorum queues where availability requirements justify them, persistent messages and publisher confirms.
- PostgreSQL, RabbitMQ and Redis persistence sized for expected load, tested backup/restore and disaster recovery procedures.
- Resource requests/limits, disk alarms, connection limits and retention policies.
- Monitoring and alerts for readiness failures, queue/DLQ depth, Inbox failures, Outbox `DEAD`, dispatch lag, retry count and cache failure rate.
- Controlled deployment order for schema migrations and event schema versions. Breaking contract changes require a new event type/routing key version.
