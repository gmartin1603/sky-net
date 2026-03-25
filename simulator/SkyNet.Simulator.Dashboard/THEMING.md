# Dashboard Theming

Use the shared tokens in `wwwroot/css/app.css` when adding or updating dashboard UI.

- Base surfaces: `--app-bg`, `--app-surface`, `--app-surface-elevated`, `--app-surface-muted`
- Content colors: `--app-fg`, `--app-muted`, `--app-link`, `--app-code`
- Panels and chrome: `--app-panel-bg`, `--app-panel-bg-soft`, `--app-panel-stroke`, `--app-shadow-soft`, `--app-shadow-strong`, `--app-panel-shadow`
- Status and actions: `--app-accent`, `--app-accent-strong`, `--app-accent-contrast`, `--app-success`, `--app-danger`
- Focus states: `--app-focus-outer`, `--app-focus-base`
- Navigation: `--app-topbar-*`
- Charts: `--chart-series-1`, `--chart-series-2`, `--chart-series-3`

Guidelines:

- Do not hardcode white, black, or light translucent fills in components unless the visual must ignore theme.
- If a component needs a new semantic color, add a named token in both light and dark theme blocks.
- Prefer semantic tokens over component-local hex values; component CSS can remix tokens with `color-mix(...)` when needed.
- For SVG-heavy components, use `var(--token-name)` directly in `fill`, `stroke`, and gradients.
