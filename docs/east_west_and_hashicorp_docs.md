
# East/West Security and HashiCorp Vault

This document describes how the Notification Service is protected against unauthorised calls from other services (east/west security, Task 01) and how it retrieves its signing secret from HashiCorp Vault instead of keeping it in source code (Task 02). It covers the design decisions, how to run the setup, and how to verify it. All test results below were produced on 2026-11-23.

## 1. Overview

```
 Client / other service
          |
          v   (only public entry point, 127.0.0.1:8080)
   +--------------+   JWT check + role check (Ocelot)
   | API Gateway  |
   +------+-------+
          |  Docker network: bizcord-internal
          v
 +-----------------------+   JWT check + role check (ASP.NET Core)
 | Notification Service  |-------------------------> RabbitMQ
 +----------+------------+
            |  reads JWT signing key once at startup
            v
      +-----------+
      |   Vault   |  (secret/notification, read-only token)
      +-----------+
```

- The gateway is the only component reachable from the host. The Notification Service has no published port.
- The gateway and the service both validate the JWT. If someone reaches the service without going through the gateway (for example from another container on the internal network), the service rejects the request itself.
- The key used to validate JWTs is not stored in the repository. The service fetches it from Vault when it starts.

## 2. Task 01: East/West security

### 2.1 Which endpoints are restricted?

| Endpoint (via gateway) | Downstream endpoint | Who may call it | Required role |
| ---------------------------------- | -------------------------------- | ---------------------------- | ---------------------- |
| `GET /notifications` | `GET /api/notifications` | Users | `member` |
| `GET /notifications/{id}` | `GET /api/notifications/{id}` | Users | `member` |
| `PUT /notifications/{id}` | `PUT /api/notifications/{id}` | Admins | `admin` |
| `DELETE /notifications/{id}` | `DELETE /api/notifications/{id}` | Admins | `admin` |
| **`POST /internal/notifications`** | `POST /api/notifications` | **Other microservices only** | **`internal-service`** |

Creating a notification is a service-to-service operation: notifications are created as a reaction to events elsewhere in the system (for example a mention in a message). A user should never create a notification directly, so the previous public `POST /notifications` route was removed and replaced by `/internal/notifications`. The `/internal/` prefix makes the intent visible in the URL.

### 2.2 Answers to the three design questions

| Question | Decision | Reason |
| ----------------------------------------------- | ------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| What mechanism authenticates incoming requests? | **JWT Bearer tokens** (HS256) | The gateway already used JWT for north/south security. Tokens are signed, carry claims, expire, and can be validated by the service without calling another service. API keys have no claims or expiry. OAuth would require an identity provider, which is out of scope. |
| What mechanism authorises incoming requests? | **Role claim** in the JWT (`role: internal-service`) | Users have the roles `member` and `admin`. Services get a separate role, so a user token (even an admin token) can never be used on an internal endpoint. |
| How is it implemented? | **Two layers**: Ocelot `RouteClaimsRequirement` in the gateway and `[Authorize(Roles = ...)]` in the service | The gateway is the first line of defence. The service protects itself in case the gateway is bypassed (defence in depth). |

### 2.3 Implementation

**Token content** (issued by the gateway's `/faucet` endpoint, see limitations):

| Claim | Value |
| ------ | ----------------------------------------------- |
| `iss` | `MicroserviceSI` |
| `aud` | `NotificationService` |
| `sub` | caller identity, for example `messaging-service` |
| `role` | `internal-service`, `member` or `admin` |
| `exp` | issue time + 1 hour |

The audience binds a token to this service, so a token issued for a different service cannot be reused here.

**Gateway** (`api-gateway/src/Configuration/ocelot.notification.json`): the route `notification-create-internal` has `AuthenticationProviderKeys: ["Bearer"]` and `RouteClaimsRequirement: { "role": "internal-service" }`. A missing or invalid token returns 401. A valid token with the wrong role returns 403.

**Service** (`src/Program.cs`): JWT Bearer authentication validates issuer, audience, lifetime and signature. Two settings are essential:

- `MapInboundClaims = false` and `RoleClaimType = "role"`: otherwise .NET renames the claims and `[Authorize(Roles = ...)]` does not find the role.
- `ClockSkew = TimeSpan.Zero`: expired tokens are rejected immediately.

**Controller** (`src/Controllers/NotificationController.cs`): `[Authorize]` is applied to the whole controller (any valid token), and `[Authorize(Roles = "internal-service")]` is applied to `Create`.

### 2.4 Network isolation (north/south)

- The external Docker network `bizcord-internal` connects the gateway, the Notification Service, RabbitMQ and Vault.
- The gateway is attached to both `default` and `bizcord-internal`. All other containers are only on `bizcord-internal`.
- The `ports:` mapping was removed from the Notification Service, so it cannot be reached from the host.
- The Ocelot routes point to `notification-service:8080` (the Docker DNS name) instead of `host.docker.internal:8000`.

## 3. Task 02: HashiCorp Vault

### 3.1 What is stored in Vault?

The shared **JWT signing key** used to validate tokens (`Jwt:SigningKey`). It used to be an application setting; now it exists only in Vault.

| Item | Value |
| ----------------- | ------------------------------------------------------------------- |
| Secrets engine | KV v2, mounted at `secret/` |
| Secret path | `secret/notification` (HTTP: `/v1/secret/data/notification`) |
| Key | `SigningKey` |
| Policy | `notification-read`: only `read` on `secret/data/notification` |
| Application token | created with `-policy=notification-read -no-default-policy -ttl=8h` |

The application token can read this one secret and nothing else (least privilege). It cannot write or read other paths. The root token and the unseal keys are only used for administration and are never given to the service.

### 3.2 How the service gets the secret

`src/Infrastructure/VaultSecretReader.cs` calls Vault's HTTP API at startup, **before** authentication is configured in `Program.cs`:

1. Read `VAULT_ADDR` and `VAULT_TOKEN` from the environment.
2. `GET /v1/secret/data/notification` with the header `X-Vault-Token`.
3. Read the value at `data.data.SigningKey` (KV v2 nests the secret under `data.data`).
4. Pass the value to the JWT validation parameters.

The service **fails fast**: if Vault is sealed or unreachable, the token is invalid, or the secret is missing or empty, the service throws an exception and stops. A service without its secret must not run in an insecure state. The secret value is never logged or returned.

`VAULT_TOKEN` is supplied at runtime from the shell (`${VAULT_TOKEN}` in `docker-compose.yml`) and is not committed.

### 3.3 Design decisions

- **Vault HTTP API instead of a client library.** It matches the task example and needs no extra NuGet package. The code is small enough to read in one go.
- **Scoped application token instead of username/password.** The token is limited to one read-only path and has a time-to-live. There are no login credentials in the code.
- **Secret read once at startup.** The token's TTL only matters at startup. A token that expires later does not affect a running service.
- **Vault on the same internal network.** The service uses `http://vault:8200`. The Vault port is published only on `127.0.0.1` so that it is reachable from the developer's machine but not from the network.

## 4. How to run it

Prerequisites: Docker with Compose, and a shell such as zsh or bash. The two repositories (the Notification Service and the API gateway) are cloned next to each other.

### 4.1 One-time setup

```bash
# 1. Create the shared network
docker network create bizcord-internal

# 2. Start Vault (from the vault/ folder)
cd vault
docker compose up -d
```

3. Open [http://localhost:8200](http://localhost:8200) and initialise Vault with **Key shares = 3** and **Key threshold = 2**.
4. Save the **root token** and the **three unseal keys** outside the repository. They are shown only once.
5. Unseal with two different keys, then sign in with the root token.
6. Create the secret, policy and application token:

```bash
docker compose exec vault sh
vault login                      # paste the root token
vault secrets enable -path=secret kv-v2
vault kv put secret/notification SigningKey='<same value as AuthenticationProviderKey in the gateway>'
printf 'path "secret/data/notification" { capabilities = ["read"] }' | vault policy write notification-read -
vault token create -policy=notification-read -no-default-policy -ttl=8h
exit
```

7. Copy the value of `token` from the output (starts with `hvs.`). This is the application token. Do not use `token_accessor`.

The signing key stored in Vault must be identical to the key the gateway uses to sign tokens (`AuthenticationProviderKey` in `appsettings.Development.json`), otherwise the service rejects every token.

### 4.2 Start the services

```bash
# Notification Service (repository root)
read -s VAULT_TOKEN        # paste the application token and press Enter
export VAULT_TOKEN
docker compose up -d --build

# API gateway (api-gateway/ folder)
docker compose up -d --build
```

`VAULT_TOKEN` only exists in the terminal where it was set. Set it again in a new terminal. Rebuild with `--build` after changing code or Ocelot configuration, because both are copied into the image.

After a restart of Vault, unseal it again with two of the three keys before starting the service.

## 5. How to verify

Run the commands in one terminal where `VAULT_TOKEN` is set.

```bash
# Tokens from the gateway's faucet
TOKEN_A=$(curl -s -X POST http://localhost:8080/faucet -H "Content-Type: application/json" -d '{"claims":{"sub":"messaging-service","role":"internal-service"}}' | sed -E 's/.*"value":"([^"]+)".*/\1/')
TOKEN_B=$(curl -s -X POST http://localhost:8080/faucet -H "Content-Type: application/json" -d '{"claims":{"sub":"user-1","role":"member"}}' | sed -E 's/.*"value":"([^"]+)".*/\1/')
BODY='{"recipientId":"550e8400-e29b-41d4-a716-446655440000","title":"Welcome!","message":"You have been added to a channel.","type":1}'

# Example: Test 3 (internal service through the gateway), expected 201
curl -s -w "\nHTTP status: %{http_code}\n" -X POST http://localhost:8080/internal/notifications \
  -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN_A" -d "$BODY"

# Example: V3 (application token reads another path), expected 403
curl -s -o /dev/null -w "HTTP status: %{http_code}\n" -H "X-Vault-Token: $VAULT_TOKEN" \
  http://localhost:8200/v1/secret/data/andet
```

The other tests use the same pattern: change the URL, the `Authorization` header and the expected status as listed in section 6.

## 6. Test results

### 6.1 East/West security

Tests 1 to 8 were run while port 8000 was still published, so that the service could be called directly. Tests 9 and 10 were run after the port was closed (section 2.4).

| # | Request | Token | Expected | Result |
| -- | -------------------------------------------------------------------- | -------------------------------------------- | ------------------ | ------------------------------------------------------------- |
| 1 | `POST localhost:8080/internal/notifications` (gateway) | none | 401 | **401** |
| 2 | same | `member` | 403 | **403** |
| 3 | same | `internal-service` | 201 | **201**, notification returned as JSON |
| 4 | `GET localhost:8080/notifications` (gateway) | `member` | 200 | **200**, list contains the notification from test 3 |
| 5 | `POST localhost:8000/api/notifications` (direct) | none | 401 | **401** |
| 6 | same | `member` | 403 | **403** |
| 7 | same | `internal-service` with an altered signature | 401 | **401** |
| 8 | same | `internal-service` | 201 | **201** |
| 9 | `POST localhost:8080/internal/notifications` after closing port 8000 | `internal-service` | 201 | **201** (gateway reaches the service over `bizcord-internal`) |
| 10 | `POST localhost:8000/api/notifications` after closing port 8000 | `internal-service` | connection refused | **HTTP status 000**, no connection possible |

What the results show:

- Tests 1 and 2: the gateway authenticates (401) and authorises (403).
- Tests 5 to 7: the service enforces authentication and the role itself, and validates the signature, even without the gateway.
- Test 8 is a control: direct access works with the correct token.
- Tests 9 and 10: the service is reachable only through the gateway.

### 6.2 Vault

| # | Test | Expected | Result |
| ------------ | -------------------------------------------------------------------------- | ---------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| V1 | Application token reads `secret/data/notification` | 200 | **200** |
| V2 | Invalid token | 403 | **403** |
| V3 | Application token reads another path (`secret/data/andet`) | 403 | **403** |
| V4 | Application token tries to overwrite `secret/data/notification` | 403 | **403** |
| V5 | `docker compose restart vault`, then read the secret while Vault is sealed | 503 | **503** (`vault status`: `Sealed true`) |
| V5 (service) | Restart the Notification Service while Vault is sealed | Service does not start | The service crashed at startup with `HttpRequestException: ... 503 (Service Unavailable)` thrown from `VaultSecretReader.ReadAsync` |
| V5b | Unseal with two of the three keys, read the secret | 200 | **200** (`vault status`: `Sealed false`), the secret survived the restart |
| V6 | Rebuild and start the service with Vault unsealed | Service starts | Service started and tests 9 and 10 succeeded, which also shows that it read the signing key from Vault |
