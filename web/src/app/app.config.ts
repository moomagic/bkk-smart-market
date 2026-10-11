import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { providePrimeNG } from 'primeng/config';

import { routes } from './app.routes';
import { demoHeadersInterceptor } from './core/demo-headers.interceptor';
import { SmartMarketPreset } from './shared/theme/smart-market-preset';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([demoHeadersInterceptor])),
    providePrimeNG({
      theme: {
        preset: SmartMarketPreset,
        options: {
          // Light only, as the tokens are (color-scheme: light).
          darkModeSelector: 'none',
          // PrimeNG styles sit in a layer, so the app's own (unlayered) styles win without !important.
          cssLayer: { name: 'primeng', order: 'primeng' },
        },
      },
    }),
  ],
};
