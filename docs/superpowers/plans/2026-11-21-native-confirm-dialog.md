# Native Confirmation Dialog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Modernize `_ConfirmDialog.cshtml` to use native HTML `<dialog>`, declarative light-dismiss (`closedby="any"`), CSS overscroll containment, and vintage-modern button micro-interactions while preserving all existing APIs and test assertions.

**Architecture:** Replace the custom fixed `<div>` modal with native `<dialog>` in the top layer via `dialog.showModal()`. Prevent background scroll chaining with `overscroll-behavior: contain`. Add fallback light dismiss for older browsers. Maintain backward compatibility with `window.showConfirmDialog` and declarative `[data-confirm]` bindings.

**Tech Stack:** ASP.NET Core 8 MVC, Razor Views, HTML5 `<dialog>`, Tailwind CSS, xUnit, FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-11-21-native-confirm-dialog-design.md`

## Global Constraints
- Target file: `src/BarberBook.Web/Views/Shared/_ConfirmDialog.cshtml`
- Test file: `tests/BarberBook.Tests/ToastAndConfirmModalTests.cs`
- Must retain `id="barberbook-confirm-dialog"`
- Must retain `window.showConfirmDialog` and `window.confirmModal`
- Must retain `data-confirm` delegation for buttons, links, and forms

## Review Focus
- Browser compatibility where `closedBy` is unsupported: must fall back to bounding box click check.
- `dialog::backdrop` must have `overflow: hidden; overscroll-behavior: contain;`.
- Esc key and cancel requests must cleanly resolve the Promise to `false`.
- Standard vs Destructive styling must toggle correctly without style leaks.

---

### Task 1: Update TDD Unit Test Assertions
**Files:**
- Modify: `tests/BarberBook.Tests/ToastAndConfirmModalTests.cs:66-99`

- [ ] **Step 1: Write the updated test assertions for native `<dialog>`**
Add checks for `<dialog`, `closedby="any"`, `showModal()`, and `overscroll-behavior: contain` to `EO4_P18_ConfirmDialogComponent_ContainsAccessibleDialogAndDeclarativeBindings`.
- [ ] **Step 2: Run test to verify it fails on the current implementation**
Run: `dotnet test --filter EO4_P18_ConfirmDialogComponent_ContainsAccessibleDialogAndDeclarativeBindings`
Expected: FAIL (missing `<dialog`, `closedby="any"`, `showModal()`).

### Task 2: Refactor `_ConfirmDialog.cshtml` Markup & CSS
**Files:**
- Modify: `src/BarberBook.Web/Views/Shared/_ConfirmDialog.cshtml:1-40`

- [ ] **Step 1: Replace custom `<div>` with native `<dialog>`**
Replace root `<div id="barberbook-confirm-dialog" ...>` with:
`<dialog id="barberbook-confirm-dialog" closedby="any" role="dialog" aria-modal="true" aria-labelledby="confirm-dialog-title" aria-describedby="confirm-dialog-desc" ...>`
- [ ] **Step 2: Add CSS scroll containment**
Include scoped CSS for `dialog#barberbook-confirm-dialog:modal` and `dialog#barberbook-confirm-dialog::backdrop`.
- [ ] **Step 3: Add `taste-skill` vintage-modern button styling**
Apply `active:scale-[0.98] hover:-translate-y-0.5 transition-all duration-150` on cancel and proceed buttons.

### Task 3: Refactor Script Engine to use `showModal()` and Native Dialog APIs
**Files:**
- Modify: `src/BarberBook.Web/Views/Shared/_ConfirmDialog.cshtml:41-227`

- [ ] **Step 1: Implement `window.showConfirmDialog` with `dialog.showModal()`**
- [ ] **Step 2: Add `close` and `cancel` event listeners to resolve Promise with boolean**
- [ ] **Step 3: Add progressive light-dismiss fallback for browsers lacking `closedBy`**
- [ ] **Step 4: Keep `[data-confirm]` delegation for links, buttons, and forms**

### Task 4: Verification & Test Execution
- [ ] **Step 1: Run unit test to verify it passes**
Run: `dotnet test --filter EO4_P18_ConfirmDialogComponent_ContainsAccessibleDialogAndDeclarativeBindings`
Expected: PASS
- [ ] **Step 2: Run build to verify 0 warnings and 0 errors**
Run: `dotnet build`
Expected: PASS
