# ADR 0003: Server-Sent Events (SSE) for Real-Time Updates

## Status
Accepted

## Context
Some client applications (e.g., a companion/dashboard app) require immediate, real-time notification when a server-side event completes — for example, when a user finishes paying against a server-issued, single-use order. Real-time updates provide instant feedback where polling would be too slow or wasteful.

To achieve this, the API needs a mechanism to push events from the server to client applications. We considered several approaches:
1. **HTTP Polling**: Simple to implement but introduces high database read load, increased network overhead, and latency.
2. **SignalR / WebSockets**: Supports bidirectional communication, but introduces significant complexity, requires custom client libraries/SDKs, and is heavyweight for a simple unidirectional (server-to-client) notification use case.
3. **Server-Sent Events (SSE)**: A lightweight, standardized HTTP-based protocol (`text/event-stream`) natively supported by modern HTTP clients. It provides unidirectional streaming over a single long-lived TCP connection, which perfectly fits this requirements profile.

However, implementing SSE in our environment introduces specific architectural challenges:
* **Application-Layer Encryption (ALE)**: Our API uses ALE to encrypt payloads. Buffering the entire response to encrypt it before transmission is incompatible with real-time streaming over SSE.
* **Asymmetric Request Signing**: The API enforces ECDSA request signing on incoming requests. Standard `EventSource` clients cannot easily attach the custom cryptographic headers required to establish the SSE stream.
* **Personally Identifiable Information (PII) Protection**: User names are stored using envelope encryption. A real-time event that must display them requires decryption on the server before broadcasting.
* **Distributed/Multi-Instance Scale**: The API may run in a multi-instance/containerized environment. An event processed on Instance A must be broadcast to clients connected to Instance B.

## Decision
We will implement real-time updates using Server-Sent Events (SSE) with the following design decisions:

1. **Lightweight Unidirectional Stream**: We will expose a `GET /v1/projects/{projectId}/events` endpoint returning `text/event-stream`.
2. **Selective Security Bypass**: 
   - **ALE Bypass**: We will decorate the endpoint with `[AllowPlaintext]` to disable payload buffering and encryption, allowing immediate streaming of events.
   - **Request Signing Bypass**: We will decorate the endpoint with `[SkipRequestSigning]` to ensure standard HTTP `EventSource` clients can initiate the stream without complex signing headers.
   - **Standard Transport Security**: Connection security will remain enforced via TLS/SSL, and authentication will be managed using standard JWT Bearer Tokens.
3. **Automated IDOR Protection**: The endpoint route will explicitly require `{projectId}`, which invokes the `ProjectResourceHandler` to verify that the authenticated client has permission (`ProjectRead`) to access that project's data.
4. **PII Decryption at the Source**: The MediatR handler will retrieve and decrypt any PII fields in-memory using the owning row's Data Encryption Key (DEK) before serializing the payload.
5. **PostgreSQL LISTEN/NOTIFY for Multi-Instance Signaling**:
   - The MediatR handler processes the completed-event and publishes the event payload to PostgreSQL using `SELECT pg_notify('payment_completed', payload)`.
   - A managed background worker (`PostgresPaymentUpdateListener`) runs on each API instance, maintaining a dedicated, non-pooled connection to listen to the `payment_completed` channel.
   - Upon receiving a database signal, the listener deserializes the payload and publishes it to the local, singleton `ProjectEventsBroadcaster`.
6. **In-Memory Connection Registry**:
   - The `ProjectEventsBroadcaster` will maintain active connection channels mapped by `ProjectId`.
   - Incoming SSE requests will subscribe to an asynchronous enumerable from this broadcaster.
   - The stream will support an optional correlation query parameter (e.g., `orderId`) to auto-terminate the connection once that specific event is delivered, preventing clients from keeping streams open indefinitely.
7. **Connection Lifecycle & Keep-Alives**:
   - The server will send a `: keep-alive` comment block every 15 seconds to prevent intermediate proxies (e.g., Nginx, ALB) or load balancers from closing idle connections.
   - Clients must explicitly close the connection when navigating away from the waiting screen.

## Consequences
- **Positive**:
  - **Standardized & Lightweight**: SSE is built on standard HTTP/1.1 and HTTP/2, requiring no custom WebSockets-like protocols or SDKs.
  - **Multi-Instance Capability**: Scales horizontally without requiring an external message broker (like Redis or RabbitMQ) by leveraging the existing PostgreSQL engine.
  - **Low Latency**: End-to-end event propagation from processing to the connected client is sub-second.
  - **Secure PII**: PII is decrypted safely in memory on the server side and never stored in a decrypted form or exposed in transit over unsecure connections.
- **Negative**:
  - **Bypassed Security Layers**: Payload encryption (ALE) and request signing are disabled for this route, placing full reliance on TLS/SSL and JWT validation.
  - **Stateful Connection Management**: Requires keeping open HTTP connections on the API hosts, requiring careful resource planning (e.g., configuring socket limits, disabling proxy buffering via `X-Accel-Buffering: no`).
- **Constraint / Rule**:
  - Endpoints exposing SSE streams must always implement ownership authorization checks to prevent unauthorized data exposure.
  - The database connection used by `PostgresPaymentUpdateListener` must be excluded from the application DbContext pool, as `LISTEN/NOTIFY` requires a dedicated persistent connection.
  - Clients must explicitly close the connection when navigating away to prevent resource leaks on the server.
