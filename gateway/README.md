# AgriDrone API Gateway

The gateway is the stable public HTTP boundary while capabilities move from
BE2 .NET to BE1 Java. It uses YARP and preserves the existing public URLs; a
client never needs to know which backend currently owns a route.

## Local Docker usage

From the repository root:

```powershell
docker compose up -d --build gateway
```

- Gateway API base URL: `http://localhost:8088`
- Combined Swagger UI: `http://localhost:8088/docs`
- Liveness: `http://localhost:8088/health/live`
- Readiness: `http://localhost:8088/health/ready`

The direct BE1 and BE2 host ports remain available during migration for
debugging. Once every client uses the gateway, remove their `ports` entries
from `compose.yaml` and retain only the internal Compose network addresses.

## Cutover rule

Only routes implemented and verified in Java are listed before the BE2 API
fallback. New Java capabilities must be added as explicit path-and-method
routes with a negative order, accompanied by a configuration test. Do not add
an unscoped `/{**catch-all}` route.

The three Identity routes default to BE2 because the checked-in local setup
does not enable BE1's RSA issuer and BE2 does not yet verify its tokens. After
that cross-service JWT gate passes, set `GATEWAY_IDENTITY_UPSTREAM=be1` to move
login, forgot-password and reset-password together. JWKS and the BE1 OpenAPI
document always route to BE1. Remaining `/api/**` traffic and the three
retained absolute compatibility paths stay on BE2 until their individual
cutover gates pass.

## Running outside Docker

Override the cluster destinations because Compose DNS names are unavailable:

```powershell
$env:ReverseProxy__Clusters__be1__Destinations__primary__Address = 'http://localhost:8081/'
$env:ReverseProxy__Clusters__be2__Destinations__primary__Address = 'http://localhost:8080/'
$env:Gateway__ReadinessEndpoints__0 = 'http://localhost:8081/actuator/health/readiness'
$env:Gateway__ReadinessEndpoints__1 = 'http://localhost:8080/health/ready'
dotnet run --project gateway/src/AgriDrone.Gateway/AgriDrone.Gateway.csproj
```

Production deployments should disable documentation with
`Gateway__DocsEnabled=false`, set the exact frontend origins, terminate TLS at
the gateway or ingress, and avoid publishing backend service ports.
