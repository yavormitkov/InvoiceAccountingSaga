# Invoice Accounting Saga (CUB Connect)

An event-driven distributed accounting Proof of Concept (PoC) built on **.NET 10**, **SQL Server 2022**, **RabbitMQ**, and **YARP**. Engineered to ingest high-frequency automated invoice payloads from M2M banking RPA bots, enforce strict idempotency at both ingestion and consumer boundaries, and propagate ledger updates reliably across isolated bounded contexts.

---

## Core Engineering Principles

* **Database-per-Service Isolation:** Separate physical persistence boundaries for operational invoicing (`CubConnectDb`) and financial accounting (`FinancialDb`), eliminating shared-database coupling.
* **Choreography Over Orchestration:** Services communicate asynchronously via domain events (`InvoiceRegisteredEvent` over MassTransit/RabbitMQ) with zero central coordinator bottleneck.
* **Single-Round-Trip Ingestion & Transactional Outbox:** Write-heavy ingestion bypasses ORM change-tracking overhead in favor of raw **Dapper** and atomic T-SQL scripts (`SET XACT_ABORT ON`), resolving natural keys (`BailiffLicenseNumber`), enforcing idempotency, and persisting outbox records in one atomic transaction.
* **Strict UTC Chronology:** All persistence timestamps execute via `SYSUTCDATETIME()` to eliminate local timezone and DST drift.

---

## End-to-End Choreography Lifecycle

1. **Edge Perimeter (`Gateway.Bff`):** YARP reverse proxy serves as the single ingress (`:5000`), routing `/api/invoices/*` and `/api/financial/*` to internal microservice containers while serving the containerized **Blazor WebAssembly** SPA (`Portal.BlazorUi` on `nginx:alpine`) on `/*` under a unified Same-Origin boundary.
2. **Idempotent Ingestion & Outbox Persistence (`Invoicing.Api`):** `RegisterInvoiceCommandHandler` executes a single atomic T-SQL script (`RegisterInvoiceSql`) wrapped in a **Polly** exponential retry pipeline. It resolves `BailiffLicenseNumber -> BailiffId`, acquires a range lock (`WITH (UPDLOCK, HOLDLOCK)`) on the composite unique index `(InvoiceNumber, BailiffId)`, and inserts into both `dbo.Invoices` and `dbo.OutboxEvents` in one transaction. Duplicate bot retries return HTTP `200 OK` (`AlreadyRegistered`) without emitting duplicate outbox records.
3. **Outbox Relay (`OutboxRelayWorker`):** An adaptive `PeriodicTimer` background worker polls `dbo.OutboxEvents` using `WITH (UPDLOCK, READPAST, ROWLOCK)`, publishes `InvoiceRegisteredEvent` payloads to RabbitMQ via MassTransit `IPublishEndpoint`, and stamps `ProcessedAt = SYSUTCDATETIME()`.
4. **Idempotent Ledger Settlement (`Financial.Api`):** `InvoiceRegisteredConsumer` consumes `InvoiceRegisteredEvent` (protected by MassTransit exponential `UseMessageRetry` and Polly retry policies) and dispatches `ProcessInvoiceAccountingCommand`. Inside one atomic transaction, `dbo.ProcessedInvoices` deduplicates `at-least-once` redeliveries under `WITH (UPDLOCK, HOLDLOCK)` before updating `dbo.UserBalances`. Unrecoverable failures route to RabbitMQ's `InvoiceRegistered_error` dead-letter queue.
5. **Audit & Monitoring (`Portal.BlazorUi`):** Queries aggregated balances (`GET /api/financial/bailiffs` via EF Core `AsNoTracking` projection) and per-bailiff invoice drill-downs (`GET /api/invoices/bailiff/{licenseNumber}`).

---

## System Topology & Dataflow

```text
[RPA Banking Bots] ---> POST /api/invoices (Bearer RS256 JWT)
                              |
[Auditor Browser]  ---> GET / & /api/financial/*
                              |
                              v
                  +-----------------------+

                  |     Gateway.Bff       |  (YARP Reverse Proxy :5000)
                  | Sliding Window Limit  |  (300 req / 10s -> HTTP 429)
                  +-----------------------+

                     |        |        |
        +------------+        |        +------------+
        v                     v                     v
+---------------+   +-------------------+   +---------------+

| cub_portal_ui |   | cub_invoicing_api |   |cub_financial_ |
| (Blazor WASM  |   | (Dapper + Polly + |   |     api       |
|  nginx:alpine)|   | OutboxRelayWorker)|   |(Dapper/EF Core|
+---------------+   +-------------------+   +---------------+
                              |                     ^
                   1-Round-Trip T-SQL               |
                   (Invoices + Outbox)       InvoiceRegisteredEvent

                              |                     |
                              v                     |
                    +-------------------+   +---------------+

                    |   CubConnectDb    |   |  cub_rabbitmq |
                    | (SQL Server 2022) |   | (MassTransit) |
                    +-------------------+   +---------------+
                                                    |
                                                    v
                                            +---------------+

                                            |  FinancialDb  |
                                            |(ProcessedInvs |
                                            | +UserBalances)|
                                            +---------------+
```

---

## Concurrency, Idempotency & Resilience Mechanics

* **Phantom Read Prevention (`UPDLOCK, HOLDLOCK`):** Under default `READ COMMITTED` isolation, row-level locks (`XLOCK, ROWLOCK`) do not lock non-existent keys, allowing concurrent duplicate `INSERT`s to collide on unique constraints. `RegisterInvoiceSql` applies `UPDLOCK, HOLDLOCK` range locks on `IX_Invoices_InvoiceNumber_BailiffId` alongside a `2601/2627` unique-violation catch handler to guarantee deterministic idempotency under parallel bot bursts.
* **Consumer At-Least-Once Deduplication (`dbo.ProcessedInvoices`):** `ProcessInvoiceAccountingCommandHandler` checks and records `InvoiceId` under `WITH (UPDLOCK, HOLDLOCK)` inside the same transaction that increments `dbo.UserBalances`, eliminating double-counting on RabbitMQ message redeliveries.
* **Defense-in-Depth Sliding Window Rate Limiting:** Configurable sliding-window limiters (`RateLimitingOptions`, `SegmentsPerWindow = 10`) enforce perimeter throttling at `Gateway.Bff` (`300 req / 10s`) and connection-pool protection at `Invoicing.Api` (`100 req / 1s`), preventing fixed-window boundary bursts and rejecting excess traffic with `HTTP 429 Too Many Requests`.
* **Deterministic Container Boot Sequencing:** `cub_sql_database` executes an active readiness loop (`sqlcmd -Q "SELECT 1"`) before running idempotent schema scripts with fail-fast error handling (`sqlcmd -b -i ...`) and exposing a `/tmp/db_initialized` healthcheck sentinel. Dependent APIs wait on `condition: service_healthy`.

---

## Security Controls

* **Asymmetric JWT Verification (RS256):** `Invoicing.Api` validates RSA public keys locally in memory (`AuthenticationOptions`, configurable `ClockSkewMinutes`) without runtime IdP round-trips, enforcing the `scope: invoices:create` authorization policy on `POST /api/invoices`.
* **Natural Key Exposure:** Surrogate `UNIQUEIDENTIFIER` primary keys remain internal; external integrations and UI drill-downs operate strictly via bailiff license numbers (`B-001`, `B-002`).
* **Strict Exception Boundaries:** Global exception middleware classifies malformed JSON payloads (`BadHttpRequestException` / `JsonException`) as `HTTP 400 Bad Request` while isolating database or broker faults as `HTTP 500 Internal Server Error`.

---

## Integration & Concurrency Verification (`dotnet test`)

* **`InvoiceIdempotencyTests`:**
  * Synchronizes concurrent `POST /api/invoices` collisions (`Task.WhenAll` + `TaskCompletionSource` gate) for identical payloads, verifying one `201 Created`, one `200 OK (AlreadyRegistered)`, identical `InvoiceId`, and a single row in `dbo.OutboxEvents`.
  * Validates real RS256 JWT enforcement (`401 Unauthorized` on untrusted signing keys, `403 Forbidden` on missing `invoices:create` scope).
* **`FinancialConcurrencyTests`:** Dispatches parallel accounting mutations and simulates duplicate `at-least-once` message redelivery for an identical `InvoiceId`, verifying exact balance aggregation and zero duplicate increments via `dbo.ProcessedInvoices`.

---

## Local Execution & Verification

### 1. Generate Ephemeral RSA-2048 Key Pair
Private keys are never committed to source control. Generate a local key pair using OpenSSL:

```bash
openssl genpkey -algorithm RSA -out private.pem -pkeyopt rsa_keygen_bits:2048
openssl rsa -pubout -in private.pem -out public.pem
```

### 2. Configure Environment & Boot Stack
Copy `.env.example` to `.env`, configure `Authentication__PublicKey` in `docker-compose.override.yml` (single-line Base64 PEM body from `public.pem`), and start the topology:

```bash
cp .env.example .env
docker-compose up --build -d
```

* **Portal UI & Gateway:** `http://localhost:5000`
* **RabbitMQ Management:** `http://localhost:15672` (`guest` / `guest`)
* **SQL Server 2022 (`://microsoft.com`):** `localhost,1433` (`sa` / `$MSSQL_SA_PASSWORD`)

### 3. Submit Authenticated Invoice Payload
Sign a JWT with `private.pem` containing claims `"iss": "http://localhost:5000"`, `"aud": "Invoicing.Api"`, `"scope": "invoices:create"`, and `"kid": "cub-key-2026"` in the header, then dispatch via `Gateway.Bff`:

```bash
curl -X POST http://localhost:5000/api/invoices \
  -H "Authorization: Bearer <RS256_SIGNED_JWT>" \
  -H "Content-Type: application/json" \
  -d '{
    "invoiceNumber": "INV-2026-001",
    "bailiffLicenseNumber": "B-001",
    "amount": 250.00
  }'
```

---

## Key Production Readiness Trade-Offs

* **Schema Provisioning:** Database schemas initialize via idempotent T-SQL scripts mounted at container startup; production deployments require versioned migration tooling (**DbUp** or **Flyway**).
• Auditor Portal Read Authentication (DemoModeEnabled Trade-Off): While write mutations (POST /api/invoices) enforce strict asymmetric RS256 JWT validation and scope: invoices:create authorization at all times, read endpoints powering the auditor dashboard (GET /api/financial/bailiffs controlled via Authentication:DemoModeEnabled=true, and GET /api/invoices/bailiff/{license}) allow anonymous access so the Blazor SPA can be inspected without a live Identity Provider. Production deployments require setting DemoModeEnabled=false and enforcing an OIDC Authorization Code + PKCE flow in Gateway.Bff using encrypted HttpOnly; SameSite=Strict session cookies.
• UI Data Access Coupling: For PoC simplicity, Portal.BlazorUi invokes HttpClient directly inside HomeBase; production codebases should extract HTTP transport and error mapping into a dedicated typed HttpClient service (IBailiffMonitoringClient) to decouple presentation state from API routing.
