import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { environment } from '../environments/environment';
import { mockApiInterceptor } from './api/mock/mock-api.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // 【プロトタイプ】Mock API を使う（environment.useMockApi）。結線時は false にする。
    provideHttpClient(withInterceptors(environment.useMockApi ? [mockApiInterceptor] : [])),
  ],
};
