# ADR 0002: Asynchronous Notification Delivery Architecture

## Status
Accepted

## Context
The API needs to send real-time push notifications to users when certain events occur (e.g., transaction completed, badge earned). Sending push notifications involves interacting with external APIs (Firebase Cloud Messaging - FCM), which introduces network latency, potential downtime, and failure modes. If notifications were sent synchronously during the main HTTP request lifecycle (e.g., inside a transaction endpoint handler), it would significantly degrade response times, couple the API availability to external dependencies, and risk transaction failures or data inconsistency.

Furthermore, device FCM tokens are considered Personally Identifiable Information (PII) and must be securely stored in the database.

## Decision
We will implement an out-of-process, decoupled, and asynchronous notification delivery system based on the following architecture:
1. **Transactional Outbox (Database-backed Queue):** Notification payloads are persisted in the database (`user_notifications`) in the same database transaction as the business operation. This ensures "at-least-once" delivery guarantee and transactional integrity.
2. **PostgreSQL LISTEN/NOTIFY for Low-Latency Signaling:** We will leverage PostgreSQL's native `LISTEN/NOTIFY` system to avoid polling the database. A database trigger on insert notifies the `new_notification` channel with a payload containing the notification ID and type.
3. **Managed Background Worker:** An ASP.NET Core `BackgroundService` (`PostgresNotificationListener`) will maintain a persistent connection to PostgreSQL, listen on the channel, and process notifications asynchronously.
4. **FCM Token Security:** Firebase Cloud Messaging (FCM) tokens will be encrypted at rest and only decrypted in-memory by the background worker during dispatch.
5. **Robustness & Catch-up:** The background worker will implement a catch-up phase on startup, scanning for unsent notifications created in the last 7 days, to ensure delivery of pending notifications during worker downtime.
6. **Delivery Concurrency Control:** Concurrency is managed inside the listener using a `SemaphoreSlim` (with a default limit of 10 parallel tasks) to prevent resource exhaustion and rate-limiting from Firebase.

## Consequences
- **Positive:**
  - **High Performance:** API endpoints return immediately without waiting for Firebase API calls.
  - **Reliability:** Notifications are never lost due to worker downtime or network issues; they are safely stored in Postgres.
  - **Security:** FCM tokens are securely encrypted at rest.
  - **Low Overhead:** Postgres `LISTEN/NOTIFY` consumes very few resources compared to database polling.
- **Negative:**
  - **Eventual Consistency:** There is a slight delay (typically sub-second) between the API transaction and the push notification delivery.
  - **Complexity:** Requires managing a background worker service and a persistent database connection specifically for listening.
- **Constraint / Rule:**
  - Database operations that emit notifications must save the notification record to the database in the same transaction.
  - The database connection used by `PostgresNotificationListener` must be dedicated, as `LISTEN/NOTIFY` requires a persistent, non-pooled connection to receive asynchronous events.
