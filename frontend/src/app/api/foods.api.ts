import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { FoodDetail, FoodSearchResult } from '../types/api';

/** API-01, API-02 */
@Injectable({ providedIn: 'root' })
export class FoodsApi {
  private readonly http = inject(HttpClient);

  search(q: string, limit = 20): Promise<FoodSearchResult> {
    return firstValueFrom(this.http.get<FoodSearchResult>('/api/foods', { params: { q, limit } }));
  }

  get(id: number): Promise<FoodDetail> {
    return firstValueFrom(this.http.get<FoodDetail>(`/api/foods/${id}`));
  }
}
