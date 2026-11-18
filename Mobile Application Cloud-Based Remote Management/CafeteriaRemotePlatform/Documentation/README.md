# Cafeteria Remote Platform

Cloud and mobile companion for Smart Cafeteria. The desktop application remains the
operational system of record; the cloud platform authenticates users, stores tenant data,
queues remote commands, and relays realtime updates between the mobile app and linked
desktop installations.

## Components

| Path | Purpose |
| --- | --- |
| `Cloud/Cafeteria.Domain` | Tenants, users, installations, commands, notifications, events |
| `Cloud/Cafeteria.Contracts` | DTOs shared by the API and desktop integration |
| `Cloud/Cafeteria.Application` | Command handling, validation, tenant rules |
| `Cloud/Cafeteria.Infrastructure` | EF Core/PostgreSQL persistence and migrations |
| `Cloud/Cafeteria.Api` | REST API, JWT authentication, SignalR hubs, background services |
| `DesktopIntegration/Cafeteria.RemoteAccess` | Desktop agent: linking, local API, outbox, command ledger |
| `Mobile/CafeteriaManager` | Expo/React Native manager app |
| `Tests/Cafeteria.Cloud.Tests` | Cloud API, application, and security tests |
| `DesktopIntegration/Cafeteria.RemoteAccess.Tests` | Desktop integration tests |

## Prerequisites

- .NET 10 SDK
- PostgreSQL 16
- Node.js 20+ and npm
- Android Studio emulator or a physical Android device
- Expo/EAS credentials only for push notifications and store builds

## Configuration

`Cloud/Cafeteria.Api/appsettings.json` contains empty secrets and must be configured
through environment variables or a secret manager in any shared environment.

Required values:

```text
ConnectionStrings__Default
Jwt__UserSigningKey
Jwt__InstallationSigningKey
```

Both signing keys must be at least 32 characters. Optional settings cover SMTP,
Expo push, CORS, and automatic migrations:

```json
{
  "Jwt": {
    "Issuer": "CafeteriaCloud",
    "Audience": "CafeteriaClients",
    "UserSigningKey": "<secret>",
    "InstallationSigningKey": "<secret>",
    "UserAccessTokenMinutes": 30,
    "InstallationAccessTokenHours": 24
  },
  "Smtp": {
    "Enabled": false
  },
  "ExpoPush": {
    "Enabled": false,
    "AccessToken": "<secret>"
  },
  "Cors": {
    "AllowedOrigins": []
  },
  "Database": {
    "AutoMigrate": true
  }
}
```

Native mobile requests are not subject to browser CORS. Add explicit origins only when
serving the Expo web build.

## Local Development

Create the database and apply migrations:

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet-sdk\dotnet.exe" tool install --global dotnet-ef --version 10.0.15
& "$env:LOCALAPPDATA\Microsoft\dotnet-sdk\dotnet.exe" ef database update `
  --project "Cloud\Cafeteria.Infrastructure" `
  --startup-project "Cloud\Cafeteria.Api"
```

If the server applies migrations through a deployment step instead of EF tooling, use the
generated idempotent script in `Documentation\schema.sql`.

Run the API:

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet-sdk\dotnet.exe" run --project "Cloud\Cafeteria.Api"
```

The development profile listens on `http://localhost:5080`. The Android emulator reaches
the host through `http://10.0.2.2:5080`; set the real cloud URL from the mobile login
screen's **Advanced** section or from **Settings**.

Run the mobile app:

```powershell
Set-Location "Mobile\CafeteriaManager"
npm install
npx expo start
```

## API Surface

| Area | Routes |
| --- | --- |
| Auth | `POST /api/auth/register`, `login`, `refresh`, `logout`, `accept-invitation`; `GET /api/auth/me` |
| Tenants | `GET /api/tenants`; members and invitations under `/api/tenants/{tenantId}` |
| Installations | List/detail and claim codes under `/api/tenants/{tenantId}/installations` |
| Installation agent | `POST /api/installations/claim`, `token`, `heartbeat`, `events` |
| Commands | User commands under `/api/tenants/{tenantId}/commands`; agent pending/ack/complete under `/api/installations/commands` |
| Notifications | List/read/unread under `/api/tenants/{tenantId}/notifications`; device registration at `POST /api/notifications/devices` |
| Health | `GET /health` |
| Realtime | `/hubs/user` and `/hubs/installation` |

All tenant data is scoped by tenant ID and authorized against the authenticated user or
installation. User and installation tokens cannot be used interchangeably.

## Remote Commands

Commands are typed and allowlisted. The API and desktop never execute arbitrary SQL or
code.

| Command | Requirements |
| --- | --- |
| `dashboard.snapshot` | Installation online |
| `reports.sales` | Installation online; `start`/`end` range |
| `users.list` | Installation online |
| `users.lock`, `users.unlock` | Installation online; non-empty `userId` |
| `inventory.list` | Installation online |
| `inventory.adjust` | Queueable; non-empty `productId`, non-zero integer `delta` |

Commands are idempotent, expire when stale, and are revalidated by the desktop before
execution. `inventory.adjust` uses `IProductService.UpdateInventoryAsync(productId, -delta)`.

## Realtime Events

`/hubs/user` emits:

- `CommandCreated`
- `CommandCompleted`
- `NotificationReceived`
- `InstallationStatusChanged`

`/hubs/installation` delivers commands and accepts heartbeat, pending-command, completion,
and event-batch messages from linked desktops.

## Testing

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet-sdk\dotnet.exe" test "CafeteriaRemotePlatform.sln" --nologo

Set-Location "Mobile\CafeteriaManager"
npx tsc --noEmit
npm run lint
npx expo-doctor
npx expo export --platform android
```

The platform solution currently contains 70 tests: 48 cloud and 22 desktop-integration.

## Production Notes

- Serve the API over HTTPS only.
- Store JWT, SMTP, Expo, and database credentials outside the repository.
- Configure `Cors:AllowedOrigins` only for trusted web origins.
- Keep `Database:AutoMigrate` disabled when migrations are applied by a deployment step.
- Register Expo, Apple, and Google push credentials before relying on notifications.
- Treat a linked desktop installation secret as a machine credential and rotate it through
  unlink/relink if exposed.

## Known Limitations

- V1 supports reads for dashboard, reports, users, and inventory; user lock/unlock is
  online-only; inventory adjustment is the only queueable mutation.
- Product/price changes, order deletion, desktop settings changes, and remote printing are
  not exposed remotely.
- Inventory adjustments are additive; V1 does not provide a set-to-value operation.
- Push notifications degrade gracefully when Expo credentials are absent.
- The mobile app targets native Android/iOS. Expo web requires CORS configuration.
