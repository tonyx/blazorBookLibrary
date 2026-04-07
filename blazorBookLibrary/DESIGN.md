# Design System Strategy: The Modern Archivist

## 1. Overview & Creative North Star
The Creative North Star for this design system is **"The Curated Sanctuary."** 

We are moving away from the "utility software" aesthetic of traditional library databases. Instead, we are building a high-end editorial experience that feels like walking into a private, sun-drenched study. This system rejects the rigid, boxy constraints of typical dashboards in favor of **intentional asymmetry, expansive breathing room, and tonal depth.** 

By layering deep botanical greens against warm, paper-like neutrals, we create a digital environment that honors the tactile history of books while providing a frictionless modern interface. We prioritize readability and "serendipitous discovery" over dense data packing.

---

## 2. Colors & Surface Philosophy

### The Palette
The color logic centers on `primary` (#012d1d) for authority and `secondary` (#77583d) for warmth, mimicking the pairing of dark-stained oak and forest-green leather.

### The "No-Line" Rule
**Traditional 1px borders are strictly prohibited for sectioning.** 
To define space, use background shifts. A `surface-container-low` (#f6f3f0) sidebar should sit adjacent to a `surface` (#fcf9f6) main content area. This creates a "soft edge" that feels architectural rather than engineered.

### Surface Hierarchy & Nesting
Treat the UI as a series of stacked materials. 
*   **Level 0:** `surface` (#fcf9f6) - The base "paper" of the application.
*   **Level 1:** `surface-container-low` (#f6f3f0) - Used for expansive background areas or navigation rails.
*   **Level 2:** `surface-container` (#f0edea) - Used for the "well" or "tray" holding content cards.
*   **Level 3:** `surface-container-highest` (#e5e2df) - Reserved for active states or nested elements requiring immediate focus.

### The "Glass & Gradient" Rule
To add a "signature" polish, use semi-transparent `surface_container_lowest` (#ffffff at 70% opacity) with a `backdrop-filter: blur(12px)` for floating navigation bars or search overlays. For Hero CTAs, apply a subtle linear gradient from `primary` (#012d1d) to `primary_container` (#1b4332) to give the button a "jewel-like" depth.

---

## 3. Typography
The system utilizes a high-contrast pairing to distinguish between **Content** (The Book) and **Utility** (The System).

*   **Editorial Serif (Noto Serif):** Used for `display`, `headline`, and `title` scales. This font carries the "soul" of the library. Book titles in cards must always use `headline-sm` or `title-lg` in Noto Serif to feel like a physical cover.
*   **Functional Sans (Manrope):** Used for `body`, `label`, and administrative data. Manrope provides a clean, neutral counter-balance to the serif, ensuring that metadata (ISBNs, dates, status) is ultra-readable and modern.

---

## 4. Elevation & Depth

### The Layering Principle
Hierarchy is achieved through **Tonal Layering**. Instead of a shadow, place a `surface-container-lowest` (#ffffff) card on top of a `surface-container` (#f0edea) background. This creates a natural "lift" that feels premium and light.

### Ambient Shadows
When an element must float (e.g., a "Quick View" modal), use an **Ambient Shadow**:
*   `color`: `on-surface` (#1c1c1a) at 5% opacity.
*   `blur`: 40px.
*   `offset-y`: 10px.
This mimics natural light hitting a heavy object, avoiding the "dirty" look of standard black shadows.

### The "Ghost Border" Fallback
If contrast is legally required for accessibility, use a **Ghost Border**: `outline-variant` (#c1c8c2) at 20% opacity. Never use a 100% opaque border.

---

## 5. Components

### Search Bar (The Entryway)
The search bar is not a box; it is an experience. Use `surface_container_lowest` with a `xl` (0.75rem) roundedness. Add a soft `primary` tinted shadow on focus. The placeholder text should be in `body-lg` using the Serif font to invite literary exploration.

### Book Cards
*   **Structure:** No borders. Use `surface_container_lowest` background.
*   **Spacing:** Use `spacing-4` (1rem) for internal padding.
*   **Typography:** Book Title in `notoSerif / headline-sm`. Author in `manrope / label-md` with `secondary` (#77583d) coloring.
*   **Image:** Use `md` (0.375rem) corner radius on book covers to mimic the slight rounding of a spine.

### Status Badges
Status should feel integrated, not like a "system error."
*   **Available:** `primary_fixed` (#c1ecd4) background with `on_primary_fixed` (#002114) text.
*   **Borrowed:** `secondary_fixed` (#ffdcc1) background with `on_secondary_fixed` (#2c1603) text.
*   **Overdue:** `error_container` (#ffdad6) background with `on_error_container` (#93000a) text.
*   **Shape:** Use `full` (9999px) roundedness for a pill shape.

### Data Tables (The Ledger)
*   **Forbid Dividers:** Remove all horizontal and vertical lines. 
*   **Alternating Tones:** Use `surface_container_low` for the table header and `surface` for rows. 
*   **Interaction:** On hover, change row background to `surface-container-highest` (#e5e2df).
*   **Padding:** Use `spacing-4` vertically and `spacing-6` horizontally to give data "room to breathe."

### Primary Buttons
*   **Background:** Linear gradient of `primary` to `primary_container`.
*   **Text:** `on_primary` (#ffffff) in `manrope / title-sm` bold.
*   **Radius:** `lg` (0.5rem).

---

## 6. Do's and Don'ts

### Do
*   **DO** use whitespace as a separator. If you think you need a line, try adding `spacing-8` (2rem) of empty space instead.
*   **DO** mix font families within a single component. Use Noto Serif for the "What" (Title) and Manrope for the "How" (Status/Meta).
*   **DO** use "Ink" colors. Text should rarely be pure black; use `on_surface` (#1c1c1a) for a softer, more organic feel.

### Don't
*   **DON'T** use 1px solid borders. They break the "Sanctuary" feel and make the app look like a spreadsheet.
*   **DON'T** use high-saturation greens or blues. Stick to the muted, deep tones of the `primary` and `secondary` tokens.
*   **DON'T** crowd the edges. Every container should have at least `spacing-5` (1.25rem) of internal padding to maintain the high-end editorial look.
