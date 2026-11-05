# BarberBook — Design System & Frontend Guidelines

This document defines the visual design system, UI specifications, and component conventions for **BarberBook**. All AI agents, contributors, and developers MUST adhere to these guidelines to ensure continuous aesthetic consistency across every view, component, and user portal.

---

## 1. Visual Identity & Design Philosophy

**Aesthetic Concept:** *Classic Vintage-Modern Barbershop (Heritage Manila Craft)*
- **Craft & Heritage:** Evokes traditional apothecary grooming, hot towel straight-razor precision, leather barber chairs, and brass fittings.
- **Modern Usability:** Crisp, responsive layout built on Tailwind CSS, highly legible typography, intuitive 30-minute booking grids, and accessible form controls.
- **Whimsical Barber Signature:** Animated 3D-styled barber poles and ribbon dividers that rotate smoothly using pure CSS (no external JS or heavy assets).

---

## 2. Color Palette & Design Tokens

| Token / Role | Hex Code | Tailwind Equivalent / Usage | Context |
| :--- | :--- | :--- | :--- |
| **Warm Cream** (Primary Light BG) | `#FBF7EE` | `bg-[#FBF7EE]`, `bg-amber-50/40` | Main customer page body, light sections, cards |
| **Dark Leather Slate** (Primary Dark BG) | `#0F172A` | `bg-slate-900`, `bg-[#0F172A]` | Navbar, hero banners, footer, dark containers |
| **Charcoal Surface** (Elevated Dark) | `#1E293B` | `bg-slate-800`, `bg-neutral-900/90` | Cards on dark backgrounds, stepper backgrounds |
| **Whiskey Gold / Brass** (Accent CTA) | `#B4832C` / `#F59E0B` | `bg-amber-500`, `text-amber-400`, `border-amber-500/30` | Primary action buttons, badges, star ratings, borders |
| **Barber Pole Red** (Accent Highlight) | `#D62828` | `#D62828`, `bg-red-600` | Barber pole stripes, subtle highlight accents |
| **Barber Pole Navy** (Accent Highlight) | `#1D3557` | `#1D3557`, `bg-blue-900` | Barber pole stripes, subtle decorative borders |
| **Border Soft** | `#E2E8F0` / `#F1F5F9` | `border-slate-200`, `border-amber-900/10` | Card borders, dividers on cream background |
| **Border Dark** | `#334155` | `border-slate-700`, `border-neutral-800` | Borders on dark slate containers |

---

## 3. Typography System

Fonts are imported via Google Fonts in `_LayoutCustomer.cshtml`:
```html
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Playfair+Display:ital,wght@0,600;0,700;0,900;1,400;1,700&family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap" rel="stylesheet">
```

### 3.1 Display & Headings (`font-serif`)
- **Font Family:** `'Playfair Display', Georgia, serif`
- **Class:** `font-serif`
- **Weight:** `font-black` (900) or `font-bold` (700)
- **Tracking:** `tracking-tight`
- **Usage:** Page titles (`h1`, `h2`), hero headers, service item names, modal titles.
- **Example:**
  ```html
  <h1 class="text-3xl sm:text-4xl font-serif font-black text-slate-900 tracking-tight">
      Meet Our Master Barbers
  </h1>
  ```

### 3.2 UI, Form Controls & Body (`font-sans`)
- **Font Family:** `'Plus Jakarta Sans', system-ui, sans-serif`
- **Class:** `font-sans`
- **Weight:** `font-normal` (400), `font-medium` (500), `font-semibold` (600), `font-bold` (700)
- **Usage:** Body copy, buttons, form inputs, time slot grids, badges, tables.

---

## 4. The Pure CSS Animated Barber Pole Component

A trademark visual signature across the application. Implemented purely in CSS (`site.css`) using linear gradients, cylindrical borders, and infinite linear keyframe animation.

### 4.1 Classes Available

1. `.barber-pole-icon` (Inline miniature pole, 12px × 24px)
   - Used inside badges, navbar logos, and card headers.
2. `.barber-pole-md` (Medium desktop pole, 20px × 48px)
   - Used in hero section sidebars and callout blocks.
3. `.barber-pole-lg` (Large statement pole, 32px × 96px)
   - Used in feature showcases and about sections.
4. `.barber-pole-divider` (Continuous animated barber ribbon bar, 4px height)
   - Placed at the very top of the page under the header or between major layout sections.

### 4.2 CSS Implementation (`site.css`)
```css
/* Animated barber pole pattern */
@keyframes pole-spin {
    0% { background-position: 0 0; }
    100% { background-position: 68px 0; }
}

@keyframes pole-spin-vertical {
    0% { background-position: 0 0; }
    100% { background-position: 0 68px; }
}

.barber-pole-icon {
    width: 12px;
    height: 24px;
    border-radius: 9999px;
    border: 1.5px solid #cbd5e1;
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.3), inset 0 1px 1px rgba(255, 255, 255, 0.6);
    background: repeating-linear-gradient(
        -45deg,
        #D62828 0 8px,
        #FFFFFF 8px 16px,
        #1D3557 16px 24px,
        #FFFFFF 24px 32px
    );
    background-size: 45px 45px;
    animation: pole-spin-vertical 2s linear infinite;
}

.barber-pole-divider {
    height: 4px;
    width: 100%;
    background: repeating-linear-gradient(
        -45deg,
        #D62828 0 12px,
        #FFFFFF 12px 24px,
        #1D3557 24px 36px,
        #FFFFFF 36px 48px
    );
    background-size: 68px 68px;
    animation: pole-spin 3s linear infinite;
}
```

---

## 5. UI Component Specifications

### 5.1 Vintage Badges & Pills
Always pair section titles with a badge:
```html
<div class="inline-flex items-center gap-2 mb-3 px-3 py-1 rounded-full bg-slate-900 border border-amber-500/30 text-amber-300 text-xs font-semibold shadow-inner">
    <div class="barber-pole-icon" style="height: 16px; width: 9px;"></div>
    <span>AUTHENTIC GROOMING · EST. 2026</span>
</div>
```

### 5.2 Primary Buttons & Action CTAs
- **Background:** `bg-amber-500 hover:bg-amber-400 active:bg-amber-600`
- **Text:** `text-neutral-950 font-extrabold text-sm`
- **Corners:** `rounded-xl` or `rounded-2xl`
- **Shadow:** `shadow-md shadow-amber-500/20`
- **Transition:** `transition-all duration-200 transform hover:-translate-y-0.5`

### 5.3 Apothecary Service Menu (Services View)
- Display service list with dotted leader line between the service name and the price:
  ```html
  <div class="flex items-baseline justify-between gap-4">
      <h3 class="font-serif font-bold text-lg text-slate-900">Precision Haircut</h3>
      <div class="flex-grow border-b border-dotted border-amber-900/30 mb-1"></div>
      <span class="font-bold text-lg text-amber-700">₱150.00</span>
  </div>
  ```

### 5.4 Master Barber Cards (Barbers View)
- Elevated cards on warm white: `bg-white/95 rounded-2xl border border-amber-900/10 shadow-md hover:shadow-xl hover:-translate-y-1 transition-all duration-200`
- Real portrait photo or monogram fallback: `aspect-square object-cover rounded-t-2xl`
- Status pill: `Active Master Staff` with green glowing dot
- Barber display name in `font-serif font-bold text-slate-900 text-xl`
- Direct CTA: "Book with {Name}" linking to `/book?barber={id}`.

### 5.5 Forms & Auth (Login / Register / Profile)
- Centered card: `max-w-md mx-auto my-6 sm:my-12 bg-white/95 rounded-2xl shadow-xl border border-amber-900/10 p-8 sm:p-10 backdrop-blur-sm`
- Form fields: `rounded-xl border border-slate-300 px-4 py-2.5 text-slate-900 shadow-sm focus:border-amber-500 focus:outline-none focus:ring-2 focus:ring-amber-500/20 text-sm`
- Labels: `block text-xs uppercase tracking-wider text-slate-600 font-bold mb-1.5`
- Always use standard direct routes (`action="/login"`, `action="/register"`, `href="/login"`, `href="/register"`) to avoid routing ambiguity with backend API endpoints.

---

## 6. Curated Photography Guidelines

When adding imagery to service highlights, barber cards, or hero backgrounds, use high-contrast vintage barbershop photography:
- **Aspect Ratios:** `aspect-square` for barber portraits; `aspect-[4/3]` or `aspect-video` for service cards.
- **Curation Sources (Unsplash CDN):**
  - Classic Barber at Work: `https://images.unsplash.com/photo-1503951914875-452162b0f3f1?auto=format&fit=crop&w=800&q=80`
  - Hot Towel & Straight Razor: `https://images.unsplash.com/photo-1621605815971-fbc98d665033?auto=format&fit=crop&w=800&q=80`
  - Beard Sculpting: `https://images.unsplash.com/photo-1622286342621-4bd786c2447c?auto=format&fit=crop&w=800&q=80`
  - Barber Shop Interior: `https://images.unsplash.com/photo-1585747860715-2ba37e788b70?auto=format&fit=crop&w=1200&q=80`
  - Barber Portraits (Ramon, Dennis, Miguel):
    - `photo-1506794778202-cad84cf45f1d`
    - `photo-1507003211169-0a1dd7228f2d`
    - `photo-1500648767791-00dcc994a43e`

---

## 7. Rules for Future Modifications

1. **Never replace the warm vintage theme** with generic cold corporate white/blue styles.
2. **Always include the barber pole motif** in layout headers or navigation partials.
3. **Always display currency with the Philippine Peso sign (₱)** formatted via `FormattingHelpers.FormatPeso(amount)`.
4. **Always preserve server-side validation & error banners** using consistent error styling.
5. **Keep navigation explicit:** use `/login` and `/register` URLs for user-facing authentication flows.
