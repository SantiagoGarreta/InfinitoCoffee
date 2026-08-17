import { isPlatformBrowser } from '@angular/common';
import { inject, InjectionToken, PLATFORM_ID } from '@angular/core';

import { environment } from '../../../environments/environment';

export interface AppRuntimeConfig {
  apiBaseUrl: string;
  signalRHubUrl: string;
  pickupSignalRHubUrl: string;
}

declare global {
  interface Window {
    __infinitoCoffeeConfig?: Partial<AppRuntimeConfig>;
  }
}

const defaultConfig: AppRuntimeConfig = {
  apiBaseUrl: environment.apiBaseUrl,
  signalRHubUrl: environment.signalRHubUrl,
  pickupSignalRHubUrl: derivePickupHubUrl(environment.signalRHubUrl),
};

export const APP_RUNTIME_CONFIG = new InjectionToken<AppRuntimeConfig>(
  'APP_RUNTIME_CONFIG',
  {
    providedIn: 'root',
    factory: () => {
      if (!isPlatformBrowser(inject(PLATFORM_ID))) {
        return defaultConfig;
      }

      const signalRHubUrl = normalizeUrl(
        window.__infinitoCoffeeConfig?.signalRHubUrl,
        defaultConfig.signalRHubUrl,
      );

      return {
        apiBaseUrl: normalizeUrl(window.__infinitoCoffeeConfig?.apiBaseUrl, defaultConfig.apiBaseUrl),
        signalRHubUrl,
        pickupSignalRHubUrl: derivePickupHubUrl(signalRHubUrl),
      };
    },
  },
);

function normalizeUrl(value: string | undefined, fallback: string): string {
  return typeof value === 'string' && value.trim().length > 0
    ? value.trim().replace(/\/$/, '')
    : fallback;
}

function derivePickupHubUrl(ordersHubUrl: string): string {
  const url = new URL(ordersHubUrl);
  const parentPath = url.pathname.replace(/\/+$/, '').replace(/\/[^/]+$/, '');
  url.pathname = `${parentPath}/pickup`;
  return url.toString().replace(/\/$/, '');
}
