# Native Confirmation Dialog Design Specification

**Feature:** Native `<dialog>` Modernization for BarberBook Confirmation Dialog  
**Target File:** `src/BarberBook.Web/Views/Shared/_ConfirmDialog.cshtml`  
**Test File:** `tests/BarberBook.Tests/ToastAndConfirmModalTests.cs`  
**Date:** 2026-11-21  
**Status:** Approved  

---

## 1. Intent & Problem Statement
Currently, `_ConfirmDialog.cshtml` renders a custom `div[role="dialog"]` backdrop-and-card with ad-hoc JavaScript focus trapping, manual `z-[10001]` layering, and custom event listeners for Esc and backdrop clicks. While functional, it does not use the browser's native top-layer `<dialog>` element, does not prevent underlying page scrolling via CSS overscroll containment, and lacks physical micro-interaction feedback on its action buttons.

## 2. Requirements & Constraints
1. **Modern Web Guidance Compliance:**
   - Use the native `<dialog>` element opened via `dialog.showModal()`.
   - Enable declarative light-dismiss with `closedby="any"` on `<dialog>`.
   - Prevent background page scroll chaining via pure CSS:
     - `dialog:modal { overscroll-behavior: contain; }`
     - `dialog::backdrop { overflow: hidden; overscroll-behavior: contain; }`
   - Include coordinate-based fallback for browsers lacking native `closedBy` support.
2. **Backward Compatibility:**
   - Maintain `id="barberbook-confirm-dialog"`.
   - Maintain `role="dialog"`, `aria-modal="true"`, `aria-labelledby="confirm-dialog-title"`, `aria-describedby="confirm-dialog-desc"`.
   - Maintain `window.showConfirmDialog({...})` returning a Promise resolving to `true` on confirmation or `false` on cancellation/dismissal.
   - Maintain alias `window.confirmModal = window.showConfirmDialog`.
   - Maintain declarative `data-confirm` handling for links, buttons, and forms.
3. **Taste-Skill & Design System Compliance:**
   - Adhere to `docs/DESIGN_GUIDELINES.md` color tokens (Warm Cream, Dark Leather Slate, Whiskey Gold, Rose Destructive).
   - Add button micro-interactions: `active:scale-[0.98] hover:-translate-y-0.5 transition-all duration-150 ease-out`.
   - Add focus ring with offset (`focus-visible:ring-2 focus-visible:ring-amber-500 focus-visible:ring-offset-2`).

## 3. Architecture & Interface
- **Root Element:** `<dialog id="barberbook-confirm-dialog" closedby="any" role="dialog" aria-modal="true" aria-labelledby="confirm-dialog-title" aria-describedby="confirm-dialog-desc" ...>`
- **Styling:** Tailwind CSS utility classes paired with scoped `<style>` for `dialog#barberbook-confirm-dialog:modal` and `dialog#barberbook-confirm-dialog::backdrop`.
- **Script Controller:** An IIFE attached to `window.showConfirmDialog` and `window.confirmModal`, managing Promise resolution via `dialog.close()` and `cancel` event listeners.
