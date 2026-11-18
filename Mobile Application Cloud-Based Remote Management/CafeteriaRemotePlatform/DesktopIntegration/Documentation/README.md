# Desktop Remote Access Integration

`Cafeteria.RemoteAccess` connects the Smart Cafeteria WPF desktop to the cloud platform.
It keeps the desktop authoritative: remote requests are validated and executed locally
against the same services used by the desktop UI.

## Runtime Behavior

- Startup: load the encrypted settings file, start the loopback API, start event collection,
  and connect to the cloud only when the installation is linked and enabled.
- Login: the login flow is never dependent on cloud connectivity.
- Shutdown: the coordinator stops the SignalR connection, background loops, and local API.
- Reconnect: the supervisor retries the cloud connection while linked and enabled.
- Offline: events are queued locally and inventory adjustments can be queued by the cloud.

The desktop starts the coordinator after database and license initialization. Cloud
failures are logged and do not block the login window.

## Linking a Desktop

1. An owner, admin, or manager creates a claim code in the mobile app.
2. The desktop operator enters the cloud URL, installation name, and claim code in
   **Settings > Remote Access**.
3. The desktop exchanges the claim code for an installation ID and secret.
4. The secret is encrypted with Windows DPAPI machine scope and stored outside the
   installation directory.

Only owner/admin can invite users. Only the owner can invite another admin.

## Local Data

```text
%ProgramData%\SmartCafeteria\RemoteAccess\
  settings\
    settings.dat          DPAPI-protected installation settings
  state\
    event-state.json      event collection cursor
    event-outbox.json     pending event uploads
    command-ledger.json   idempotency and terminal command outcomes
```

Access rules:

- `settings` directory: SYSTEM and Administrators full control; Users read/execute.
- `state` directory: SYSTEM and Administrators full control; Users modify.
- Binding and unbinding require administrator rights.
- DPAPI uses `DataProtectionScope.LocalMachine`, so a moved settings file cannot be
  decrypted on another computer.

## Local API

The desktop hosts a loopback-only API on a random port:

| Route | Purpose |
| --- | --- |
| `GET /local/health` | Liveness |
| `GET /local/status` | Current remote-access status |
| `POST /local/events` | Accept local events into the outbox |

The URL and running state are exposed through `RemoteAccessStatus`. The API binds to
`127.0.0.1` only.

## Command Execution

The cloud sends typed commands over `/hubs/installation`. The desktop:

1. Records the command in the ledger before execution.
2. Rejects duplicate or previously completed commands.
3. Revalidates authorization and payload.
4. Executes through the normal desktop service layer.
5. Records the terminal result and reports completion to the cloud.

`inventory.adjust` is the only queueable mutation in V1. It applies a signed delta through
`IProductService.UpdateInventoryAsync(productId, -delta)`. All other commands require a
live connection and fail when the installation is offline.

## Security Model

- User and installation JWTs use separate signing keys and authentication schemes.
- Installation tokens are machine credentials; they are never stored in plaintext.
- Tenant and installation ownership are checked on every API request.
- Remote commands cannot invoke arbitrary SQL, shell commands, or code.
- The local API is loopback-only and does not expose desktop data directly.
- Settings and state directories use restrictive ACLs.
- Any exposed installation secret must be rotated by unlinking and relinking the desktop.
- Administrators can decrypt machine-scope settings by design; this is required for
  recovery and service operation.

## Desktop Wiring

`App.xaml.cs` registers the integration with:

```csharp
services.AddCafeteriaRemoteAccess(
    sp => sp.GetRequiredService<SmartCafeteria.Services.UserSession>());
```

The WPF project references:

- `Cafeteria.RemoteAccess`
- `Cafeteria.Contracts`

`SettingsViewModel` exposes link/unlink, enable/disable, status, local API URL, and error
messages. Remote events are collected from orders, users, products, and settings without
changing their existing service contracts.

## Testing

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet-sdk\dotnet.exe" test `
  "DesktopIntegration\Cafeteria.RemoteAccess.Tests" --nologo
```

The integration test suite covers settings persistence, DPAPI behavior, ledger idempotency,
command payload validation, event outbox behavior, and coordinator transitions.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Link fails with an authorization error | The Windows account must be an administrator |
| Settings cannot be read after moving the app | DPAPI machine scope is tied to the computer |
| Cloud stays disconnected | Verify URL, HTTPS certificate, installation access, and outbound network |
| Commands stay pending | Confirm the installation is online, enabled, and connected to `/hubs/installation` |
| Inventory adjustment fails | Verify the product exists and the resulting quantity is valid |
| Local API is unavailable | Check `RemoteAccessStatus.IsLocalApiRunning` and the Settings status panel |
| Duplicate command was ignored | Inspect `command-ledger.json`; duplicate execution is intentional |
