---
name: Academic Precision
colors:
  surface: '#f8f9fa'
  surface-dim: '#d9dadb'
  surface-bright: '#f8f9fa'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f3f4f5'
  surface-container: '#edeeef'
  surface-container-high: '#e7e8e9'
  surface-container-highest: '#e1e3e4'
  on-surface: '#191c1d'
  on-surface-variant: '#414844'
  inverse-surface: '#2e3132'
  inverse-on-surface: '#f0f1f2'
  outline: '#717973'
  outline-variant: '#c1c8c2'
  surface-tint: '#3e6653'
  primary: '#00160c'
  on-primary: '#ffffff'
  primary-container: '#012d1d'
  on-primary-container: '#6d9681'
  inverse-primary: '#a5d0b8'
  secondary: '#0055c9'
  on-secondary: '#ffffff'
  secondary-container: '#036cfb'
  on-secondary-container: '#fefcff'
  tertiary: '#260709'
  on-tertiary: '#ffffff'
  tertiary-container: '#401b1d'
  on-tertiary-container: '#b67f80'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#c0edd4'
  primary-fixed-dim: '#a5d0b8'
  on-primary-fixed: '#002114'
  on-primary-fixed-variant: '#264e3c'
  secondary-fixed: '#dae2ff'
  secondary-fixed-dim: '#b1c5ff'
  on-secondary-fixed: '#001946'
  on-secondary-fixed-variant: '#00419e'
  tertiary-fixed: '#ffdada'
  tertiary-fixed-dim: '#f5b7b8'
  on-tertiary-fixed: '#331013'
  on-tertiary-fixed-variant: '#663a3c'
  background: '#f8f9fa'
  on-background: '#191c1d'
  surface-variant: '#e1e3e4'
  slate-gray: '#6C757D'
  border-muted: '#E2E3E5'
typography:
  display-lg:
    fontFamily: Manrope
    fontSize: 48px
    fontWeight: '700'
    lineHeight: 56px
    letterSpacing: -0.02em
  display-lg-mobile:
    fontFamily: Manrope
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
    letterSpacing: -0.01em
  headline-lg:
    fontFamily: Manrope
    fontSize: 32px
    fontWeight: '600'
    lineHeight: 40px
  headline-lg-mobile:
    fontFamily: Manrope
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
  headline-md:
    fontFamily: Manrope
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
  body-lg:
    fontFamily: Manrope
    fontSize: 18px
    fontWeight: '400'
    lineHeight: 28px
  body-md:
    fontFamily: Manrope
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  label-md:
    fontFamily: Manrope
    fontSize: 14px
    fontWeight: '500'
    lineHeight: 20px
    letterSpacing: 0.01em
  label-sm:
    fontFamily: Manrope
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.05em
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  base: 8px
  container-max: 1200px
  gutter: 24px
  margin-mobile: 16px
  margin-desktop: 48px
---

## Brand & Style

The brand personality is intellectual, structured, and forward-thinking, catering to researchers, academics, and information architects. The visual identity avoids decorative excess, focusing instead on the clarity of information and the structural integrity of data.

This design system employs a **Modern Minimalist** style. It leverages significant white space, a restricted and purposeful color palette, and high-quality typography to create a sense of calm authority. The interface should feel like a premium digital library—organized, accessible, and quiet, allowing the content to remain the primary focus.

## Colors

The color strategy is anchored by a deep, scholarly green (`#012D1D`), which serves as the primary brand anchor for headings and navigation. This is contrasted against a clean, off-white background (`#F8F9FA`) to reduce eye strain during long reading sessions.

A vibrant blue (`#0D6EFD`) is used sparingly as a secondary accent for interactive elements like links and primary calls-to-action, providing a clear visual cue for navigation. Neutral tones are used for structural borders and secondary metadata to maintain a hierarchical distance from the core content.

## Typography

The typography system is built entirely on **Manrope** to achieve a modern, geometric, and highly legible aesthetic. The focus is on mathematical vertical rhythm and clear hierarchy.

Headlines use tighter letter spacing and heavier weights to command attention, while body text uses a generous line height (1.5x - 1.6x) to facilitate "deep reading." For secondary information and technical labels, the font weight is increased, and the size is decreased to maintain a clean, grid-like appearance without overwhelming the user.

## Layout & Spacing

This design system utilizes a **Fixed Grid** philosophy for desktop screens to ensure readability remains optimized at a maximum width of 1200px. Content is organized on a 12-column grid.

The spacing rhythm is based on an 8px baseline. Large sections are separated by significant vertical padding (80px to 120px) to create "breathing room" between distinct intellectual concepts. On mobile devices, the grid transitions to 4 columns with reduced margins (16px) to maximize the limited horizontal space for text-heavy content.

## Elevation & Depth

Depth is communicated primarily through **Tonal Layers** rather than heavy shadows. Different levels of the `neutral` palette are stacked to create hierarchy—for example, a secondary container may use a slightly darker or lighter background than the main page.

Where physical separation is required, use **Low-contrast outlines**. Borders should be thin (1px) and use the `#E2E3E5` color. If shadows are absolutely necessary for floating elements like modals, use a "Soft Ambient" shadow: `0px 4px 20px rgba(1, 45, 29, 0.05)`, tinting the shadow with the primary dark green to keep it integrated with the brand palette.

## Shapes

The shape language is "Soft" and professional. While the brand is serious, sharp 90-degree corners are avoided to keep the interface approachable. Most UI components like buttons and input fields use a 0.25rem (4px) corner radius. Larger containers, such as feature cards, may use up to 0.75rem (12px) to subtly differentiate them from the page background.

## Components

- **Buttons:** Primary buttons use the deep green (`#012D1D`) with white text. They should have a subtle hover state that shifts to the secondary blue (`#012D1D`) or increases opacity. Text is always semi-bold.
- **Input Fields:** Use a 1px border (`#E2E3E5`) with a focus state that highlights the border in the secondary blue. Labels sit above the field in the `label-sm` style.
- **Cards:** Cards are defined by their 1px border rather than a shadow. They should include generous internal padding (min 24px) to respect the minimalist aesthetic.
- **Chips/Tags:** Used for categories or metadata, chips should have a light gray background (`#E2E3E5`) and use the `label-sm` typography.
- **Data Tables:** Tables should be minimalist, utilizing only horizontal dividers in `#E2E3E5`. Avoid vertical lines. Headers should be `label-sm` and uppercase for clear distinction.
- **Navigation:** Top-level navigation uses the `label-md` style with significant horizontal spacing between items.