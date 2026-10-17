// 【使い捨て】/api への要求を Mock で処理する（docs/13.開発手順.md フェーズ 1）。

import { HttpErrorResponse, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { delay, of, throwError } from 'rxjs';
import { MockHttpError, handle } from './mock-handlers';

export const mockApiInterceptor: HttpInterceptorFn = (req, next) => {
  const url = new URL(req.urlWithParams, 'http://mock.local');
  if (!url.pathname.startsWith('/api')) return next(req);
  try {
    const res = handle({ method: req.method, path: url.pathname, query: url.searchParams, body: req.body });
    return of(new HttpResponse({ status: res.status, body: res.body, url: req.url })).pipe(delay(120));
  } catch (e) {
    if (e instanceof MockHttpError) {
      return throwError(() => new HttpErrorResponse({ status: e.status, error: e.body, url: req.url })).pipe(delay(120));
    }
    throw e;
  }
};
