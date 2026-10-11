import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';

/**
 * PrimeNG preset (ADR-0006): Aura's behaviour with this app's look — text first, borders not shadows,
 * radius 4/6/8. Every colour comes from the design tokens in styles.scss (referenced or mixed from
 * them), so the tokens stay the single source.
 */
export const SmartMarketPreset = definePreset(Aura, {
  primitive: {
    borderRadius: { none: '0', xs: '2px', sm: '4px', md: '6px', lg: '8px', xl: '8px' },
    // Aura's danger (red) and warn (orange) severities both become --warn: today one colour marks
    // errors, deadlines and destructive actions. 500 is the token itself, as Aura paints severities with 500.
    red: warnScale(),
    orange: warnScale(),
  },
  semantic: {
    primary: {
      50: tint('--accent', 8),
      100: 'var(--accent-soft)',
      200: tint('--accent', 32),
      300: tint('--accent', 50),
      400: tint('--accent', 68),
      500: tint('--accent', 84),
      600: 'var(--accent)',
      700: shade('--accent', 85),
      800: shade('--accent', 70),
      900: shade('--accent', 55),
      950: shade('--accent', 35),
    },
    focusRing: {
      width: '2px',
      style: 'solid',
      color: '{primary.color}',
      offset: '2px',
      shadow: 'none',
    },
    formField: { paddingX: '10px', paddingY: '8px', borderRadius: '{border.radius.md}' },
    overlay: {
      select: { borderRadius: '{border.radius.md}', shadow: 'none' },
      popover: { borderRadius: '{border.radius.md}', padding: '0.75rem', shadow: 'none' },
      modal: { borderRadius: '{border.radius.lg}', padding: '1.25rem', shadow: 'none' },
      navigation: { shadow: 'none' },
    },
    colorScheme: {
      light: {
        surface: {
          0: 'var(--surface)',
          50: 'var(--ground)',
          100: mix('--ground', '--line', 50),
          200: 'var(--line)',
          300: mix('--line', '--muted', 65),
          400: mix('--line', '--muted', 30),
          500: 'var(--muted)',
          600: mix('--muted', '--ink', 65),
          700: mix('--muted', '--ink', 35),
          800: mix('--muted', '--ink', 15),
          900: 'var(--ink)',
          950: shade('--ink', 70),
        },
        primary: {
          color: '{primary.600}',
          contrastColor: '{surface.0}',
          hoverColor: '{primary.700}',
          activeColor: '{primary.800}',
        },
        highlight: {
          background: '{primary.100}',
          focusBackground: '{primary.200}',
          color: '{text.color}',
          focusColor: '{text.color}',
        },
        text: {
          color: '{surface.900}',
          hoverColor: '{surface.950}',
          mutedColor: '{surface.500}',
          hoverMutedColor: '{surface.600}',
        },
        formField: {
          borderColor: '{surface.200}',
          hoverBorderColor: '{surface.400}',
          color: '{text.color}',
          placeholderColor: '{text.muted.color}',
          shadow: 'none',
        },
      },
    },
  },
  components: {
    // Buttons as before: semibold, 8px × 14px, outlined ones bordered in the full accent
    button: {
      // Link buttons read as inline links: flush with the text around them and, unless sized, in its size
      // (so a link in a table row keeps the row's height)
      css: '.p-button-link { padding: 0; } .p-button-link:not(.p-button-sm):not(.p-button-lg) { font-size: inherit; line-height: inherit; }',
      root: { paddingX: '14px', paddingY: '8px', label: { fontWeight: '600' } },
      colorScheme: {
        light: {
          outlined: {
            primary: { borderColor: '{primary.color}' },
            danger: { borderColor: '{red.500}' },
          },
        },
      },
    },
    // Tables as before: compact 8px × 12px cells, muted semibold headers, line-coloured rules
    datatable: {
      headerCell: { padding: '8px 12px', color: '{text.muted.color}' },
      columnTitle: { fontWeight: '600' },
      bodyCell: { padding: '8px 12px' },
    },
    // Audit timeline: entries as tall as their content, flat markers
    timeline: {
      event: { minHeight: '0' },
      eventMarker: { content: { insetShadow: 'none' } },
    },
    // Cards are bordered panels, not shadowed. styleClass="fill" makes one fill its slot (cards in a row line up)
    card: {
      root: { borderRadius: '{border.radius.lg}', shadow: 'none' },
      body: { padding: '14px 16px', gap: '0' },
      css: '.p-card { border: 1px solid var(--p-content-border-color); } .p-card.fill { height: 100%; }',
    },
    // Notices: info is the accent-soft note; warn and error are --warn text on the surface with a --warn border
    message: {
      root: { borderRadius: '{border.radius.md}' },
      content: { padding: '10px 12px' },
      text: { fontSize: '1rem', fontWeight: '400' },
      colorScheme: {
        light: {
          info: {
            background: '{primary.100}',
            borderColor: '{primary.100}',
            color: '{text.color}',
            shadow: 'none',
          },
          warn: {
            background: '{surface.0}',
            borderColor: '{orange.500}',
            color: '{orange.500}',
            shadow: 'none',
          },
          error: {
            background: '{surface.0}',
            borderColor: '{red.500}',
            color: '{red.500}',
            shadow: 'none',
          },
        },
      },
    },
    // The old status pill: small, semibold, 4px corners; primary text in the accent itself
    tag: {
      root: {
        fontSize: '0.75rem',
        fontWeight: '600',
        padding: '2px 8px',
        borderRadius: '{border.radius.sm}',
      },
      // Warn (the mock-data badge, drafts, Ineligible) is the --warn token itself on a light tint: 4.7:1
      colorScheme: {
        light: {
          primary: { background: '{primary.100}', color: '{primary.600}' },
          warn: { background: '{orange.50}', color: '{orange.500}' },
        },
      },
    },
  },
});

function warnScale() {
  return {
    50: tint('--warn', 8),
    100: tint('--warn', 18),
    200: tint('--warn', 36),
    300: tint('--warn', 54),
    400: tint('--warn', 76),
    500: 'var(--warn)',
    600: shade('--warn', 86),
    700: shade('--warn', 72),
    800: shade('--warn', 58),
    900: shade('--warn', 44),
    950: shade('--warn', 28),
  };
}

/** Steps between tokens are mixed from them in CSS, so changing a token in styles.scss moves its whole scale. */
function tint(token: string, percent: number): string {
  return `color-mix(in srgb, var(${token}) ${percent}%, white)`;
}

function shade(token: string, percent: number): string {
  return `color-mix(in srgb, var(${token}) ${percent}%, black)`;
}

function mix(a: string, b: string, percentOfA: number): string {
  return `color-mix(in srgb, var(${a}) ${percentOfA}%, var(${b}))`;
}
