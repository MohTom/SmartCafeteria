# Smart Cafeteria Desktop - Code Review Report

**Review date:** 2026-11-10
**Review type:** Read-only code review (no source files were modified)
**Scope:** Entire solution, with emphasis on the 2026-11-08/09 login, ordering, receipt, and cashier-summary changes
**Stack:** .NET 10 WPF, C#, Dapper + SQLite, CommunityToolkit.Mvvm 8.2.2, QuestPDF 2024.12.3

> This folder is not a Git repository, so a diff against a base branch could not be produced. The review was based on a full source scan plus file timestamps. Findings are grouped by severity, not by change status.

---

## Executive Summary

**Decision: BLOCK.** The application has two critical security weaknesses and several high-severity reliability issues. The codebase is generally well-structured, SQL is parameterized, order creation is transactional, and the test suites that were run pass. The main risks are the shipped default administrator credential, authorization that exists only in the UI, swallowed exceptions, and UI-thread blocking during PDF/printing work.

| Area | Status |
|---|---|
| Build | Passes with the working SDK (0 warnings, 0 errors) |
| Core tests | 51/51 passed |
| Data tests | 48/48 passed |
| Services tests | 255/255 passed |
| UI tests | Not run (launch the real executable and use the live database) |
| Coverage threshold | Not configured or measured |
| CRITICAL issues | 2 |
| HIGH issues | 10 |
| MEDIUM issues | 14 |
| LOW / style issues | 7 |

### Verification performed

- Build: `& "$env:LOCALAPPDATA\Microsoft\dotnet-sdk\dotnet.exe" build SmartCafeteria.sln -v minimal --nologo`
- Tests: `dotnet test` on Core, Data, and Services projects with the same SDK.
- The machine-wide .NET SDK currently fails with `MSB6006: "csc.exe" exited with code 1`; that is an environment problem, not a source problem. The workaround SDK must be documented for contributors until the machine SDK is repaired.

---

## CRITICAL Issues (must fix before shipping)

### [CRITICAL] Default administrator account ships with a known password
**File:** `src/SmartCafeteria/App.xaml.cs:86-96`
**Also:** `tests/SmartCafeteria.UI.Tests/AppTestBase.cs:51`, `tests/SmartCafeteria.UI.Tests/OrderCancelFlowTests.cs:39`

**Issue:** Every fresh installation creates an active `admin` account with the hardcoded password `admin123`. Nothing forces the password to change on first login. Anyone who installs or obtains the application can sign in as administrator, view financial reports, manage users, and change settings. The UI tests also depend on this password, which makes the insecure default harder to remove.

**Fix:**
- Generate a random one-time password during first-run setup and show it once in a modal dialog, or require the operator to create the administrator through a first-run wizard.
- Add a `MustChangePassword` column to `Users` and refuse all navigation until it is cleared.
- Do not seed an administrator silently from `OnStartup`.
- Supply test credentials through an environment variable or test fixture, not through a production default.

```csharp
// App.xaml.cs - first run only
var initialPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));
var admin = new User {
    Username = "admin",
    FullName = "مدير النظام",
    Role = UserRole.Admin,
    MustChangePassword = true
};
await userService.CreateUserAsync(admin, initialPassword);
FirstRunCredentialsDialog.Show(initialPassword); // show once, never log it
```

### [CRITICAL] Authorization is enforced only by UI navigation
**File:** `src/SmartCafeteria/ViewModels/MainViewModel.cs:124-147`
**Also:** `src/SmartCafeteria/ViewModels/UserManagementViewModel.cs:49-155`, `src/SmartCafeteria/ViewModels/OrdersViewModel.cs:125-136`, `src/SmartCafeteria.Data/Repositories/UserRepository.cs:35-84`

**Issue:** Cashier restrictions exist only in `MainViewModel.NavigateAsync` and in `Visibility` bindings in `MainWindow.xaml`. `UserManagementViewModel.AddUserAsync`, `UpdateUserAsync`, `DeleteUserAsync`, and `ToggleActiveAsync` never check that the current user is an administrator. The repositories and services accept any caller. One navigation or binding mistake exposes user management, financial data, or destructive operations. The check is also bypassed entirely by any future screen, command, or code path that does not go through `NavigateAsync`.

**Fix:** Enforce authorization at the service boundary, in addition to the UI check. Pass the acting user (or a scoped authorization service) into mutating methods and reject unauthorized calls there. Add tests that call the service directly as a cashier and assert `UnauthorizedAccessException`.

```csharp
// Example service-level guard
public async Task UpdateUserAsync(User actor, User target)
{
    if (actor.Role != UserRole.Admin || !actor.IsActive)
        throw new UnauthorizedAccessException("Administrator role required.");
    // ... existing update logic
}
```

---

## HIGH Issues (should fix before shipping)

### [HIGH] No brute-force protection or failed-login auditing
**File:** `src/SmartCafeteria/ViewModels/LoginViewModel.cs:36-56`, `src/SmartCafeteria/Data/Repositories/UserRepository.cs:86-96`

**Issue:** Failed logins return silently, are not counted, are not delayed, and are not written to the system log. There is no lockout, no backoff, and no audit trail for repeated failures. `VerifyPassword` also performs less work for unknown usernames because the user lookup returns before hashing, which exposes a timing difference.

**Fix:** Add `FailedLoginCount` and `LockoutUntil` columns, lock after 5 failures for 15 minutes, and log every failure with the username and timestamp (never the password). Always perform a PBKDF2 verification against a dummy hash when the user is not found.

### [HIGH] Silent exception swallowing across the application
**File examples:**
- `src/SmartCafeteria/App.xaml.cs:25`
- `src/SmartCafeteria.Data/Database/DbContext.cs:34`
- `src/SmartCafeteria.Data/Repositories/OrderRepository.cs:99,163,204`
- `src/SmartCafeteria.Data/Repositories/ProductRepository.cs:102,131`
- `src/SmartCafeteria.Services/PdfReportService.cs:40,69,72`
- `src/SmartCafeteria.Services/AnalyticsPdfService.cs:72,194,198`
- `src/SmartCafeteria/ViewModels/MainViewModel.cs:93,183`
- `src/SmartCafeteria/ViewModels/ReportsViewModel.cs:87`
- `src/SmartCafeteria.Printing/ThermalPrinter.cs:372`

**Issue:** Approximately 23 `catch { }` blocks discard failures. Font registration, report cleanup, legacy database copy, transaction rollback, printing, and UI refresh failures all disappear. When a PDF prints blank or a receipt is cut incorrectly, there is no diagnostic evidence. Some catches also hide data-loss paths, such as a failed legacy database copy.

**Fix:** Never swallow an exception without recording it. Inject `ILogService` into services where possible; for low-level static helpers, use `System.Diagnostics.Trace`/`Debug` with the exception. Keep the safe fallback behavior, but record the failure and, where a user action just failed, surface a short message.

```csharp
catch (Exception ex)
{
    Trace.TraceWarning("Arabic font registration failed: {0}", ex);
    // Keep the fallback font, but the failure is now observable.
}
```

### [HIGH] Weak password policy and below-guideline PBKDF2 work factor
**File:** `src/SmartCafeteria.Data/Repositories/UserRepository.cs:109-124`, `src/SmartCafeteria/ViewModels/UserManagementViewModel.cs:64-68`

**Issue:** The repository hashes any string, including an empty password. The UI only rejects `null`/empty on create, accepts one-character passwords, and has no complexity or length rule. PBKDF2-SHA256 uses 100,000 iterations, below the OWASP recommendation of roughly 600,000 for this construction. There is no rehash-on-login path for future increases.

**Fix:** Enforce 12+ characters (or a passphrase policy) at the service layer, use 600,000 iterations, and store a versioned hash (`v2$600000$salt$hash`) so `AuthenticateAsync` can rehash older records. Consider `Microsoft.AspNetCore.Identity.PasswordHasher<T>` instead of a custom implementation.

### [HIGH] Administrator-separation invariants are incomplete
**File:** `src/SmartCafeteria/ViewModels/UserManagementViewModel.cs:89-155`

**Issue:** Deleting the last administrator is blocked, but changing that administrator's role to Cashier is not, and `ToggleActiveAsync` does not prevent deactivating the last active administrator. An administrator can therefore remove every usable administrator account and lock the business out of management functions. The checks also live only in the ViewModel, so a direct service call bypasses them.

**Fix:** Enforce the invariant in the service inside a transaction: after any role change, deactivation, or deletion, at least one active administrator must remain. Reject self-demotion and self-deactivation explicitly. Add unit tests for each operation.

### [HIGH] Password hashes are loaded into UI-bound user objects
**File:** `src/SmartCafeteria.Data/Repositories/UserRepository.cs:23-33`, `src/SmartCafeteria/ViewModels/UserManagementViewModel.cs:40-44`

**Issue:** Both `GetUserByIdAsync` and `GetUserByUsernameAsync` use `SELECT *`, and `GetAllUsersAsync` does the same. `UserManagementViewModel.Users` therefore holds live `PasswordHash` values. Any future diagnostic dump, serializer, or binding mistake exposes them. The UI never needs the hash.

**Fix:** Project explicit columns for all user-management reads and load `PasswordHash` only inside the authentication query. Introduce a separate DTO for the user list.

```csharp
private const string PublicUserColumns =
    "Id, Username, FullName, Role, IsActive, CreatedAt";
```

### [HIGH] Raw printing ignores Win32 failures and can leak unmanaged memory
**File:** `src/SmartCafeteria.Printing/RawPrinterHelper.cs:39-63`

**Issue:** `StartDocPrinter`, `StartPagePrinter`, `WritePrinter`, `EndPagePrinter`, and `EndDocPrinter` return values are ignored. `Marshal.AllocCoTaskMem` is not protected by `try/finally`, so an exception between allocation and free leaks the buffer. The method returns `true` even when the printer rejects the job, so callers cannot report a failed receipt.

**Fix:** Check every return value, capture `Marshal.GetLastWin32Error()`, free the buffer in `finally`, and return `false` on any failure. Use a single cleanup path for page and document handles.

```csharp
IntPtr pBytes = IntPtr.Zero;
try
{
    if (!StartDocPrinter(hPrinter, 1, di)) return false;
    if (!StartPagePrinter(hPrinter)) return false;
    pBytes = Marshal.AllocCoTaskMem(bytes.Length);
    Marshal.Copy(bytes, 0, pBytes, bytes.Length);
    if (!WritePrinter(hPrinter, pBytes, bytes.Length, out var written) ||
        written != bytes.Length)
        return false;
    return true;
}
finally
{
    if (pBytes != IntPtr.Zero) Marshal.FreeCoTaskMem(pBytes);
    EndPagePrinter(hPrinter);
    EndDocPrinter(hPrinter);
}
```
### [HIGH] PDF generation and printing run synchronously on the UI thread
**File:** `src/SmartCafeteria.Services/PdfReportService.cs:44-54`, `src/SmartCafeteria.Services/AnalyticsPdfService.cs:76-178`, `src/SmartCafeteria.Printing/ThermalPrinter.cs:42-60`, `src/SmartCafeteria/ViewModels/ReportsViewModel.cs:198-286`, `src/SmartCafeteria/ViewModels/CartViewModel.cs:91-127`

**Issue:** The PDF methods only wrap synchronous QuestPDF work in `Task.FromResult`, and the print methods return `Task.CompletedTask` after synchronous GDI/printer work. Awaiting them does not yield, so the WPF dispatcher freezes during report export and receipt printing. Large reports can make the window appear hung. `ReportsAnalyticsViewModel` is also exposed to this through the same services.

**Fix:** Move CPU/IO-heavy work to a background thread with `Task.Run` and pass a `CancellationToken`; keep only UI collection updates on the dispatcher. For printing, run the GDI and Win32 calls on a background task and report failures back to the UI. Disable the command while the operation runs to prevent double submission.

```csharp
public Task<string> GenerateSalesReportAsync(...)
    => Task.Run(() => GenerateSalesReport(...));
```

### [HIGH] Missing business validation in inventory and supplier workflows
**File:** `src/SmartCafeteria/ViewModels/InventoryViewModel.cs:87-109`, `src/SmartCafeteria/ViewModels/SuppliersViewModel.cs:129-165,188-244,294-314`

**Issue:** Products can be added with empty names, a negative price, a negative inventory, or a blank category. Supplier purchase-order lines accept negative unit costs, which produces negative totals. Supplier name validation only checks for whitespace, and e-mail/phone values are unvalidated. Product IDs use a millisecond timestamp (`p-custom-{UnixMs}`), so two rapid additions can collide. None of this is validated below the ViewModel.

**Fix:** Validate at the service boundary with a shared validator (DataAnnotations or FluentValidation): name required and length-limited, price >= 0, inventory >= 0, quantity > 0, unit cost >= 0, valid e-mail if supplied. Use `Guid.NewGuid()` for product IDs. Return a typed validation result instead of failing in the database.

### [HIGH] Receipt numbers are generated with a non-atomic COUNT + 1
**File:** `src/SmartCafeteria.Data/Repositories/OrderRepository.cs:27-31`

**Issue:** The receipt code is `COUNT(*) of today's orders + 1`, calculated outside a dedicated counter. Cancelled orders are included, deleted orders cause number reuse, and two terminals writing to the same database can allocate the same number. Duplicate receipt numbers undermine cash reconciliation and auditability.

**Fix:** Add an `OrderCounters(Date, LastNumber)` table and increment it inside the same transaction, or add a unique index on `(Date, Code)` and retry on conflict. Do not derive accounting identifiers from row counts.

```sql
INSERT INTO OrderCounters(Date, LastNumber) VALUES (@Date, 1)
ON CONFLICT(Date) DO UPDATE SET LastNumber = LastNumber + 1
RETURNING LastNumber;
```

### [HIGH] The global crash handler logs and then terminates without informing the user
**File:** `src/SmartCafeteria/App.xaml.cs:28-51`

**Issue:** `DispatcherUnhandledException` writes a log and sets `args.Handled = false`, so the process terminates silently. `OnStartup` is `async void` and rethrows after logging, which is not a reliable place to recover or present an error. Users see the application vanish and have no idea where the log went.

**Fix:** Set `Handled = true`, show a modal error dialog with the log path and a short instruction, then call `Shutdown(1)`. Avoid rethrowing from an `async void` startup handler; handle startup failure inside the method and show the same dialog.

```csharp
DispatcherUnhandledException += (_, args) =>
{
    WriteCrashLog(args.Exception);
    MessageBox.Show(
        $"حدث خطأ غير متوقع. تم حفظ التفاصيل في:\n{LogPath}",
        "Smart Cafeteria", MessageBoxButton.OK, MessageBoxImage.Error);
    args.Handled = true;
    Shutdown(1);
};
```

---

## MEDIUM Issues (should fix soon)

### [MEDIUM] N+1 queries when loading orders and products
**File:** `src/SmartCafeteria.Data/Repositories/OrderRepository.cs:106-116,229-239`, `src/SmartCafeteria.Data/Repositories/ProductRepository.cs:17-27,46-56,60-72`

**Issue:** Each order triggers a separate query for its items, and each product triggers a separate query for its customizations. A 200-order report performs roughly 201 queries; a menu with 100 products performs 101. This also runs during startup and analytics, where all data is loaded.

**Fix:** Load child rows in one query using `WHERE OrderId IN @Ids` (Dapper expands lists) or a `JOIN`, then group in memory. For analytics, push the date filter into SQL rather than loading every order and filtering in C# (`AnalyticsService.cs:47-51`).

### [MEDIUM] Fire-and-forget tasks hide failures and allow overlapping initialization
**File:** `src/SmartCafeteria/ViewModels/MainViewModel.cs:80`, `src/SmartCafeteria/ViewModels/OrdersViewModel.cs:48`, `src/SmartCafeteria/ViewModels/SettingsViewModel.cs:221,234`

**Issue:** `_ = InitializeAsyncSafe()` and the settings persistence calls discard the task. Failures are only partially caught, and startup can race with a user navigating to a screen, causing overlapping loads and stale state. `SettingsViewModel` can fire several saves for rapid slider changes with no ordering guarantee.

**Fix:** Store and await initialization from the window's `Loaded` handler, or expose an `AsyncRelayCommand` with `IsRunning`. Coalesce/debounce settings saves and serialize them through a single queue.

### [MEDIUM] Notification timer race hides newer messages early
**File:** `src/SmartCafeteria/ViewModels/MainViewModel.cs:187-200`

**Issue:** Each call starts its own three-second delay. If two notifications arrive within three seconds, the first delay expires while the second message is still visible and clears it. The newest message disappears too soon.

**Fix:** Cancel the previous delay with a `CancellationTokenSource` and restart the timer for each new message.

```csharp
private CancellationTokenSource? _notificationCts;

public async Task ShowNotificationAsync(string message, string type = "success")
{
    _notificationCts?.Cancel();
    var cts = _notificationCts = new CancellationTokenSource();
    // set message, colors, visibility
    try { await Task.Delay(3000, cts.Token); }
    catch (OperationCanceledException) { return; }
    ShowNotification = false;
}
```

### [MEDIUM] LIKE wildcard injection in product search
**File:** `src/SmartCafeteria.Data/Repositories/ProductRepository.cs:60-66`

**Issue:** The search pattern is `%{query}%` with no escaping. A user entering `%` matches every product, and `_` matches arbitrary characters. This is not SQL injection because the query is parameterized, but it is incorrect search behavior and an easy way to force a full-table scan.

**Fix:** Escape `\`, `%`, and `_` in the search term and add `ESCAPE '\'` to each `LIKE`.

```csharp
var pattern = "%" + query
    .Replace("\\", "\\\\")
    .Replace("%", "\\%")
    .Replace("_", "\\_") + "%";
const string sql = "... NameAr LIKE @Pattern ESCAPE '\\' ...";
```

### [MEDIUM] Predictable temp report filenames and cross-user cleanup
**File:** `src/SmartCafeteria.Services/PdfReportService.cs:56-73`, `src/SmartCafeteria.Services/AnalyticsPdfService.cs:89,119,157,180-199`

**Issue:** Report names use second-resolution timestamps, so two exports in the same second overwrite each other. Cleanup enumerates and deletes `*Report_*.pdf` files in the shared temp folder, including files created by other users or processes on the same machine. Predictable names also make it easier for another local user to guess and open reports.

**Fix:** Use a per-user subfolder such as `%TEMP%\SmartCafeteria\Reports`, include a GUID in the filename, and delete only files owned by the current user or only files the application created in that subfolder. Consider deleting the report when the user closes it, or let the user choose the save location.

### [MEDIUM] QuestPDF Community license is set unconditionally at runtime
**File:** `src/SmartCafeteria.Services/AnalyticsPdfService.cs:87,117,155`, `src/SmartCafeteria.Services/PdfReportService.cs` (license setup in the report generation path)

**Issue:** `QuestPDF.Settings.License = LicenseType.Community` is assigned on every export. Community use is only valid below QuestPDF's revenue threshold, and setting a global static from multiple services concurrently is not a good configuration pattern. If the business exceeds the threshold, this is a license-compliance issue.

**Fix:** Set the license once during startup after confirming eligibility. If the business is not eligible, purchase a license. Centralize the configuration in one place and document the eligibility assumption.

### [MEDIUM] User creation has a TOCTOU race and mutates its input
**File:** `src/SmartCafeteria.Data/Repositories/UserRepository.cs:35-58`, `src/SmartCafeteria/ViewModels/UserManagementViewModel.cs:69-86`

**Issue:** `CreateUserAsync` checks for an existing username and then inserts. Two concurrent calls can pass the check and hit the unique constraint. The method also overwrites `user.PasswordHash` on the caller's object, and `UpdateUserAsync` accepts an empty username with no validation. The UI does not catch a database constraint error, so `AddUserAsync` can crash the command.

**Fix:** Add a unique index on `Username`, catch `SqliteException` with `SQLITE_CONSTRAINT_UNIQUE`, and translate it to a user-facing "username already exists" message. Do not mutate the input object: build the row parameters locally. Validate the username and role in `UpdateUserAsync`.

### [MEDIUM] Raw exception messages are shown to users
**File:** `src/SmartCafeteria/ViewModels/ReportsAnalyticsViewModel.cs:64-67,120-123,138-141`, `src/SmartCafeteria/ViewModels/SettingsViewModel.cs:119-127,205-215`, `src/SmartCafeteria/ViewModels/CartViewModel.cs:123-126`

**Issue:** `ex.Message` is written directly to the status bar or log, which can expose file paths, SQLite details, or internal type names. It is also inconsistent: some code logs the exception, some shows it, and some does both.

**Fix:** Show a short, translated message ("تعذر إنشاء التقرير. حاول مرة أخرى.") and log the full exception with a correlation ID. Include the log path in a details expander if operators need diagnostics.

### [MEDIUM] Destructive actions have no confirmation
**File:** `src/SmartCafeteria/ViewModels/InventoryViewModel.cs:158-164`, `src/SmartCafeteria/ViewModels/SuppliersViewModel.cs:167-174,269-276`, `src/SmartCafeteria/ViewModels/UserManagementViewModel.cs:114-137`, `src/SmartCafeteria/ViewModels/OrdersViewModel.cs:70-90`

**Issue:** Delete product, delete supplier, delete user, cancel purchase order, cancel order, and permanent order deletion execute immediately. For full financial and inventory data, a single misclick is destructive and irreversible. The UI does show a status message afterwards, but there is no pre-action confirmation.

**Fix:** Add a shared confirmation service or dialog with the object name and consequence, require an explicit "yes", and keep audit logging after the action. Consider soft deletion for orders and products so history is preserved.

### [MEDIUM] Ticket configuration saves are not serialized
**File:** `src/SmartCafeteria.Core/Configuration/TicketConfigStore.cs:63-81`, `src/SmartCafeteria/ViewModels/SettingsViewModel.cs:216-234`

**Issue:** The write-then-move pattern is a good start, but every save uses the same `.tmp` name and there is no lock. Rapid slider changes can interleave, one save can move the temp file while another is writing it, and `File.Move` can throw. Failures are logged but the last value may not be persisted.

**Fix:** Guard `Save` with a private lock, use a unique temp name (`Guid`), and serialize saves through a single background queue. Catch and report `IOException` with a retry.

### [MEDIUM] Time handling mixes local `DateTime.Now` with SQLite's local date
**File:** `src/SmartCafeteria.Data/Repositories/OrderRepository.cs:27-31,50,84` and other entities using `DateTime.Now`

**Issue:** Timestamps are stored as unspecified local time while SQLite computes "today" with `date('now','localtime')`. Around DST changes, midnight boundaries, or if the machine time zone changes, the receipt counter and date-range reports can disagree. Mixed local timestamps also make future multi-terminal synchronization unreliable.

**Fix:** Store UTC timestamps (ISO 8601) and convert to local time only for display and report labels. Compute the business day boundary once in C# and pass explicit start/end parameters to SQL.

### [MEDIUM] Inventory filtering reallocates the collection on every keystroke
**File:** `src/SmartCafeteria/ViewModels/InventoryViewModel.cs:62-77`

**Issue:** Every search-query change creates a new `ObservableCollection<Product>` and raises a collection reset, causing full list rebinding and losing selection/scroll state. `NameEn.Contains(...)` will throw if `NameEn` is null in legacy rows.

**Fix:** Use `ICollectionView`/`CollectionViewSource` with a filter delegate, or update the existing collection in place. Use null-safe comparisons (`p.NameEn?.Contains(...) == true`).

### [MEDIUM] No unit tests for the critical ViewModels
**File:** `tests/SmartCafeteria.UI.Tests/` (only automation tests), absence of ViewModel tests

**Issue:** Login, Main navigation/authorization, UserManagement, Cart checkout, Orders cancel/permissions, and Settings persistence have no unit tests. The UI tests launch the real executable, depend on the seeded admin credential, and touch a live database, so they are not suitable for fast regression coverage and were not run in this review. There is no coverage threshold in the build.

**Fix:** Add a ViewModel test project (or expand the existing one) using mocked service interfaces. Cover role gating, last-admin invariants, failed login behavior, checkout failure paths, and settings persistence. Add `coverlet.msbuild` with a minimum 80% line threshold for Core/Data/Services and the ViewModels.

### [MEDIUM] Accessibility coverage is minimal
**File:** `src/SmartCafeteria/Views/*.xaml` (9 of 13 views have zero `AutomationProperties`)

**Issue:** Most views contain no `AutomationProperties.Name` or `LabeledBy` associations, even though the project already uses UI automation tests. Screen readers will announce unlabeled edit fields, and the automation tests rely on brittle control-type searches. Icon-only navigation buttons have no accessible name.

**Fix:** Add `AutomationProperties.Name` to interactive controls, associate labels with inputs via `AutomationProperties.LabeledBy`, give icon-only buttons descriptive names, and verify tab order and focus visuals. Include a high-contrast check before release.
---

## LOW / Style Issues

### [LOW] Oversized style resource dictionary
**File:** `src/SmartCafeteria/Themes/ControlStyles.xaml` (1,012 lines)

**Issue:** This exceeds the project's 800-line guideline and mixes buttons, inputs, cards, dialogs, and navigation styles. It is harder to review and causes broad merge conflicts.

**Fix:** Split into `Buttons.xaml`, `Inputs.xaml`, `Cards.xaml`, `Navigation.xaml`, and `Dialogs.xaml`, merged from a small `ControlStyles.xaml`. Keep each file under 300-400 lines.

### [LOW] Emojis in UI markup and seed data
**File:** `src/SmartCafeteria/Views/MainWindow.xaml:27-100`, `src/SmartCafeteria/Views/AccountingView.xaml:173,178`, `src/SmartCafeteria/Views/EmployeesView.xaml:81,86`, `src/SmartCafeteria/Views/SuppliersView.xaml:84,89,303`, `src/SmartCafeteria/Views/ReportsView.xaml:23-84`, `src/SmartCafeteria/Views/ReportsAnalyticsView.xaml:16,87`

**Issue:** The project guidelines say no emojis in code or comments. Emoji glyphs also render inconsistently across Windows versions and fonts, and they can complicate automation names.

**Fix:** Replace them with vector icons (Path/Segoe Fluent Icons) or plain text labels. Keep one icon resource set for consistency.

### [LOW] Stale artifacts and oversized root documents
**File:** `MainWindow.xaml.bak`, `PROGRESS.md` (52 KB), `SESSION-SUMMARY-20261022.md`

**Issue:** `MainWindow.xaml.bak` is an obsolete copy of a view that can confuse search results and reviewers. The two long-lived root documents duplicate history that belongs in version control or a docs folder.

**Fix:** Delete `MainWindow.xaml.bak`. Move session history to `docs/` or keep a short `PROGRESS.md` index with links to dated notes.

### [LOW] Navigation uses string literals instead of constants
**File:** `src/SmartCafeteria/ViewModels/MainViewModel.cs:127-145`, `src/SmartCafeteria/Views/MainWindow.xaml` (CommandParameter values)

**Issue:** The same Arabic view names are repeated in the ViewModel and XAML. A typo in either side silently falls back to the menu or fails to gate a restricted view. This is not compile-time checked.

**Fix:** Define a `NavigationTarget` enum or constants and bind `CommandParameter` through a converter. Put the admin-only check on the target metadata rather than a hardcoded string list.

### [LOW] Mutation of bound model objects before persistence
**File:** `src/SmartCafeteria/ViewModels/InventoryViewModel.cs:128-147`, `src/SmartCafeteria/ViewModels/SuppliersViewModel.cs:134-142`, `src/SmartCafeteria/ViewModels/UserManagementViewModel.cs:90-111`

**Issue:** Edit dialogs mutate the object currently shown in the list, then save. If the save fails, the UI shows the new values while the database still has the old ones. This violates the project's immutability guideline and makes rollback difficult.

**Fix:** Copy the edited values into a separate draft object, persist the draft, and replace the item in the collection only after success. On failure, keep the dialog open and show the error.

### [LOW] Fire-and-forget and overlapping collection updates in checkout
**File:** `src/SmartCafeteria/ViewModels/CartViewModel.cs:91-132`

**Issue:** The cart is cleared after the order is committed even when printing fails. The failure is only logged, so the cashier may believe a receipt printed when it did not. There is also no busy guard, so a double-click can submit two orders before the first completes.

**Fix:** Disable the command while placing the order (`IsBusy`/`CanExecute`), show a clear failure message if printing fails, and offer a reprint action. Clearing the cart is correct after persistence, but the user must be told about the print failure.

### [LOW] `DateTime.Now` and `DateTime.Today` used directly
**File:** `src/SmartCafeteria.Services/PdfReportService.cs:61,115`, `src/SmartCafeteria/ViewModels/ReportsAnalyticsViewModel.cs:77-93`, `src/SmartCafeteria/ViewModels/SuppliersViewModel.cs:156,233,304`

**Issue:** Direct clock access makes tests time-dependent and prevents deterministic date-range behavior. This is a maintainability issue even where it is not a correctness bug.

**Fix:** Inject an `IClock`/`TimeProvider` (built into .NET 8+) and use it for report timestamps and period calculations. Use UTC internally as described above.

---

## Positive Observations

- All SQL in repositories is parameterized with Dapper; no string-concatenated user input was found.
- Order creation uses a transaction, conditional inventory decrement (`Inventory >= @Quantity`), and rollback on insufficient stock (`OrderRepository.cs:22-101`).
- Password storage uses a per-user random salt with PBKDF2; plaintext passwords are never stored.
- The login screen is well separated from the main window, and logout correctly reopens login without terminating the process (`App.xaml.cs:103-138`).
- PDF layout constants and analytical column widths are centralized and covered by service tests.
- The `OrderItem` receipt model and ticket layout have focused regression tests, including notes removal and spacing.
- The project has meaningful integration tests for schema, migrations, and financial calculations.

---

## Recommended Order of Work

1. Remove the default administrator credential and add forced first-login password change (CRITICAL).
2. Add service-level authorization for user management and financial operations (CRITICAL).
3. Add login lockout/audit logging and strengthen the password policy (HIGH).
4. Replace silent catches with logged failures (HIGH).
5. Fix printer error handling and move PDF/printing work off the UI thread (HIGH).
6. Enforce the last-active-administrator invariant and stop selecting password hashes into UI models (HIGH).
7. Add business validation for products, suppliers, and purchase orders (HIGH).
8. Make receipt numbering atomic and unique (HIGH).
9. Address the medium items before the next release, especially N+1 queries, temp report handling, confirmations, and ViewModel tests.
10. Clean up style issues as part of the next maintenance pass.

---

## Appendix: Main Files Reviewed

- `src/SmartCafeteria/App.xaml.cs`
- `src/SmartCafeteria/ViewModels/` (Login, Main, UserManagement, Orders, Cart, Settings, Reports, ReportsAnalytics, Inventory, Suppliers)
- `src/SmartCafeteria/Views/` (LoginWindow, MainWindow, OrdersView, UserManagementView)
- `src/SmartCafeteria.Data/Repositories/` (User, Order, Product, Settings, Supplier)
- `src/SmartCafeteria.Data/Database/` (DbContext, DatabaseInitializer)
- `src/SmartCafeteria.Services/` (PdfReportService, AnalyticsPdfService, AnalyticsService, UserSession)
- `src/SmartCafeteria.Printing/` (ThermalPrinter, PrintReceiptBuilder, RawPrinterHelper)
- `src/SmartCafeteria.Core/Configuration/TicketConfigStore.cs`
- `src/SmartCafeteria/Themes/ControlStyles.xaml`
- `installer/Product.wxs`
- `tests/SmartCafeteria.UI.Tests/AppTestBase.cs`, `OrderCancelFlowTests.cs`, and the Core/Data/Services test suites

---

*No source files were changed during this review. This report is the only file created.*