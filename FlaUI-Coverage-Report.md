# FlaUI UI Test Coverage Report

**Date:** 2026-11-11  
**Project:** SmartCafeteria Desktop (`net10.0-windows`, WPF)  
**Test project:** `tests/SmartCafeteria.UI.Tests`  
**Runner:** FlaUI UIA3 + xUnit, serialized by `AssemblyInfo.cs`

## Result

| Metric | Value |
|---|---|
| Full UI suite | **66 passed / 0 failed / 0 skipped** |
| Duration | 4.55 minutes |
| Build | **0 warnings / 0 errors** |
| Isolated data | Per-test temp copy of SQLite DB + `printer.json` |
| Live database | Unchanged: `A690F4C6E4794003BE05F4252329A5190A004E47C9724143D7D8F58711352651` |
| Live printer config | Unchanged: `8D78C34B30CB7FC6255E39AD2EEBEF31D47CD9BB7B5A24D4FD5B920AC40D0BA1` |
| Published exe | `publish\win-x64\SmartCafeteria.exe`, SHA256 `7CB6D2F4C24396B3B752329C7785661DB342A935DCE559DB2DD37A0487D5BFF3` |

The suite starts the real desktop executable, logs in through the real login window, and drives the
UI through UI Automation. No test opens or writes the live user database.

## Coverage by screen / feature

| Area | Tests | Covered behavior |
|---|---|---|
| Login | 6 | Window controls, developer credits, empty-credential validation, wrong password, inactive account, no extra inputs |
| Session / roles | 2 | Cashier hides all admin-only navigation; logout returns to the login window |
| Navigation | 3 | Arabic window title, Arabic nav labels, admin Users screen |
| Menu / sales | 7 | Product rendering, product cards, add to cart, cart quantity +/-, remove item, category filter, search filtering, empty checkout is a no-op |
| POS flow | 5 | Quick add, cart count, order type default/switch, order note, checkout clears cart and resets order fields |
| Orders | 2 | Header/status filters, rendered content |
| Order cancellation | 1 | Cancel order, filter cancelled orders, admin permanent delete with confirmation |
| Inventory | 4 | Header/list, edit dialog open/cancel, add product persists to list, search filters all products out |
| Users | 4 | Screen renders, user list, add user, missing-username validation |
| Accounting | 1 | Stats render and expense can be added |
| Suppliers | 3 | Add supplier, PO actions hidden without selection, PO dialog opens with selected supplier and cancels |
| Employees | 1 | Add employee and open salary dialog |
| Reports | 2 | Stats/buttons/labels, date-range section |
| Settings | 3 | Sections render, save status, ticket font-size slider |
| Reports/Settings view-model regressions | 11 | Date range, PDF ranges, font-size and line-spacing persistence/clamping |
| Smoke | 2 | Executable exists, login window appears |

## New coverage added in this pass

1. `Login_EmptyCredentials_ShowsValidationError`
2. `Login_InactiveUser_ShowsInactiveError`
3. `Cashier_HidesAdminOnlyNavigation`
4. `Logout_ReturnsToLoginWindow`
5. `Menu_CartQuantityControls_UpdateTotalsAndRemoveItem`
6. `Menu_EmptyCheckout_DoesNotCreateOrder`
7. `Inventory_Search_FiltersOutAllProducts`
8. `UserScreen_AddUser_WithoutUsername_ShowsValidation`
9. `Suppliers_WithoutSelection_HidesPurchaseOrderActions`

## Fixes required to make the suite reliable

- Added test-only database/config overrides (`SMART_CAFETERIA_DB_PATH`,
  `SMART_CAFETERIA_PRINTER_CONFIG`) so every test uses a disposable copy.
- Seeded deterministic `ui_admin` and `ui_cashier` accounts in the copy.
- Added login retry to absorb first-launch timing.
- Updated stale selectors/assertions for product cards, Arabic category labels, report labels,
  POS order-type values, and the receipt modal removal.
- Fixed the supplier list binding to `Mode=TwoWay`; without it, selecting a supplier in UI Automation
  did not enable the purchase-order actions.
- Targeted the supplier list by its section title instead of the first `List` in the window, which is
  the system-log list.

## Commands

```powershell
$dn = "$env:LOCALAPPDATA\Microsoft\dotnet-sdk\dotnet.exe"
& $dn build tests\SmartCafeteria.UI.Tests\SmartCafeteria.UI.Tests.csproj -c Release --no-restore
& $dn test tests\SmartCafeteria.UI.Tests\SmartCafeteria.UI.Tests.csproj -c Release --no-build
```

## Remaining risks / not yet automated

- Destructive user/product/supplier deletes are not exercised end-to-end; only order deletion is.
- Print output and PDF contents are not validated through the UI.
- Account lockout is covered below the UI layer, not through five repeated login attempts.
- Cashier restrictions are verified for navigation visibility, not by invoking hidden commands.
- Tests intentionally depend on visible Arabic labels; future copy changes require selector updates.
- UI tests remain serialized because multiple app windows would contend for focus/UIA.

## Verdict

The full FlaUI suite is green against isolated data with the live database and printer settings
byte-for-byte unchanged. Coverage now spans every top-level view, core role/session behavior, and the
main POS, cart, inventory, supplier, accounting, employee, reports, and settings flows.
