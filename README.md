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