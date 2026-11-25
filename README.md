# Notification Microservice

The Notification Microservice is responsible for creating and managing notifications for users in the Bizcord system.

The service is implemented as an ASP.NET Core Web API and follows a layered architecture to separate domain logic, application logic, infrastructure, and presentation.

## Domain Model

The main entity is `Notification`.

A notification contains:

- A unique ID
- A recipient ID
- Notification content (title and message)
- A notification type
- A status (`Unread` or `Read`)
- Creation timestamp
- Optional read timestamp

Notification content is treated as immutable. Once a notification has been created, its content is not changed. The notification can instead move from `Unread` to `Read`.

## REST API

The service currently provides the following endpoints:

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/notifications` | Create a notification |
| GET | `/api/notifications` | Get all notifications |
| GET | `/api/notifications/{id}` | Get a notification by ID |
| PUT | `/api/notifications/{id}` | Mark a notification as read |
| DELETE | `/api/notifications/{id}` | Delete a notification |

## Architecture

The service is separated into the following areas:

- **Domain** – Entities, value objects, and domain behaviour.
- **Application** – Application services and repository abstractions.
- **Infrastructure** – Repository implementations.
- **Controllers** – REST API endpoints and HTTP handling.
- **Shared** – Data contracts exposed to other services.
- **Messaging** – Message broker abstraction using EasyNetQ.

## Persistence

Notifications are currently stored in memory using `NotificationRepository`.

This is intended as an initial implementation and can later be replaced with persistent storage without changing the application layer, as it depends on `INotificationRepository`.

## Messaging

Messaging is abstracted through `IMessageClient`.

The current implementation uses EasyNetQ and RabbitMQ. The abstraction allows the underlying message broker to be replaced without exposing EasyNetQ or RabbitMQ-specific details to the rest of the application.

## Docker and RabbitMQ

This week the Notification Service was containerized using Docker and Docker Compose.

### Dockerfile

A multi-stage Dockerfile was added for the Notification Service using .NET 10.

The Dockerfile:
- Restores the project dependencies
- Builds the application in Release mode
- Publishes the application
- Creates a smaller final image containing only the ASP.NET Core runtime and published application

The Docker image can be built with:

docker build -t notification-service .

## Message event handling
The Notification Service now reacts to messages posted elsewhere in the Bizcord system using RabbitMQ.

### Shared contracts
A MessagePostedEvent was added to Shared.Contracts. It contains the message ID, channel ID, author ID, message content, and timestamp.

Using a shared contract allows the Messaging Service to publish an event without depending directly on the Notification Service.

The Notification Service also publishes a MentionDetectedEvent when a valid user mention is found. The event contains the original MessageId for traceability and the ID of the mentioned user.

### Message handling
MessagePostedSubscription runs as a background service and subscribes to MessagePostedEvent.

Incoming events are passed to MessagePostedHandler, which checks the message content for a mention in the form @{userId}.

When a valid mention is found: A notification is created for the mentioned user using the existing NotificationService.
A MentionDetectedEvent is published through IMessageClient.
And the original MessageId is included in the new event so the message can be traced through the system.

If the message does not contain a valid mention, no notification or result event is created.

### Testing
Three levels of testing were added for the message handling functionality.


## Reliability measures
We identified failure points in our project and decided to implement a solution to of them: Duplicate message processing (more on that in the failure-point.md)

### Our solution - idempotency
- A HashSetGuid tracks successfully processed message IDs
- A SemaphoreSlim prevents concurrent processing from bypassing the duplicate check
- Processed messages are ignored
- Message IDs are marked as processed after successful publishing

### Testing
A unit test was added to simulate receiving the same MessagePostedEvent twice. The test verifies that only one notif is created and that only one MentionDetectedEvent is published.
The test passed.


## East/West security and Vault

This week the Notification Service was protected against unauthorised calls from other services, and its secret was moved out of the source code and into HashiCorp Vault. The full design, run instructions and test results are in docs/east-west-security-and-vault.md.

### East/West security

- The service validates JWT Bearer tokens itself (issuer, audience, lifetime and signature), so it is protected even if the API gateway is bypassed.
- `POST /api/notifications` is an internal endpoint. It requires the role `internal-service`, because notifications are created by other microservices and not by users.
- The gateway exposes this endpoint as `POST /internal/notifications` and checks the same role with `RouteClaimsRequirement`. The previous public `POST /notifications` route was removed.
- The service has no published port. It is only reachable through the gateway over the internal Docker network `bizcord-internal`.

| Method | Endpoint | Description | Access |
| ------ | -------- | ----------- | ------ |
| POST | `/api/notifications` | Create a notification | `internal-service` |
| GET | `/api/notifications` | Get all notifications | valid token (`member` via gateway) |
| GET | `/api/notifications/{id}` | Get a notification by ID | valid token (`member` via gateway) |
| PUT | `/api/notifications/{id}` | Mark a notification as read | valid token (`admin` via gateway) |
| DELETE | `/api/notifications/{id}` | Delete a notification | valid token (`admin` via gateway) |

### Vault

- The JWT signing key is stored in Vault at `secret/notification` (KV v2, key `SigningKey`).
- At startup `VaultSecretReader` reads the key with a scoped application token. The token has a read-only policy for that single path.
- If Vault is sealed or unreachable, or the token is invalid, the service refuses to start.
- `VAULT_ADDR` and `VAULT_TOKEN` are supplied at runtime. No secrets are committed to the repository.

### Running with Vault

```bash
docker network create bizcord-internal

cd vault
docker compose up -d          # then initialise and unseal Vault, see the docs file

cd ..
read -s VAULT_TOKEN          # paste the application token
export VAULT_TOKEN
docker compose up -d --build
```

### Testing

Ten east/west tests and six Vault tests were run, and all gave the expected result. In short:

- Without a token the service returns 401. A `member` token returns 403. An `internal-service` token returns 201.
- A token with an altered signature is rejected with 401.
- After the port mapping was removed, a direct call to port 8000 is refused, while the same request through the gateway returns 201.
- Vault returns 403 for an invalid token, for another path and for an attempt to overwrite the secret. A sealed Vault returns 503 and the service does not start. After unsealing, the secret is still there.

The complete test tables are in the docs file.


## Saga Pattern
Implemented an orchestration-based Saga pattern for the scenario where a workspace is deleted by its last admin.
The Notif service acts as the saga orchestrator, coordinating transactions and compensating transactions across multiple microservices thorugh RabitMQ and the exisitng MessageClient.