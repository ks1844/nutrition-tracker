// API 呼び出しの共通部品。エラー形式は docs/09.API仕様.md §1.2。

import { HttpErrorResponse } from '@angular/common/http';
import { ApiError } from '../types/api';

export function toApiError(e: unknown): ApiError {
  if (e instanceof HttpErrorResponse) {
    const body = e.error as Partial<ApiError> | null;
    if (body && typeof body === 'object' && typeof body.code === 'string') {
      return { code: body.code, message: body.message ?? '', details: body.details ?? [] };
    }
  }
  return { code: 'INTERNAL_ERROR', message: '通信に失敗しました。時間をおいて再度お試しください', details: [] };
}

export function isNotFound(e: unknown): boolean {
  return toApiError(e).code === 'NOT_FOUND';
}

export function fieldErrors(e: unknown): Record<string, string> {
  const out: Record<string, string> = {};
  for (const d of toApiError(e).details) if (!(d.field in out)) out[d.field] = d.reason;
  return out;
}
