import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** 【使い捨て】プロトタイプ専用: Mock のデータ初期化。実 API には存在しない。 */
@Injectable({ providedIn: 'root' })
export class MockAdminApi {
  private readonly http = inject(HttpClient);

  reset(withProfile: boolean): Promise<void> {
    return firstValueFrom(this.http.post<void>('/api/__mock__/reset', { withProfile }));
  }
}
