# Potential failure points - notif service

## 1 RabbitMQ unavailability
The notif service depends on RabbitMQ for receiving MessagePostedEvent and publishing MentionDetectedEvent. If Rabbit becomes unavailable, communication may fail, meaning delayed or missing notifications.

## 2 Network failures
Network interruption between notif service and Rabbit could cause publishing or subscribing to fail. No explicit retry or timeout policies have been implemented.

## 3 In-Memory data storage
Notifs are currently stored in a in-memory list. If the service crashes or restarts, all stored notifs will be lost.

## 4 Duplicate message processing
The MessagePostedHandler doesn't check whether a message has already been processed. If the same event is recieved multiple times, duplicates may appear.

## 5 Partial message processing
The Handler^^ creates a notif, then publishes a MentionDetectedEvent. If publishing fails, the notif has already been created, which is a potential for inconsistencies. 
