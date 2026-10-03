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

The current implementation uses EasyNetQ and RabbitMQ. The abstraction allows the underlying message broker to be replaced without exposing EasyNetQ or RabbitMQ-specific details to the rest of the application.# MicroserviceSIgruppeM3xA

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