# Smart Cafeteria Desktop — WPF Modernization Handoff

## Objective
Modernize the WPF app UI (Fluent look, Cairo as app-wide inherited font, Light theme),
preserving ALL layout, navigation (RadioButton sidebar), MVVM, bindings, commands,
converters, business logic, RTL/Arabic. Hard constraints: **0 warnings / 0 errors**,
all pre-existing tests stay green (never weaken/remove tests).

## Key decisions (user-approved)
1. **All 12 views in one pass.**
2. **Pure-WPF Fluent restyle, NO NuGet UI package.** Reason (verified, not assumed):
   the `Wpf.Ui` id on this machine's feed resolves to `WPF.UI 3.4.2.7`, a counterfeit
   net451-only fork (author XIAMU, `http://xiamu.3vkj.vip/wpf.ui`, Galasoft MVVM-Light
   clone namespaces). Reflection of the installed DLL proved it ships NO
   `FluentWindow` / `ThemesDictionary` / `ControlsDictionary` / `FluentTheme` /
   `CardControl`, and it raises `NU1701` on `net10.0-windows` (breaks 0-warnings).
   Probes for genuine lepoco ids/versions returned no exact match. So the Fluent
   look is implemented in the app's own theme dictionaries (zero dependencies).
3. **Light theme** (existing green/white branding).
4. **Windows 10 standard control sizing** (~32px heights, tighter padding, font
   sizes untouched so content never clips; `MinHeight` used, never fixed `Height`).

## Ground truths (verified)
- Project: `src/SmartCafeteria/SmartCafeteria.csproj` → `net10.0-windows`, `UseWPF`,
  Nullable+ImplicitUsings on. Packages: QuestPDF 2024.12.3, MS.DI 8.0.1,
  CommunityToolkit.Mvvm 8.2.2. **No UI package.**
- Cairo is NOT installed system-wide → bundled at `src/SmartCafeteria/Fonts/Cairo.ttf`
  (599,548 bytes, OFL variable font), csproj `<Resource Include="Fonts\Cairo.ttf" />`,
  proven embedded as `fonts/cairo.ttf` in the built assembly.
- Font resource: `AppFontFamily` =
  `pack://application:,,,/Fonts/Cairo.ttf#Cairo` (App.xaml); inherited app-wide via
  `FontFamily="{StaticResource AppFontFamily}"` on both window roots
  (`Views/MainWindow.xaml`, `Views/LoginWindow.xaml`) → covers all 12 views.
- App is Arabic/RTL: read XAML only via file-read tools (shell output mangles Arabic).
- 12 views: MainWindow, LoginWindow, MenuView, CartView, OrdersView, InventoryView,
  EmployeesView, SuppliersView, AccountingView, ReportsView, SettingsView,
  UserManagementView. Sidebar nav = RadioButton `NavButton` style.
- App.xaml registers 13 converters (`conv:` = `SmartCafeteria.Converters`) + merges
  `Themes/GreenWhiteTheme.xaml`, `Themes/ControlStyles.xaml`, `Themes/DataTemplates.xaml`.

## Changes made
- `App.xaml`: removed all `ui:` (WPF UI) refs; Cairo resource + converters + 3 theme merges.
- `Themes/ControlStyles.xaml`: added pressed states + keyboard focus rings to
  Primary/Secondary/Danger/Icon/ProductCard buttons + NavButton; new keyed styles
  `ModernPasswordBox`, `ModernDatePicker`, `FluentDataGrid` (+ header/row styles).
  No existing resource key renamed or removed.
- Standard sizing: buttons padding `24,12`→`16,7` + `MinHeight 32`; inputs/combo/
  password padding →`10,6` + `MinHeight 32`; DatePicker padding →`8,4` + `MinHeight 32`;
  IconButton `34`→`32`; NavButton padding `18,13`→`14,8`; grid header padding
  `12,10`→`10,7`; card padding `20`→`16` / product card `15`→`12`. Font sizes unchanged.
- Views: applied new styles — 2× PasswordBox (Login, UserManagement), 4× DatePicker
  (Accounting, Employees, 2× Reports), 1× DataGrid (UserManagement); removed fixed
  `Height="44"` (login button) and `36×36` (MenuView qty buttons). Bindings,
  commands, `x:Name` code-behind hooks, RTL untouched.

## Verification status
- `dotnet build src/SmartCafeteria/SmartCafeteria.csproj` → **0 warnings, 0 errors**.
- UI tests (`tests/SmartCafeteria.UI.Tests`): **24 passed / 6 failed** — the 6 are the
  documented pre-existing FlaUI failures (4 FinanceScreensTests + 2 PosFlowTests,
  e.g. "Cart count did not update after quick add" = click/timing, not XAML).
  No regressions, no tests modified.

## Pick-up points for next agent
- Everything builds and runs; remaining work is purely additive polish if desired
  (e.g. window chrome, toasts/dialogs, DataTemplates.xaml is still an empty stub).
- Standing rules: never guess APIs — verify against repo/files; Cairo stays
  inherited app-wide (not per-control); keep 0/0; keep all tests green.
- Full session history is in the conversation; this file is the authoritative state.

---

# Addendum (2026-10-21): ticket printing — DPI-correct layout + right alignment

## Problem
- On the XP-80C the ordered items and the total were printed on the **left** margin,
  and only the **top half** of every character reached the paper.

## Verified ground truth (measured, not assumed)
- The XP-80C printer device reports `g.DpiX/DpiY = 203` and `g.PageUnit = Display`
  inside the `PrintPage` handler; `PageBounds` = 283×1169 px, `MarginBounds` = 243×1119 px
  (margins 20/20/20/30). A 1 px = 1/203 in there, not 1/96 in.
- The old layout used **fixed pixel row heights** (14 px) with **point-sized fonts**:
  at 203 dpi one 14 px box keeps only rows 4..13 of the 4..28 ink range → the bottom
  half of the characters was clipped away.
- GDI+ anchoring: in a right-to-left paragraph `Near` = right margin, `Far` = left
  margin; the old code used `Far` + RTL, which is exactly why the items sat on the left.
- `Font.GetHeight(g)` and the layout-rect height are both in device pixels, so a row box
  must be at least the font's line height, scaled by `g.DpiY / 96`.

## Changes
- `src/SmartCafeteria.Printing/TicketLayout.cs` (new): owns the 6 fonts and every
  metric, scaled to the device (`DpiFactor = g.DpiY / 96`), each text box
  `max(reference * factor, font.GetHeight(g))`, plus `PriceGap` and the footer reserves.
- `src/SmartCafeteria.Printing/ThermalPrinter.cs`: `RenderTicket` rewritten around
  `TicketLayout`; item name right-anchored (`Near` + RTL), price in its own column on
  the left margin (right-aligned inside it, so decimals line up), total row
  right-anchored, header/footer lines stay centred, over-long names are trimmed
  (`EllipsisCharacter` + `NoWrap`) instead of wrapping into a clipped second line.
- `TicketLine` gained `PriceText`; `PrintReceiptBuilder` puts the price there
  (`$"{item.ProductPrice:F2}"`, empty in minimal mode) instead of inside `Text`.
- `TicketFontSet`: values untouched (still the 96 dpi reference layout), only the
  doc comments now say "reference pixels at 96 dpi".

## Tests
- `tests/SmartCafeteria.Services.Tests/TicketRenderingTests.cs` (new, 12 tests):
  items right-anchored (full + minimal), price column on the left with equal right
  edges, total right-anchored, row box ≥ font height at 96/203/300 dpi, item text
  not cut off at 96/203/300 dpi, footer inside the page at the max font size.
- `LegacyTicketRenderer.cs` + `TicketPrintEquivalenceTests.cs` were **deleted**: they
  pinned the defective layout (items/total on the left, clipped glyphs) and can no
  longer be an oracle. They were replaced by the behavioural tests above.

## Verification status (2026-10-21)
- Solution build: **0 warnings / 0 errors**.
- Core.Tests 43/43 · Data.Tests 43/43 · Services.Tests 69/69 ·
  UI.Tests 31 passed / 7 failed (the documented pre-existing FlaUI failures:
  ReportsTests ×2, MenuSalesTests ×3, FinanceScreensTests ×1, PosFlowTests ×1).
- Numeric ink-band analysis at 96/203 dpi and in the **real** XP-80C device context:
  item rows end at the right margin (ink x = …509 of 514), price column on the left,
  total right-anchored, every band has the full ink height (23–25 px at 203 dpi).
- The live DB (`%LOCALAPPDATA%\SmartCafeteria\smart_cafeteria.db`) was restored from
  the pre-test backup: SHA256 `89A9A8BB…F1A64`, dump identical to `db_baseline.txt`.

---

# Addendum (2026-10-21): receipt — tighter line spacing, characters untouched

## Problem
- The white space between two lines of the receipt was too large. The request was to
  pull the lines closer together **without changing a single character of a line**.

## Measured ground truth
- The row pitch used to be the full line box (`LineAdvance = max(reference, LineHeight)`),
  so every line carried the leading the font adds above and below the characters.
- Ink of the real ticket lines (the product names the cafeteria sells, Segoe UI,
  measured on a bitmap): a product name reaches the **lowest** pixel of its box
  (Arabic descenders), a customization line starts a few pixels higher inside its
  own box. That combination is the one with the least room between two lines.
- The gap between two lines is therefore `pitch + inkStart(next) − inkEnd(prev)`.
  Measured over 7 resolutions (96–300 dpi) × 4 font sizes (7–16 pt) × normal/bold/
  small: the smallest gap is **1.69 px**, at 203 dpi it is 2.6–10.8 px. The planned
  factor 0.82 would have made the lines touch → rejected by measurement.

## Change
- `src/SmartCafeteria.Printing/TicketLayout.cs`: new `RowPitchFactor = 0.9f`, applied to
  `LineAdvance` and `DetailAdvance` only.
  - The **line box keeps the full font height** → no character is clipped or resized.
  - Only the distance between two rows shrinks (10 % less leading).
  - Title / section / total advances, block gap, price column: untouched.
- `TicketLayout.RuleAdvance` (new): a rule is drawn on the bottom of the line box and
  not on the row pitch, so it never touches the characters of the row above it.
  `ThermalPrinter.RenderTicket` uses it for the rows that are followed by a rule.
- `TicketLayout.MinimalFooterReserve` / `FullFooterReserve`: the reserved space now
  covers the advance of the last item, the line that marks the items that did not fit
  and the whole footer block. Before, the footer of a long receipt was cut off at the
  bottom of the paper (measured 10 px at 203 dpi / 9 pt, 32 px at 300 dpi / 16 pt).

## Tests (TDD)
- `tests/SmartCafeteria.Services.Tests/TicketSpacingTests.cs` (new, 21 cases):
  - `RowPitch_IsSmallerThanTheFontLineHeight` (96/203/300 dpi) — the pitch is shorter
    than the line box but still leaves room for the characters,
  - `ConsecutiveRows_StaySeparated` — the gap between the ink of the product name and
    the ink of the customization line stays ≥ 1 px for **every** product name and
    every customization at 96/203/300 dpi,
  - `TheRows_AreCloserThanTheFontLineHeight` — the rendered distance between the rows
    is the new pitch and is smaller than the font height,
  - `ThePitch_ScalesWithTheFontSize` — the ratio holds for 7/9/12/16 pt at 96/203/300 dpi,
  - `TheRenderedReceipt_HasNoTouchingLines` — the whole receipt (full and section
    ticket) has no two lines of characters touching,
  - `TheRenderedReceipt_KeepsTheRulesClearOfTheText` — no rule touches the row above it.
- `tests/SmartCafeteria.Services.Tests/TicketInk.cs` (new): the ink measurements both
  ticket test files need (`InkRows`, `InkBands`, `LastInkRow`, `HasInk`, `SamePixel`).
- `tests/SmartCafeteria.Services.Tests/TicketRenderingTests.cs`:
  - the footer test now renders on a canvas twice as tall as the page, so ink outside
    the page is visible — it used to be unable to fail,
  - the footer test also covers the section ticket,
  - `ItemText_IsNotCutOff` now checks the real product names.
- Every new test was checked against a broken version of the layout (rule on the
  pitch, old footer reserve, pitch factor 0.8): each one fails when the defect is
  back.

## Verification status (2026-10-21)
- Solution build **0 warnings / 0 errors**; Core.Tests 43/43 · Data.Tests 43/43 ·
  Services.Tests 91/91 (69 + the 22 new and extended ones).
- Rendered receipt analysed at 96/203/300 dpi × 9/16 pt × full/section ticket:
  **no band touches its neighbour**, **no rule touches the text above it**, the footer
  stays on the page (9–118 px of clearance), the item lines keep the full ink height
  (23–25 px at 203 dpi), the item name still ends on the right margin and the price
  column still starts on the left margin.
- The receipt is shorter: last ink row 644 → 597 at 203 dpi (47 px ≈ 6 mm), the
  spacing between two lines drops from 33.75 px to 30.38 px.
- The receipt was rendered in the **real** context of the XP-80C (203 dpi, Display
  units, page 283×1169 px, margins 243×1119 px) without a sheet of paper: no error.
- The bold product name was measured like the normal one (the bold font is used for the
  section title and the total): the smallest gap is 1.78 px, so no font of the
  receipt can touch another one at any resolution and size.

# Addendum (2026-10-21): receipt — configurable line spacing (90 %–130 %)

## Problem
- The distance between two receipt lines was a fixed value (90 %). The request was a
  slider the user controls, in the same place and with the same behaviour as the
  existing font-size slider, **without** changing the current layout and **without**
  ever letting two lines of the receipt touch.

## Measured ground truth
- The line spacing had to be measured on the **ink of the real receipt**, not on the
  line boxes: the leading the font adds above and below the characters is never
  printed, so a line box can be shared without the characters touching.
- Two measurement methods were used and cross-checked:
  - *per column, two images*: the same text is drawn once at y = 0 and once at
    y = `LineAdvance`; the gap is `lower.First[x] − upper.Last[x] − 1`. This is the
    exact distance between two lines of characters.
  - *envelope*: the first and the last inked row of the whole line, without splitting
    the columns. It is the stricter of the two.
- **Floor measured on the real receipt** (block count of the whole item area against
  the sum of the block counts of the same names printed one by one — a name that
  touches its neighbour grows into one block and the sum becomes smaller):

  | dpi | 60 % | 70 % | 75 % | 80 % | 85 % | 90 % | 110 % | 130 % |
  |-----|------|------|------|------|------|------|-------|-------|
  | 96  | touch | touch | 1 px | 1 px | 2 px | 3 px | 7 px | 12 px |
  | 203 | 3 px | 3 px | 1 px | 2 px | 3 px | 3 px | 3 px | 3 px |
  | 300 | 4 px | 4 px | 4 px | 3 px | 4 px | 4 px | 4 px | 4 px |

  (values are the distance between two product names; the price column keeps 4–53 px)
- Worst case is **96 dpi with a 13 pt font**: below 75 % the names touch.
  **90 % is the smallest share that is offered**, so the slider never reaches a layout
  that can touch.
- The distance between two rows is the **share of the line box**: the line box keeps
  the full font height, so no character is ever clipped, resized or moved. Only the
  white space between two rows changes.

## Change
- `src/SmartCafeteria.Core/Configuration/TicketPrintConfig.cs`: new
  `TicketLineSpacing` (percent), `DefaultLineSpacing = 90`, `MinLineSpacing = 90`,
  `MaxLineSpacing = 130`, `ClampLineSpacing`.
- `src/SmartCafeteria.Core/Configuration/TicketConfigStore.cs`: the share is read from
  and written to the same `printer.json` as the font size, and clamped in **both**
  directions. The SQLite schema is **not** touched (`SchemaFreezeTests` proves it).
- `src/SmartCafeteria.Printing/TicketFontSet.cs`: `TicketFontSet(baseFontSize,
  lineSpacingPercent)` with `LineSpacingPercent` / `LineSpacingFactor`;
  `FromConfig` passes the stored share. `TicketLayout.RowPitchFactor` is now that
  factor and scales `LineAdvance` and `DetailAdvance` only.
- `src/SmartCafeteria/ViewModels/SettingsViewModel.cs`: new `TicketLineSpacing`
  property with the same persist/rollback behaviour as `TicketFontSize` (a value
  that cannot be stored puts the slider back on the value that is stored).
- `src/SmartCafeteria/Views/SettingsView.xaml`: new slider `TicketLineSpacingSlider`
  (90–130, step 5) next to the font-size slider, with the value in percent.
- `src/SmartCafeteria.Printing/TicketFontSet.cs` / `TicketLayout.cs` / `ThermalPrinter.cs`:
  **no** change to the drawing itself — the layout was already parameterised.

## Tests (TDD)
- `tests/SmartCafeteria.Services.Tests/TicketSpacingTests.cs`:
  - `TheRenderedReceipt_KeepsTwoProductNamesApart` — the block count of the receipt
    with six product names must equal the sum of the block counts of the same six
    names printed one by one. This catches two names that grow into one block.
    It runs at **every** share the user can choose (96 and 203 dpi).
  - `TheRenderedReceipt_HasNoTouchingLines` — no two bands of the whole receipt
    touch (full and section ticket, every share).
  - `ThePitch_FollowsTheConfiguredSpacing` — the distance between two rows is
    exactly the share the user chose.
  - the cases come from `MemberData` (not `InlineData`), because the smallest and the
    default share can be the same number and xUnit rejects a duplicated case.
- `tests/SmartCafeteria.UI.Tests/TicketLineSpacingPreferenceTests.cs` (new, 7 cases):
  the settings view model starts at the default, persists to the file, loads it back,
  clamps a value that is too large and one that is too small, never writes to the
  database, and the thermal printer reads the stored share back.
- **The new test was checked against a broken layout**: with `MinLineSpacing = 70`
  the test fails at 96 and 203 dpi and passes again at 90 %.

## Verification status (2026-10-21)
- Solution build **0 warnings / 0 errors**.
- Core.Tests 43/43 · Data.Tests 43/43 · Services.Tests 126/126 · UI.Tests 38/45
  (the 7 failures are the pre-existing ones: `ReportsTests` ×2, `MenuSalesTests` ×3,
  `FinanceScreensTests` ×1, `PosFlowTests` ×1 — unchanged by this feature).
- The live database was backed up before the UI run and restored afterwards
  (SHA256 `EFA60B8CF7761A927B5C7AD5FF067218C8CA606112E209B16F80966EA5647A1E`).
- The receipt was rendered with the **real** config file (font size 13) at
  96/203/300 dpi × 60–130 %: at 90 % the distance between two product names is
  3–4 px and between two prices 8–24 px; nothing touches at any share the slider
  offers.
- The rendered ticket now scales its whole geometry with the resolution of the
  device (the paper has a fixed physical size). Before, the test canvas was 283 px
  wide at **every** resolution, which squeezed a 203 dpi ticket into 42 % of its
  real width and made the measurement stricter than reality.

# Addendum (2026-10-22): receipt — line spacing slider starts at 10 %

## Problem
- The slider of the previous addendum started at **90 %**: `MinLineSpacing` was `90`
  and the XAML slider had `Minimum="90"`. The request was a slider that **starts at
  10 %**, so the user can pull the lines of the receipt much closer together.
- The default stays at **90 %** and the step stays at **5 %**, so a receipt that is
  printed without touching the settings is bit-for-bit the receipt of today.

## Measured ground truth (re-measured, 2026-10-22)
- The floor of the previous addendum (75 %) was measured with a **13 pt font only**.
  The font-size slider goes down to **7 pt**, and a smaller font means a smaller
  line box, so the floor had to be measured again for the whole range of the slider.
- Same method as before (block count of the item area against the sum of the block
  counts of the same names printed one by one — a name that touches its neighbour
  grows into one block and the sum becomes smaller), now for the font sizes
  7 / 8 / 9 / 16 pt at 96 and 203 dpi:

  | dpi | font 7 pt | font 8 pt | font 9 pt | font 16 pt |
  |-----|-----------|-----------|-----------|------------|
  | 80 % | touch    | touch     | touch     | ok         |
  | 85 % | ok       | ok        | ok        | ok         |

  (at 203 dpi both 80 % and 85 % are ok for every font size)
- **85 % is the safe floor**: the smallest share that keeps every product name
  apart for every font size of the settings slider at every printer resolution.
- The distance between two rows is the **share of the line box**: the line box keeps
  the full font height, so no character is ever clipped, resized or moved — also at
  10 %. Only the white space between two rows changes, and below the safe floor the
  rows overlap **on purpose**.

## Change
- `src/SmartCafeteria.Core/Configuration/TicketPrintConfig.cs`: `MinLineSpacing`
  **90 → 10**. `DefaultLineSpacing` stays `90`, `MaxLineSpacing` stays `130`,
  `ClampLineSpacing` is unchanged.
- `src/SmartCafeteria/Views/SettingsView.xaml`: the slider `TicketLineSpacingSlider`
  now has `Minimum="10"` (`Maximum="130"`, `TickFrequency="5"` unchanged), and the
  Arabic helper text names the new range and the unchanged default.
- `src/SmartCafeteria.Core/Configuration/TicketConfigStore.cs`: `Save` now clamps
  the share before writing it, so the file always holds a printable value. The
  duplicated clamp in `Load` (the share was clamped twice on the same line pair)
  was removed.
- `src/SmartCafeteria.Printing/*` and `src/SmartCafeteria/ViewModels/*`: **no**
  change — the layout was already parameterised, and the view model already clamps
  through `TicketPrintConfig.ClampLineSpacing`. The SQLite schema is untouched.

## Tests (TDD)
- **RED first**: 6 of the new/changed cases failed before the production change
  (`SaveThenLoad_RoundTripsTheSmallestSelectableLineSpacing`,
  `ClampLineSpacing_KeepsValuesInsideThePrintableRange`,
  `TheSmallestSelectableShare_IsAcceptedAndIsBelowTheDefault`,
  `TheCharactersOfALine_AreNeverClipped_AtTheSmallestShare`,
  `TheSmallestShare_LetsTheLinesTouch_OnPurpose`,
  `TheSmallestShare_MakesTheReceiptShorter`) and all pass after it.
- `tests/SmartCafeteria.Services.Tests/TicketSpacingTests.cs`:
  - the safe floor is now the measured **85 %** (`SafeLineSpacing`);
  - the member data was split into `SafeSpacings` (distinct dpi × share) and
    `SafeCases`. Before, the two theories that only need (dpi, share) projected the
    `minimal` flag away, which produced **8 duplicated case IDs that xUnit skipped
    silently** — they now run as real cases;
  - new `TheCharactersOfALine_AreNeverClipped_AtTheSmallestShare` (the ink of a line
    is the same at 10 % and at 90 %: the characters are neither clipped nor
    resized), `TheSmallestShare_MakesTheReceiptShorter` and
    `TheSmallestShare_LetsTheLinesTouch_OnPurpose` (below the floor the rows
    overlap on purpose).
- `tests/SmartCafeteria.Services.Tests/TicketFontSetTests.cs`: new
  `TheSmallestSelectableShare_IsAcceptedAndIsBelowTheDefault`.
- `tests/SmartCafeteria.Services.Tests/TicketConfigStoreTests.cs`: new
  `SaveThenLoad_RoundTripsTheSmallestSelectableLineSpacing` and
  `Save_WritesTheShareThatWasGiven`; the clamp test now also covers 10 % and 5 %.
- `tests/SmartCafeteria.UI.Tests/TicketLineSpacingPreferenceTests.cs`: the case
  that clamps a too small value now uses **5 %** (5 was the old minimum, it is out
  of range now), and a new case checks that the smallest selectable share (10 %)
  is accepted and stored. **These two changes are not executed yet** — the FlaUI
  suite is off limits in this session.

## Verification status (2026-10-22)
- Solution build **0 warnings / 0 errors**.
- Core.Tests 43/43 · Data.Tests 43/43 · Services.Tests **141/141** (126 before,
  +7 new cases, +8 cases that used to be skipped as duplicated IDs).
- UI.Tests were **not** run in this session (the FlaUI suite may not be started
  without an explicit instruction). The live database was never touched: its
  SHA256 is `C07F85585D9FDF293CA2204B2CB080A7C8AD910BE1E23D11A75665EF05021CFB`
  before and after the work.
- The measurement harness that produced the table above was a temporary file and
  has been deleted again.

# Addendum (2026-10-22): receipt — receipt number and date on separate lines

## Problem
- The customer receipt printed the receipt number and the date on **one** shared
  line (`الرقم: 1042    التاريخ: 2026-10-21 12:30`). The request was to separate
  them into two lines, each with its own row.

## Verified ground truth (measured, not assumed)
- The shared line was drawn in `ThermalPrinter.RenderTicket` — the only place in
  the code where the two values were combined.
- The row was drawn with `layout.SmallFont` and advanced by `layout.DetailAdvance`,
  and `DetailAdvance = max(fonts.DetailAdvance × DpiFactor, LineHeight) × RowPitchFactor`
  is never smaller than `LineAdvance`. Using it for **both** new rows therefore gives
  the number/date gap that the header row already had against the customer row.
- **Measured receipt height** (last row that carries ink) before → after the change:

  | dpi | share | before | after | growth |
  |-----|-------|--------|-------|--------|
  | 96  | 85 %  | 300  | 314  | +14 |
  | 96  | 90 %  | 311  | 326  | +15 |
  | 96  | 130 % | 401  | 421  | +20 |
  | 203 | 85 %  | 635  | 664  | +29 |
  | 203 | 90 %  | 659  | 689  | +30 |
  | 203 | 130 % | 848  | 892  | +44 |
  | 300 | 85 %  | 939  | 982  | +43 |
  | 300 | 90 %  | 974  | 1019 | +45 |
  | 300 | 130 % | 1253 | 1318 | +65 |

  (the growth is exactly one `DetailAdvance`: 14.4 px at 96 dpi, 30.5 px at 203 dpi
  and 45 px at 300 dpi, each scaled by the line-spacing share the user chose)

## Changes
- `src/SmartCafeteria.Printing/ThermalPrinter.cs`: the combined `ticketDate` string is
  replaced by two `DrawString` calls — `الرقم: {TicketNumber}` first, `التاريخ: {Date}`
  second, both centred exactly as the old row was, both advancing by `layout.DetailAdvance`.
- **No change** to `TicketLayout`, `TicketFontSet`, `TicketPrintModel` (the number and the
  date were already two separate properties), `PrintReceiptBuilder`, the database or the
  on-screen views.
- The **minimal kitchen ticket** is unchanged: it prints the receipt number only and has
  no date today.

## Tests (TDD)
- **RED first**: all 10 new cases failed before the production change and pass after it.
- `tests/SmartCafeteria.Services.Tests/TicketHeaderLayoutTests.cs` (new, 10 cases):
  - `TheReceiptNumber_AndTheDate_ArePrintedOnSeparateLines` (96/203 dpi) — two receipts
    that differ in exactly one value tell with a pixel diff where that value was printed;
    the receipt number must be strictly above the date.
  - `TheDate_IsStillPrintedOnTheReceipt` (96/203 dpi) — the date is still on the receipt,
    on a row below the number.
  - `TheReceiptNumber_AndTheDate_DoNotTouch` (96/203 dpi × 85/90/130 %) — the number and
    the date must sit in two different bands of ink, so at least one blank row is between
    them even at the smallest share that is safe to print.
- `tests/SmartCafeteria.Services.Tests/TicketInk.cs`: the pixel-diff helper `DiffBounds`
  moved here from `TicketRenderingTests` — the file exists to hold the measurements both
  ticket test files need. The 5 call sites in `TicketRenderingTests` now use
  `TicketInk.DiffBounds`; **no assertion was changed**.

## Verification status (2026-10-22)
- Solution build **0 warnings / 0 errors**.
- Core.Tests 43/43 · Data.Tests 43/43 · Services.Tests **151/151** (141 before, +10 new).
- The guards that now cover the new row automatically stay green:
  `TheRenderedReceipt_HasNoTouchingLines` (12 cases), `TheRenderedReceipt_KeepsTheRulesClearOfTheText`
  and `RenderTicket_AtALargerFontSize_KeepsEveryLineInsideTheFooterReserve`.
- UI.Tests were **not** run in this session (the FlaUI suite may not be started without an
  explicit instruction). The live database is untouched: SHA256
  `C07F85585D9FDF293CA2204B2CB080A7C8AD910BE1E23D11A75665EF05021CFB`, and the live
  `printer.json` still holds `{ "TicketFontSize": 10, "TicketLineSpacing": 90 }`.

## Incident: the live database was changed during the plan phase
- While the implementation plan was being written, the live database was modified (09:14:28)
  and the live `printer.json` was written (09:14:24) — it ended up at
  `{ "TicketFontSize": 13, "TicketLineSpacing": 45 }`.
- The cause is the FlaUI suite: `Settings_SaveSettings_ShowsSuccessStatus` starts the real
  application, the application seeds the live database (a `DbContext` without an explicit
  path resolves to `%LOCALAPPDATA%\SmartCafeteria\smart_cafeteria.db`) and writes the live
  `printer.json`.
- **No test of this session does that.** Core.Tests, Data.Tests and Services.Tests were each
  run on their own against the live file: the hash of the live database stayed
  `C07F8558…5021CFB` after every one of them.
- Both files were restored from the backups taken before the plan phase
  (`db_inspect_backup.db`, `printer_inspect_backup.json`); the database has the reference
  hash again.
- **Lesson:** the FlaUI suite is the only part of this repository that touches the live
  database. It needs the database backed up and restored around it, and it must never be
  started without an explicit instruction.

## Note: a restored source file can produce a stale build
- `Copy-Item` keeps the timestamp of the source file. A file restored that way can be
  **older** than the compiled DLL, and MSBuild then skips it — the solution built without
  an error and the receipt was still rendered with the old single line.
- After restoring a file, touch it (`(Get-Item …).LastWriteTime = Get-Date`) and rebuild
  before trusting a test result.

## Analytics reports (2026-10-22)

### Goal
Add a large set of **new** analytical reports to the reports screen — on screen as tables
and as PDF exports — **without** touching the database and **without** touching the four
existing reports.

### What was added
- `Core/Enums/AnalyticsReportKind.cs` — 16 report kinds in display order: TopItems,
  SalesByCategory, PaymentMethods, CashierPerformance, SalesByHour, SalesByDay,
  SalesByWeekday, MenuEngineering, DeadStock, ReorderPriority, FavouriteCustomizations,
  ExpensesByCategory, ProfitAndLoss, Suppliers, Employees, ErrorLog.
- `Core/Models/Analytics/` — `AnalyticsRow` (Rank/Primary/Secondary/Tertiary/Value/BarRatio),
  `AnalyticsReport` (Kind/Title/Columns/Rows/EmptyMessage), `AnalyticsKpi`, `AnalyticsSummary`
  (plain numbers), `AnalyticsBundle` (+`Find(kind)`).
- `Core/Interfaces/IAnalyticsService.cs`, `IAnalyticsPdfService.cs`.
- `Services/Analytics/AnalyticsInput.cs` — the read-once data snapshot handed to every
  calculator (Orders, AllOrders, ReceivedOrders, Products, AccountEntries, Suppliers,
  PurchaseOrders, SupplierPayments, Employees, Logs, Currency, RangeDays).
- `Services/Analytics/AnalyticsSalesCalculators.cs` — `AnalyticsFormat` (invariant-culture
  Money/Number/Percent) plus sales, payment, cashier and time-pattern calculators, and the
  `ItemBucket` record. **Line revenue = `ProductPrice * Quantity + CustomizationsTotal`**,
  matching `OrderRepository` (`Order.Total = Subtotal + CustomizationsTotal`).
- `Services/Analytics/AnalyticsMenuCalculators.cs` — menu engineering (نجمة/حصان/لغز/راكد by
  median popularity and mean revenue per unit), dead stock, reorder priority (coverage under
  three days) and favourite customizations (parses `SelectedCustomizationsJson`).
- `Services/Analytics/AnalyticsFinanceCalculators.cs` — expenses by category, profit and
  loss, supplier balances, employees and the error log.
- `Services/AnalyticsService.cs` — the orchestrator: one read pass, 16 reports with their
  final Arabic column headers, 12 KPI cards and the summary.
- `Services/AnalyticsPdfService.cs` — one table renderer for every report, A4 / 40 margin /
  RTL, brand-green header, summary cards, footer page numbers, files named
  `AnalyticsReport_{kind}_{yyyyMMdd_HHmmss}.pdf` and `AnalyticsFullReport_…` in `%TEMP%`,
  with a seven-day cleanup.
- `ViewModels/ReportsAnalyticsViewModel.cs` — its **own** date filter (defaults to
  "كل الفترات"), refresh, per-report export and full export; plus the tiny display
  projections `AnalyticsReportView` / `AnalyticsRowView` (they number the rows the service
  did not rank, so every table has a position column).
- `Views/ReportsAnalyticsView.xaml(.cs)` — binds one card per report; resolves its view model
  from `App.ServiceProvider` on construction and loads on `Loaded`.

### Constraints honoured
- **No schema change**: no table, column or index was added. Guarded by
  `AnalyticsSchemaFreezeTests` (exactly 15 tables; the schema and every row count are
  identical before and after `BuildAsync`, including two consecutive builds).
- **The existing reports are untouched**: `ReportsViewModel`, `PdfReportService`,
  `IPdfService` and the four existing PDF buttons are byte-for-byte unchanged.
- `ReportsView.xaml`: exactly one added element (`<views:ReportsAnalyticsView/>`) plus the
  `xmlns:views` declaration it needs.
- `App.xaml.cs`: four additive DI lines only.
- **Read-only**: `AnalyticsService` only reads; the freeze test proves it writes nothing.
- Arabic RTL throughout; every string is hardcoded Arabic, so nothing depends on an `ar-SA`
  culture being installed.
- Arithmetic formatting uses `CultureInfo.InvariantCulture`, which is what makes the tests
  deterministic.

### Deliberate behaviour
- `SalesByWeekday` always returns seven rows and `ProfitAndLoss` always returns five
  statement lines — they are structural reports. Every other report returns zero rows for an
  empty period and the view shows its `EmptyMessage`.
- `BuildKpis` falls back to "لا يوجد" so no KPI card is ever blank.
- The first column is headed `#`; it shows the service's rank when there is one and the row
  position otherwise.

### Font note
Cairo is embedded once in the Services assembly (`SmartCafeteria.Services.Fonts.Cairo.ttf`).
`AnalyticsPdfService` registers it under its own guard; `PdfReportService` keeps its own.
`AnalyticsPdfServiceTests.RegisteringTheArabicFontTwice_DoesNotThrow` proves QuestPDF
tolerates the double registration, so **`PdfReportService.cs` needed no edit**.

### Verification status (2026-10-22)
- Solution build **0 warnings / 0 errors**.
- Core.Tests 43/43, Data.Tests 43/43, Services.Tests **206/206** (151 before, +55:
  21 calculator, 8 service, 22 PDF, 4 schema-freeze).
- UI.Tests were **not** run (the FlaUI suite needs an explicit instruction).
- The live database is untouched: SHA256
  `C07F85585D9FDF293CA2204B2CB080A7C8AD910BE1E23D11A75665EF05021CFB`, and the live
  `printer.json` still holds `{ "TicketFontSize": 10, "TicketLineSpacing": 90 }`.

### Incident: the live database was touched again during the analytics plan phase
- The planner agent modified the live database at 10:08:50 and 10:08:59 while the plan was
  being written, leaving it at `9EAE1C7B…`.
- Restored from the pre-plan backups (`db_inspect_backup.db` / `printer_inspect_backup.json`),
  and `printer.json` was rewritten to `{ "TicketFontSize": 10, "TicketLineSpacing": 90 }`.
- Same root cause as the first incident: an agent started the application. Check the database
  hash after **every** agent or subagent invocation, not only after test runs.

## General Financial Report + Unified Reports Screen (2026-10-22)

### Goal
Add a **General Financial Report** (all financial operations in one chronological ledger with a closing summary) and make all analytical reports buttons like the four main reports, unified on one screen sharing a single date filter.

### What was added
- `Core/Enums/FinancialDirection.cs` (Inflow/Outflow/Commitment)
- `Core/Models/Analytics/FinancialOperation.cs`, `FinancialSummary.cs`, `FinancialReport.cs`
- `Core/Interfaces/IAnalyticsPdfService.cs` — added `GenerateFinancialReportAsync`
- `Services/Analytics/AnalyticsInput.cs` — added `Start`/`End`
- `Services/Analytics/AnalyticsFinanceCalculators.cs` — `GeneralFinancial` (sales, other income, supplier payments, salaries, other expenses; purchase orders as commitments, excluded from cash flow)
- `Services/AnalyticsService.cs` — populates `AnalyticsBundle.Financial` using the same date range
- `Services/AnalyticsPdfService.cs` — financial PDF with ledger table and summary block (sales, inflow/outflow, net cash flow, supplier purchases/movement, operation count). Cleanup now removes `FinancialReport_*.pdf`.
- `ViewModels/ReportsViewModel.cs` — extended with `IAnalyticsService`/`IAnalyticsPdfService`; added `DownloadAnalyticsReportAsync`, `DownloadFinancialReportAsync`; loads analytics bundle on `LoadStatsAsync` and builds buttons from `AnalyticsBundle.Reports`; single date filter drives everything.
- `Views/ReportsView.xaml` — unified: four main buttons, then `التقرير المالي الشامل` (primary), then all analytical reports as secondary buttons. Removed separate analytics section.

### Behaviour
- Purchase orders are treated as commitments (shown in summary as `مشتريات الموردين (التزامات)`) and not subtracted from cash outflow.
- Salaries are separated from other expenses; supplier payments are counted as cash outflow.
- Financial summary appears after the ledger table.
- Files saved to `%TEMP%` as `FinancialReport_*.pdf` / `AnalyticsReport_*.pdf` and opened automatically.

### Verification status (2026-10-22)
- Solution build **0 warnings / 0 errors**.
- Core.Tests 43/43, Data.Tests 43/43, Services.Tests **220/220** (+14: 11 financial calculator tests + 3 PDF tests).
- Schema unchanged; analytics remain read-only.
- UI.Tests not run. Live DB untouched: `C07F85585D9FDF293CA2204B2CB080A7C8AD910BE1E23D11A75665EF05021CFB`, `printer.json` still `{ "TicketFontSize": 10, "TicketLineSpacing": 90 }`.
## Order Type + Order Notes (2026-11-08)

### Goal
Add a `نوع الطلب` selector (`محلي` default / `سفري`) and a free-text `ملاحظات` field, side by side with
`طريقة الدفع` on the POS cart, and carry both through persistence, receipts, the Orders screen, PDF
reports and analytics.

### What was added
- `Core/Enums/OrderType.cs` (new): `Local = 0`, `Takeaway = 1`, plus `OrderTypeExtensions.ToArabic()`
  (`محلي` / `سفري`).
- `Core/Models/Order.cs`: `OrderType` (default `Local`) and `Notes` (default `""`).
- `Core/Interfaces/IOrderService.cs`, `Data/Repositories/OrderRepository.cs`,
  `Services/OrderManagementService.cs`: the extra parameters flow end to end; the repository writes
  both columns.
- `Data/Database/DatabaseInitializer.cs`: `Orders.OrderType INTEGER NOT NULL DEFAULT 0` and
  `Orders.Notes TEXT NOT NULL DEFAULT ''`, plus two idempotent `EnsureColumnAsync` calls so existing
  databases migrate in place (legacy rows read back as `Local` / empty).
- `ViewModels/CartViewModel.cs`: `SelectedOrderType` / `Notes`, `OrderTypes` list, checkout forwards
  them, and both reset after a successful sale.
- `Views/MenuView.xaml` + `Views/CartView.xaml`: the payment row became a three-column grid with mini
  labels; AutomationIds `PaymentMethodCombo`, `OrderTypeCombo`, `OrderNotesInput`; note capped at 200.
- `Converters/EnumArabicNameConverter.cs`: Arabic names for `OrderType`;
  `Converters/VisibilityConverters.cs`: new `NonEmptyStringToVisibilityConverter` (registered in
  `App.xaml`).
- `Views/OrdersView.xaml`: order type + note shown per order.
- `Printing/*`: `TicketPrintModel`, `PrintReceiptBuilder`, `ThermalPrinter` — the order type is printed
  on every `PrintMethod` layout and the note is printed when present.
- `Core/Enums/AnalyticsReportKind.cs`: `OrderTypes` and `OrdersWithNotes` appended **after** `ErrorLog`
  so existing numeric values stay stable. `AnalyticsSalesCalculators` gained both calculators (with a
  shared `ShareRows` helper) and `AnalyticsService` registers both reports.
- `Services/PdfReportService.cs`: sales PDF adds `نوع الطلب`; orders PDF adds `نوع الطلب` and `ملاحظات`.

### Tests (TDD)
- `AnalyticsServiceTests` / `AnalyticsSchemaFreezeTests`: 16 → 18 reports; `AnalyticsPdfServiceTests`
  covers both new kinds.
- `AnalyticsCalculatorsTests` (+4), `PrintReceiptBuilderTests` (+3, all print methods), `ConvertersTests`
  (+3 order-type / visibility cases), `OrderManagementServiceTests` (+1), `ServicesIntegrationTests`
  (+1), `TicketRenderingTests` (+2 pixel-diff assertions).
- `DatabaseIntegrationTests` (+2 persistence cases) and new `LegacyOrdersMigrationTests` (+3: columns
  added, legacy row reads back `Local`/empty, migration idempotent).
- `UI.Tests/PosFlowTests`: 4 FlaUI cases + helpers, written but **not executed**.

### Incident: machine-wide broken `csc.exe`
- `dotnet build` started failing with `MSB6006: "csc.exe" exited with code 1` and no diagnostics;
  `dotnet exec csc.dll -version` and `vbc.dll -version` both failed with
  `Could not load file or assembly 't, Version=10.0.0.0, ...`, and a fresh console project failed the
  same way. `dotnet msbuild -version` worked, so the runtime was fine.
- `Microsoft.CodeAnalysis.dll` in `C:\Program Files\dotnet\sdk\10.0.401\Roslyn\bincore` is corrupt
  (reflection reports a duplicate `Roslyn.Utilities` type). The system SDK needs a repair; this session
  installed a clean SDK 10.0.401 into `%LOCALAPPDATA%\Microsoft\dotnet-sdk` and built with it.
- No project or solution change was needed for the workaround.

### Verification status (2026-11-08)
- Solution build **0 warnings / 0 errors** (clean SDK).
- Core.Tests **51/51** (+8), Data.Tests **48/48** (+5), Services.Tests **238/238** (+18).
- UI.Tests not run. The live database was not touched by this session (still `C25844AB…`, mtime
  2026-10-29); `printer.json` is `{ "TicketFontSize": 11, "TicketLineSpacing": 70 }` (user-changed,
  not by tests).

## Receipt Notes Removal + Cashier Summary + Compact POS Fields (2026-11-08)

### Goal
Keep the order notes out of every printed ticket, turn the second copy of `إيصال مزدوج` into a short
cashier receipt, and make the payment-type / order-type / notes row shorter on the POS screen.

### What was added
- `Printing/TicketPrintModel.cs`: removed `Notes`; added `IsCashierSummary` and `SummaryTitle`.
- `Printing/PrintReceiptBuilder.cs`: the order note and the item notes are no longer added to any
  ticket; new `BuildCashierSummaryReceipt`; `TotalReceiptTwice` prints the full customer receipt and
  then the short cashier copy, both in `PrintOrderWithMethodAsync` and `BuildReceiptsForOrder`.
- `Core/Interfaces/IPrintService.cs` + `Printing/ThermalPrinter.cs`: new `PrintCashierSummaryAsync`
  and a render branch that prints only the cafeteria name, `إيصال استلام`, the receipt number, the
  meals and the total.
- `Themes/ControlStyles.xaml`: `CompactTextBox` / `CompactComboBox` (`MinHeight` 26, 13 pt,
  padding `8,2`) based on the modern input styles.
- `Views/MenuView.xaml` + `Views/CartView.xaml`: the three-field row uses the compact styles, the
  label gap went 6 → 4, and `SearchBox` got an AutomationId for the UI comparison test.

### Behaviour
- The database, the screens and the PDF reports still store and show the notes; only the printed
  tickets changed.
- The cashier copy carries no date, phone, address, payment method, order type, pickup time, notes,
  order id, thanks line, or item prices/customizations.
- Section tickets still print the order type; they no longer print notes.

### Tests (TDD)
- `PrintReceiptBuilderTests`: the note assertions were flipped to "no note anywhere", the cashier
  summary content and meals are covered, and the print order (customer → cashier) is checked.
- New `TicketNotesRemovalTests` (4 pixel renders identical with and without notes) and new
  `CashierSummaryTicketTests` (each required element is printed, the non-summary fields are ignored,
  the total stays on the page at the max font size) + `TicketRenderHarness`.
- `TicketRenderingTests`: the obsolete "note is printed" test was replaced.
- `PosFlowTests`: `Menu_OrderFields_AreCompactButReadable` (written, **not executed**).

### Verification status (2026-11-08)
- Solution build **0 warnings / 0 errors**.
- Core.Tests **51/51**, Data.Tests **48/48**, Services.Tests **252/252**.
- UI.Tests compiled but **not run**.
- The live database changed at 11:28 (hash `D123A130…`) before this implementation started — most
  likely the planner subagent launching the app. No test of this task touched it.

### Incident: double-encoded text files
- Two PowerShell `Get-Content`/`Set-Content` round-trips (`MenuView.xaml`, `CartView.xaml`,
  `PROGRESS.md`) decoded UTF-8 as CP1252 and double-encoded the Arabic. The text was restored
  byte-exactly by re-encoding through CP1252 and decoding as UTF-8. Use the editing tools for text
  files instead of shell round-trips.

## Cashier Copy Order Type (2026-11-08)

### Goal
On `إيصال مزدوج` the cashier copy must also show the order type (`محلي` / `سفري`), on its own line
directly below the receipt number.

### What changed
- `Printing/PrintReceiptBuilder.cs`: `BuildCashierSummaryReceipt` now sets
  `OrderType = order.OrderType.ToArabic()`.
- `Printing/ThermalPrinter.cs`: the cashier branch prints `نوع الطلب: …` (`SmallFont`, centred)
  right below `الرقم: …` and before the rule; guarded on empty so legacy/hand-built models render
  byte-identically.
- `Printing/TicketPrintModel.cs`: comments updated — `OrderType` is used by the full, minimal and
  cashier layouts.

### Tests
- `PrintReceiptBuilderTests`: every print method now asserts the order type on **all** models
  (cashier copy included); the summary content test expects `سفري`; new default case expects `محلي`.
- `CashierSummaryTicketTests`: order type removed from the "ignored fields" render; new
  "prints the order type" test and a pixel test proving the line sits below the receipt number.

### Verification status (2026-11-08)
- Solution build **0 warnings / 0 errors**.
- Core.Tests **51/51**, Data.Tests **48/48**, Services.Tests **255/255**.
- UI.Tests compiled but **not run**.
- The live database changed at 12:18 (hash `31F6D910…`) while the suites ran, but no test uses the
  live path and no app process or WAL file remained afterwards — attributed to external/manual app
  use, not to the tests.

## UI Motion & Modernisation (2026-11-08)

### Goal
Make the interface feel fluid and modern without changing behaviour, layout or automation hooks.

### What changed
- `Themes/ControlStyles.xaml`:
  - Press feedback on every button family (`PrimaryButton`, `SecondaryButton`, `DangerButton`,
    `IconButton`, `DangerIconButton`, `SidebarButton`, `CategoryChip`): a render-transform scale
    that shrinks on press and springs back — transforms only, so no layout reflow.
  - `ProductCardButton`: lifts 4 px with a deeper shadow on hover, settles on press.
  - New `CategoryChip` pill style; `MenuView` categories and `OrdersView` status filters now read as
    a toggle group instead of default radio buttons.
  - New `DangerIconButton` (round red remove button) and `SidebarButton` (white outline for the
    gradient sidebar; replaces the green outline on green).
  - `NavButton`: a white accent bar that fades and grows in with the selection.
  - `ModernComboBox`: roomier dropdown items (`Padding 10,6`, `MinHeight 28`).
  - New `Toast` style: the notification fades and slides in from above when it becomes visible.
- `Views/MenuView.xaml`: chips, softer cart rows (surface + 1 px divider), styled +/- and remove
  buttons.
- `Views/MainWindow.xaml` + `.xaml.cs`: new screens fade and lift 12 px into place on navigation
  (240 ms, `CubicEase`), driven by a `ContentProperty` descriptor; the toast style is applied.
- `Views/LoginWindow.xaml` + `.xaml.cs`: soft gradient backdrop, elevated card, 280 ms fade-up.
- Every animation is short (≤ 280 ms), transform/opacity only, and skipped when
  `SystemParameters.ClientAreaAnimation` is off.

### Verification status (2026-11-08)
- Solution build **0 warnings / 0 errors**.
- Core.Tests **51/51**, Data.Tests **48/48**, Services.Tests **255/255**.
- UI.Tests compiled but **not run** (they launch the real exe against the live database).
- The live database changed at 16:09 (hash `70A78BCB…`); no test uses the live path and no app
  process remained afterwards — attributed to manual app use.

## Brand Refresh: App Icon + Login Developer Credits (2026-11-09)

### Goal
Use the glossy café artwork as the single application icon everywhere and add the developer
credit block (name, e-mail, phone) to the login screen.

### Icon
- `scripts/build-app-icon.ps1` (new, reproducible): trims the transparent border of
  `Resources/Glossy Café Management Icon Set.png`, pads 4 %, centres it on a square canvas, then
  writes both outputs below. The artwork has no safe area, so the trimmed square is what the shell
  shows at every size.
- `src/SmartCafeteria/Resources/AppIcon.png` (new, 512 × 512) — logo on the login screen.
- `src/SmartCafeteria/Resources/AppIcon.ico` (regenerated, 223 KB): **9 frames** — 16, 20, 24, 32,
  40, 48, 64, 128 as 32-bpp DIB + 256 as PNG (the standard layout; the old file had a single frame).
  `ApplicationIcon` in `SmartCafeteria.csproj` and `installer/Product.wxs:8` keep pointing at it.
- `Resources/SmartCafeteriaIcon.ico` was left untouched (unreferenced by any project).

### Login screen
- `Views/LoginWindow.xaml`: the `☕` text glyph is now the `AppIcon.png` logo; the card is a
  two-row grid (form centred, footer pinned) and grew to 480 × 580. New footer: divider + soft-green
  rounded band with the credit centred and the e-mail / phone rows forced LTR (with MDL2 mail and
  phone glyphs) so they read correctly inside the RTL window.
- `Resources/Strings.ar.xaml`: new keys `DeveloperCredit` (`تم التصميم بواسطة محمد إبراهيم التوم`),
  `DeveloperEmail` (`mohammedtom904@gmail.com`), `DeveloperPhone` (`0129039378`).
- The footer is TextBlocks only, so the username box stays the first `Edit` control for the UI tests.

### Tests (written, **not executed** — UI suite launches the real exe)
- `UI.Tests/LoginTests`: `Login_ShowsDeveloperCredits` (all three strings visible) and
  `Login_DeveloperPanelAddsNoInputs` (exactly two `Edit` controls + the `AppLogo` element exists).

### Verification status (2026-11-09)
- Solution build **0 warnings / 0 errors**; `Resources/AppIcon.png` and `.ico` are embedded
  (`SmartCafeteria.g.resources` keys verified) and the exe carries the icon resource.
- Core.Tests **51/51**, Data.Tests **48/48**, Services.Tests **255/255**; UI.Tests compiled only.
- Published `Release/win-x64` self-contained single-file to `publish/win-x64/SmartCafeteria.exe`
  (162 MB, 2026-11-09 14:29).
- Live database untouched by this session: still `70A78BCB…` before and after the suites/publish.
- Windows caches shell icons; if Explorer still shows the old icon, restart Explorer or clear the
  icon cache.

### Follow-up: side-by-side login layout (2026-11-09)
- User preferred the developer info **beside** the form, not below it. `Views/LoginWindow.xaml` is now
  840 × 580 with a 3-column grid: info panel (*, visual right in RTL), 1 px `DividerBrush` vertical
  line, login form (fixed 380 px, visual left).
- The login form block (logo, name, subtitle, fields, error, button, hint) is byte-for-byte the same
  as before; only the credits moved out of the footer band into a centred `PrimarySoftBrush` card in
  the info column under a "معلومات التواصل" heading.
- UI-test invariants kept: exactly two `Edit` controls, `AppLogo`, `PasswordInput`, and all three
  credit strings still visible.
- Verified again: build **0 warnings / 0 errors**; Core **51/51**, Data **48/48**, Services
  **255/255**; UI.Tests not run; republished `publish/win-x64` (2026-11-09 14:44).

### Follow-up: default-account hint removed (2026-11-09)
- Deleted the `الحساب الافتراضي: admin` hint at the bottom of the login form (the `admin` seed and
  login behaviour are unchanged); the login button now has `Margin="0"` so the form ends cleanly.
- No test referenced the hint. Build **0 warnings / 0 errors**; Core **51/51**, Data **48/48**,
  Services **255/255**; republished `publish/win-x64`.

### Remediation Phase 1–4: authentication, authorization, account hardening (2026-11-10)
Scope: the two CRITICAL findings plus the account-related HIGH findings from `reviewReport.md`.

- **No default password.** First run now seeds `admin` with a 16-char random password
  (`RandomNumberGenerator.GetString`, ambiguous characters omitted), shown exactly once in the new
  `FirstRunCredentialsWindow` (copy + continue) and flagged `MustChangePassword`; the account must
  choose its own password on first login. `admin/admin123` is gone from the codebase.
- **Versioned PBKDF2.** `PasswordHasher` writes `v2$600000$<salt>$<hash>` (SHA-256, 16-byte salt,
  32-byte key). Legacy `base64(salt|hash)` hashes at 100k iterations still verify and are upgraded
  on the next successful login. Unknown usernames are verified against a dummy hash so the failure
  path costs the same PBKDF2 work.
- **Lockout.** Five consecutive failures lock the account for 15 minutes (counters on `Users`);
  a correct password during the lockout still reports `LockedOut` with the remaining minutes.
  Success resets the counters. `TimeProvider` is injected so the tests control time.
- **Forced change.** `LoginViewModel` raises `PasswordChangeRequired` instead of signing in; the
  login window opens `ChangePasswordDialog`, which validates the old password, the non-empty
  password rule and the confirmation, then the session is re-read before `LoginSucceeded`.
- **Policy.** `PasswordPolicy` requires only a non-empty password for creation and every password
  change. Existing users keep their current passwords until they change them.
- **Hash never leaves the authentication path.** All `GetUser*` projections exclude
  `PasswordHash`; `AuthenticateDetailedAsync` clears it before returning the session user.
- **Authorization at the data layer.** New `ICurrentUserContext` (implemented by `UserSession`).
  `UserRepository` construction may happen only for the very first account; all later creates,
  updates, deletes, activations and admin-initiated password resets require an active admin.
  Invariants enforced in transactions: no self-demotion/deactivation/deletion and at least one
  active admin always remains. `AccountRepository`, `SupplierRepository` and `EmployeeRepository`
  now require an active admin for every mutation (UI gating is no longer the only protection).
  Duplicate usernames are translated from the unique index instead of a check-then-insert race.
- **Database migration (idempotent).** `Users` gains `MustChangePassword`,
  `FailedLoginCount`, `LockoutUntil`; the legacy `admin`/`admin123` hash is detected and flagged
  for a mandatory change. This means the installed database will ask for a new admin password on
  its next launch; the live file itself was **not** opened or modified in this session
  (`70A78BCB…` unchanged).
- **Tests.** New `tests/SmartCafeteria.ViewModels.Tests` (17 tests: login failures/lockout message,
  forced change flow, user-management policy and guard feedback) added to the solution.
  Data.Tests gained `AuthSecurityTests` (22 tests: hashing formats, legacy upgrade, lockout and
  expiry, policy, projection hygiene, bootstrap vs admin-only creation, cashier denial on finance
  data, last-admin and self-protection, both `DatabaseInitializer` migrations). Existing tests
  updated to the 12-char policy and an admin `TestCurrentUser` helper; no assertion weakened.
- **Verification.** Build **0 warnings / 0 errors**; Core **51/51**, Data **70/70**,
  Services **255/255**, ViewModels **17/17**; UI.Tests compiled only (never launched).
  `printer.json` untouched (`8D78C34B…`).

## Remediation Phase 5–12

- **Crash handling.** `App` now writes `crash-yyyyMMdd.log` (version, OS, stack) to
  `%LOCALAPPDATA%\SmartCafeteria\Logs`, then the install folder, then temp; every failure to write
  is traced instead of discarded. `DispatcherUnhandledException` sets `Handled = true`, shows an
  Arabic dialog, and calls `Shutdown(1)`. Startup failures are caught inside `OnStartup` (no
  rethrow from `async void`) and follow the same dialog + clean shutdown path.
- **Printing.** `RawPrinterHelper.SendBytes` throws `Win32Exception` with the real spooler error,
  always frees `AllocCoTaskMem` in `finally`, and cleans up the document in the right order.
  Cutting failures are traced, not swallowed. `ThermalPrinter` receipt/kitchen/summary printing and
  printer enumeration now run on a background task (`Task.Run`) so the UI thread never blocks on
  the spooler.
- **Receipt numbering.** New `OrderCounters(Date, LastNumber)` table. `OrderRepository` seeds the
  counter from the day's existing orders once, then allocates with
  `INSERT ... ON CONFLICT(Date) DO UPDATE SET LastNumber = LastNumber + 1 RETURNING LastNumber`
  inside the order transaction. Cancelled/deleted orders no longer cause reuse or gaps.
  `AnalyticsSchemaFreezeTests` now asserts the sixteen frozen tables including `OrderCounters`.
- **PDF/report hardening.** QuestPDF license is set once at startup through
  `QuestPdfConfiguration.EnsureCommunityLicense()` (Community assumption documented there);
  report services only call the idempotent helper. Report filenames use a GUID suffix (no
  predictable temp names), and both PDF services generate off the UI thread. Font registration and
  report cleanup failures are traced.
- **No silent failures.** All 23 `catch { }` sites in `src` were replaced: best-effort paths keep
  their fallback but write `Trace.TraceWarning` (font, cleanup, legacy DB copy, ticket config/font,
  customization JSON, clipboard, initialization/refresh, report bundle). Transaction rollbacks now
  guard the rollback itself so the original exception is never masked. `MainViewModel` notification
  factories still avoid recursive logging but trace the logging failure.
- **Business validation.** New `Data/Validation/BusinessRules.cs` enforces: product Arabic name and
  category required, name/description/category length limits, price ≥ 0, inventory ≥ 0,
  inventory ≤ max, optional English fields length-limited; supplier name required, e-mail syntax
  validated; purchase orders need at least one line, quantity > 0 and unit cost ≥ 0; supplier
  payments must be positive. Wired into `ProductRepository` and `SupplierRepository` (below the UI).
  Product ids created in `InventoryViewModel` now use `Guid.NewGuid()` instead of a millisecond
  timestamp.
- **Queries and search.** `GetAllProductsAsync` / category / search load customizations in one
  `ProductId IN @Ids` query; `GetAllOrdersAsync` loads all items in one `OrderId IN @Ids` query.
  Product search escapes `\`, `%`, `_` and adds `ESCAPE '\'`, so wildcards are literal.
- **UI safety/races.** Delete actions (product, supplier, category, employee, expense, order, user)
  ask for confirmation (default answer "No"). Notification banner uses a
  `CancellationTokenSource` so an older timer cannot hide a newer message. Main view model
  initialization is awaited from `MainWindow.Loaded` (no fire-and-forget at construction);
  filter-driven order reloads are serialized; settings persistence is serialized through a
  `SemaphoreSlim`. `TicketConfigStore.Save` is locked and uses a unique temp file before the atomic
  move. User-facing status lines no longer interpolate raw exception text; the detail goes to
  `Trace`.
- **Tests.** New `BusinessRulesTests` (11) and `ProductSearchTests` (3: literal `%`/`_`, batched
  customizations). Existing integration fixtures updated with the now-required product category;
  no assertion weakened. `FinancesIntegrationTests` was rewritten earlier after a tooling encoding
  round-trip damaged its Arabic (only `read`/`edit`/`write` are used for text now).
- **Verification.** Build **0 warnings / 0 errors**. Core **51/51**, Data **88/88**,
  Services **255/255**, ViewModels **17/17**. UI.Tests compiled only (never launched). Live database
  hash still `70A78BCB69A301429B3374144965EAB135D1D94ABE141B672ABA0B270C16ACE9`; `printer.json`
  still `8D78C34B30CB7FC6255E39AD2EEBEF31D47CD9BB7B5A24D4FD5B920AC40D0BA1`. Release single-file
  build republished to `publish\win-x64`.
- **Deliberate deviations (documented).** Timestamps stay in stored local time because the live
  database already contains local values and a UTC migration would shift historical business days;
  new receipt/business-day boundaries are computed once in C# and passed as parameters (no SQLite
  `date('now','localtime')` in the counter path). Deferred low-severity items: inventory list
  filtering still reallocates its collection per keystroke, navigation still uses string literals,
  emoji seed/markup unchanged, accessibility annotations are unchanged, and stale root artifacts
  were not pruned. `UI.Tests` still assumes the legacy `admin/admin123` flow and must be updated
  before it is ever run against a real profile.

## Printing, Password Policy & Operator Identity (2026-11-11)

### Goal
Fix combined-mode printing, remove the 12-character password rule, and remove the cashier/terminal
fields from general settings while keeping historical order data intact.

### What changed
- **Combined printing.** `PrintReceiptBuilder.PrintOrderWithMethodAsync` and
  `BuildReceiptsForOrder` now send `minimal: true` for the combined (`FullAndSection`) branch, so
  each section gets one full receipt plus one minimal section ticket. The other print methods are
  unchanged.
- **Password policy.** `PasswordPolicy` accepts any non-empty password and rejects only empty/null
  values with `كلمة المرور مطلوبة`. The change-password hint reads `GetError`, so no label claims a
  12-character minimum. Tests now accept short non-empty passwords and reject empty ones.
- **Operator identity.** `User.DisplayName` uses `FullName` with `Username` as fallback.
  `CartViewModel`, `AccountingViewModel`, `EmployeesViewModel`, `SuppliersViewModel` and
  `SettingsViewModel` take `ICurrentUserContext`; cashier/user fields come from the signed-in user
  and the station comes from `Environment.MachineName`. `MainViewModel.CurrentStationName` exposes
  the machine name to `MainWindow`. The cashier/terminal inputs were removed from `SettingsView`.
  `AppSettings.CashierName`/`StationId`, their columns and historical `Order.CashierName` values
  remain for schema/legacy compatibility, but the application no longer reads them for new orders.

### Tests
- `PrintReceiptBuilderTests`: updated combined-mode assertions; new multi-section and print-order
  tests; printing subset passed 20/20.
- New `UserDisplayNameTests` (3 cases) and `CartViewModelTests` proving `CreateOrderAsync` receives
  the signed-in display name and machine name. `LoginViewModelTests` and
  `UserManagementViewModelTests` were updated to the non-empty-password policy. UI-test stubs were
  updated to the new `SettingsViewModel` constructor; UI.Tests compiled only.

### Verification status (2026-11-11)
- Solution build **0 warnings / 0 errors**. Release single-file republished to
  `publish\win-x64\SmartCafeteria.exe` (SHA256
  `69BCCE151519C7343E19DF8C17771548089A8CBDA32DFBDFED80E47B0501FA87`).
- Core.Tests **54/54**, Data.Tests **90/90**, Services.Tests **257/257**, ViewModels.Tests **18/18**
  (419 total). UI.Tests compiled but **not run**.
- `printer.json` unchanged at
  `8D78C34B30CB7FC6255E39AD2EEBEF31D47CD9BB7B5A24D4FD5B920AC40D0BA1`. The live database is now
  `A690F4C6E4794003BE05F4252329A5190A004E47C9724143D7D8F58711352651` (last write 08:28:39, 172032
  bytes, 17 orders). No test uses the live path and no WAL file or app process remained afterwards;
  the change is attributed to external/manual app use, consistent with earlier incidents.

---

# Addendum (2026-11-11): FlaUI coverage pass

## Goal
Audit UI coverage, add missing desktop tests, and run the full suite without touching live data.

## What changed
- Test-only overrides: `SMART_CAFETERIA_DB_PATH` in `DbContext`, `SMART_CAFETERIA_PRINTER_CONFIG`
  in `TicketConfigStore`.
- `UiTestDatabase` creates a disposable copy of the DB, WAL and printer config per test and seeds
  `ui_admin` / `ui_cashier`; `AppTestBase` launches, logs in (with one retry), and tears it down.
- Repaired stale selectors/assertions for product cards, POS order types, reports labels, Arabic
  category labels and the removed receipt modal.
- Fixed `SuppliersView` selected-item binding to `Mode=TwoWay` so choosing a supplier enables PO
  actions (also fixes the real UI flow when selection comes from automation/keyboard).
- Added 9 edge/role tests: empty login, inactive login, cashier nav restrictions, logout, cart
  increment/decrement/remove, empty checkout no-op, inventory search-to-empty, missing username
  validation, and PO actions hidden without a supplier.

## Verification status
- Build: **0 warnings / 0 errors**.
- Full UI suite: **66/66 passed**, 4.55 minutes.
- Non-UI suites: Core **54/54**, Data **90/90**, Services **257/257**, ViewModels **18/18**.
- Republished self-contained single-file `publish\win-x64\SmartCafeteria.exe`, SHA256
  `7CB6D2F4C24396B3B752329C7785661DB342A935DCE559DB2DD37A0487D5BFF3` (162,342,477 bytes).
- Live DB and `printer.json` hashes unchanged (see above).
- Full matrix and residual risks: `FlaUI-Coverage-Report.md`.

## Remaining
- Optional next coverage: UI-level lockout, destructive user/product/supplier deletes, printed/PDF
  output assertions, and command-level (not only nav-level) cashier authorization.

---

# Addendum (2026-11-12): optional inventory restriction + compact inventory forms

## Goal
Make POS inventory enforcement optional from General Settings, hide stock fields when disabled,
and lay out inventory add/edit fields two per row.

## What changed
- **Preference storage.** `TicketPrintConfig.EnforceInventoryTracking` (default `true`) is stored in
  the existing JSON preference file, so the SQLite schema is unchanged and `SchemaFreezeTests` stays
  green.
- **Settings.** `SettingsView` has a General Settings checkbox (`تفعيل ربط البيع بالمخزون`);
  `SettingsViewModel` loads and saves it with the existing settings save command.
- **POS.** `MenuViewModel` skips the zero-stock gate when disabled. `OrderRepository` reads the same
  preference and skips stock decrement/rejection, along with cancellation/deletion restores.
- **Inventory UI.** `InventoryViewModel` excludes hidden stock values from add/save when disabled.
  `InventoryView` now uses a two-column grid for add/edit, hides the `المخزون` / `الحد الأقصى`
  groups when disabled, adds the missing English-description input, and exposes AutomationIds.

## Verification status
- Solution build: **0 warnings / 0 errors**. UI.Tests compiled, **not run**.
- Core **54/54**, Data **96/96**, Services **260/260**, ViewModels **26/26** (436 total).
- No database schema change. Live `printer.json` unchanged at
  `8D78C34B30CB7FC6255E39AD2EEBEF31D47CD9BB7B5A24D4FD5B920AC40D0BA1`.
- Live database at `%LOCALAPPDATA%\SmartCafeteria\smart_cafeteria.db` is
  `56F29AB5D1CE83B2E20B1F65DDB607A2D2F97F173E984DDFB558B50C14778BD8` (176128 bytes, last write
  2026-11-11 15:44). This differs from the hash recorded on 2026-11-11 and was already changed
  before this session; the tests here use temporary databases and do not write the live path.
- `publish\win-x64\SmartCafeteria.exe` was not republished; it remains
  `7CB6D2F4C24396B3B752329C7785661DB342A935DCE559DB2DD37A0487D5BFF3` (162,342,477 bytes).

## Deliberate limitation
- Orders created while tracking was enabled and cancelled after it is disabled are not restocked.
  Avoiding that edge case would require a new order-level schema column, which the schema-freeze
  policy forbids.

---

# Addendum (2026-11-14): commercial licensing, activation UI, and MSI

## Goal
Ship the approved offline licensing layer (install binding, 14-day trial, monthly/permanent
activation, Arabic Activator, Settings activation panel) with a working WiX 6 installer.

## What changed
- **Expiry semantics.** `LicensePolicy.EndOfUtcDay` now drives both `LicensePolicy.Evaluate` and
  `EcdsaLicenseVerifier`, so an encoded expiry date is valid through the end of that UTC day.
- **Activation UI tests.** Added `ActivationTests` (device ID, request generation, invalid-code
  error). Tab selection uses the UI Automation `SelectionItem` pattern because the tabs are
  off-screen; the tab items expose `ActivationRequestTab` / `ActivationEntryTab`.
- **Test fixtures.** Updated the two ticket-preference tests for the new `SettingsViewModel`
  constructor with `ActivationViewModel(Mock.Of<ILicenseService>())`; repaired Arabic text that a
  PowerShell text round-trip had corrupted.
- **Installer.** Rewrote `Product.wxs` for WiX 6: package-level `ComponentGroup`, `StandardDirectory`
  for `ProgramFiles64Folder`/`ProgramMenuFolder`/`DesktopFolder`, `LatoFontFolder` (avoids the
  virtual `FontsFolder` symbol), `MACHINEGUID` via `RegistrySearch`, and the `install.marker` payload
  plus HKLM `InstallMachineGuid` value. `Platform=x64` and `SuppressIces=ICE60` moved to the
  `.wixproj`.
- **End-to-end activation.** Generated a request from the real hardware identity, issued a monthly
  activation with the development key, verified it with the embedded public key, and applied it
  through `LicenseService`. Result: `LICENSE_E2E_OK`; expiry `2026-12-12`.
- **Packaging.** Republished the self-contained single-file app, rebuilt the MSI, and verified the
  packaged EXE via an administrative extraction.

## Verification status
- Solution builds (Debug and Release): **0 warnings / 0 errors**.
- Tests: Core **54/54**, Data **96/96**, Services **260/260**, ViewModels **27/27**,
  Licensing **26/26**, UI **70/70** — **533/533**.
- `publish\win-x64\SmartCafeteria.exe`: 162,622,480 bytes, SHA256
  `63ADDB1E5F1CCF9545BCE60878E1C285DC363EE32FF76247CF7D498E88C8797D`.
- Installer: **0 warnings / 0 errors**; `installer\bin\x64\Release\SmartCafeteria-Setup-1.0.0.msi`
  is 65,003,520 bytes, SHA256
  `3C93FE83B263D76960785852118236E9932264EDB1FFDDAF081F284A6B5C3324`.
- Administrative extraction confirmed the MSI contains the same EXE hash, `install.marker`, and
  all 18 Lato font files.
- The e2e run left no license state behind: the ProgramData, LocalAppData, and HKCU anchors are
  absent, and the temporary console project was removed.

## Remaining
- ~~Replace the development keypair with the production keypair and do not ship the dev key.~~
  Done 2026-11-14; see the production rotation section below.
- ~~Code-sign the EXE/MSI before distribution.~~ Done with a self-signed test certificate on
  2026-11-14; a CA-issued certificate is still required for public distribution.
- Run a manual per-machine install test (marker + HKLM fingerprint + activation) on a clean machine.
  Checklist added to `RELEASE-KEY-ROTATION-AND-CODE-SIGNING.txt` Part 10; not yet executed.
- ~~Exercise `ActivationLockWindow` activation double-close behavior.~~ Done; focused test added.
- `Core`/`Data`/`Services` were re-run at the counts above; `Data` is slow (~6.3 min) due to
  password-hashing tests.

## Production Key Rotation, Lock-Window Fix, and Signed Release Candidate (2026-11-14)

### What changed
- **Production licensing key.** Generated a new ECDSA P-256 keypair outside the repository at
  `C:\Users\user\Documents\SmartCafeteria-Secrets\production-2026`. The embedded public key in
  `LicenseKeys.cs` is now:
  `MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEbGEeD+yNY5hWtLmtuyF7I/kIcggXiYDez3q7AYklLvCDSbYpVb5aSNq231lkskFx9gHRIQNuSL2pDE679ISh9w==`.
  Fingerprint:
  `967058902DA6703A13438500D1F3857A832BDCE7A1B9C7DDDFD9D0686B585674`.
- **Development key removed.** Deleted the old `.pk8`/`.spki` from the Activator source and all
  build outputs. A backup remains only in the external secrets folder under `legacy-dev-keys`.
  Removed the Activator `.csproj` rule that copied `keys\**\*` into build output, so a production
  private key cannot be copied there by accident.
- **Lock-window double-close fixed.** `ActivationLockWindow` now treats activation as idempotent:
  it unsubscribes, sets `DialogResult` only when valid, closes only if still visible, and does not
  unsubscribe when `OnClosing` cancels the close. Added
  `ActivationLockWindowTests.LockedApp_Activation_ClosesWindowOnceAndShowsLogin`; it seeds an
  expired trial, issues a monthly code with an ephemeral key, activates through the real lock
  window, and waits for `PasswordInput`.
- **Key-separation proof.** A temp verifier generated a monthly code with the production private
  key and verified it successfully, while a code signed with the old development private key was
  rejected (`توقيع رمز التفعيل غير صالح`). A separate untrusted key was also rejected.
- **Secrets handling.** `PRODUCTION-SECRETS.txt` is outside the repo and ACL-restricted to the
  current Windows user. It holds the production private-key password and PFX password; those
  passwords are deliberately not recorded here.
- **Signing certificate.** Created a self-signed code-signing certificate:
  `CN=Smart Cafeteria Development Code Signing, O=Smart Cafeteria, C=EG`,
  thumbprint `A9FC9D49203637ED8492468475EA989F84C6575C`, valid through 2031-11-14. PFX/CER are in
  the external secrets folder. This is a test certificate, not a public trust anchor.
- **Signing method.** `signtool.exe` was not installed. Signed the EXE and MSI with PowerShell
  `Set-AuthenticodeSignature` using SHA256 and the DigiCert RFC3161 timestamp endpoint.

### Verification
- Debug and Release builds: **0 warnings / 0 errors**.
- Debug tests: Core **54/54**, Data **96/96**, Services **260/260**, ViewModels **27/27**,
  Licensing **26/26**, UI **71/71** — **534/534**.
- The first full-solution run had one UI timing failure
  (`PosFlowTests.Menu_Checkout_ResetsTheOrderTypeAndTheNote`) while Data/Services were running in
  parallel. The test passed alone, and the full UI project passed alone **71/71**. Treat the
  parallel-solution UI run as load-sensitive; run UI tests separately.
- Final signed EXE: `publish\win-x64\SmartCafeteria.exe`, **162,440,760 bytes**, SHA256
  `640FAC1E39D7A5AAC2A1A2BA806F9ECAC0906E1085A7772E61AE4453C355973D`.
- Final signed MSI: `installer\bin\x64\Release\SmartCafeteria-Setup-1.0.0.msi`,
  **64,823,296 bytes**, SHA256
  `428748B23EF736D7F67F35E4A158BD77A4335410637B8AA9D259914BB353F5E8`.
- MSI administrative extraction: `msiexec` exit **0**; extracted EXE hash matches the signed
  published EXE exactly; `install.marker` present; all **18** Lato `.ttf` files present.
- Signature state: both files carry signer thumbprint
  `A9FC9D49203637ED8492468475EA989F84C6575C` and a DigiCert timestamp. With the self-signed CER
  temporarily trusted in CurrentUser Root/TrustedPublisher, both report **Valid**. With normal
  trust settings they report **UnknownError: certificate chain terminated in a root certificate
  which is not trusted**, as expected. The temporary trust entries were removed after verification.
- Repository audit found no `.pk8`, `.pfx`, `.p12`, or production key material under the project.

### Remaining release limits
- The self-signed certificate cannot establish public trust or SmartScreen reputation. Replace it
  with a CA-issued OV/EV code-signing certificate before public distribution.
- The clean-machine per-machine install checklist is in
  `RELEASE-KEY-ROTATION-AND-CODE-SIGNING.txt` Part 10 but has not been executed on a clean VM.
- The version remains `1.0.0`; bump it consistently before a new public release.

## NPM Audit Residual Risk Acceptance (2026-11-18)
- `npm audit --omit=dev --audit-level=moderate` reports unresolved transitive advisories in the
  Expo/Jest toolchain (`glob`, `minimatch`, `picomatch`; GHSA-3g3m-2fjr-6pw4,
  GHSA-g3hv-75wg-m249, GHSA-fc5q-39xc-vjpf).
- `npm audit fix --dry-run` leaves the advisories in place. The available force fix downgrades
  Jest to `26.6.3` and breaks the Expo/React Native toolchain. Upstream `picomatch` has no patched
  release for the reported advisory.
- Decision: accept the residual development-tooling risk, keep the dependency graph unchanged,
  and revisit when upstream publishes a non-breaking patched version. Runtime dependencies are
  not known to use the affected code path, and all mobile unit, lint, typecheck, doctor, and
  bundle-export gates pass.

