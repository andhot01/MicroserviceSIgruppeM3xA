# Analysis and Design
This document describes the analysis and design of the Notifcation Service part of the Bizcrd system.

## Domains
The system is divided into separate domains based on the responsiilities identified during the initial analysis. The notification service belings to the notification domain.

## Microservice responsibilities
The notification service is responsible for notifications belonging to users. Its responsibilities include: creating notifications, retrieving notifications, and marking notifications as read.

## Service boundary
The Notification service owns notification-specific data and business logic. Communication with the service takes place through its REST API or through shared messaging contracts. The service may reference information owned by another microservice through identifiers. Example: RecipientId identifies the recipient of a notification, but the Notification service doesn't own the user.
Also, when the service receives information about a message, it doesn't become responsible for storing or managing that message. It only uses the necessary information to determine whether a notification should be created or not.

## Data ownership
Notification service owns all notification data, such as:
- notification id
- recipient id reference
- notification content
- notification type
- notification status
- creation time
- read time

## Interactions
The Notification service communicates with other microservices mainly through asynchronous messaging using RabbitMQ. It subscribes to MessagePostedEvent and checks the message content for user mentions. If a mention is found, the service creates a notification for the mentioned user and publishes a MentionDetectedEvent.

The service also exposes a REST API, which allows clients to create, retrieve, mark as read, and delete notifications.