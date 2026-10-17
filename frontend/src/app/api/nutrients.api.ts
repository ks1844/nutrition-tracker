import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Contributions, NutrientDef } from '../types/api';

/** API-03, API-19 */
@Injectable({ providedIn: 'root' })
export class NutrientsApi {
  private readonly http = inject(HttpClient);

  list(): Promise<NutrientDef[]> {
    return firstValueFrom(this.http.get<NutrientDef[]>('/api/nutrients'));
  }

  contributions(code: string, from: string, to: string): Promise<Contributions> {
    return firstValueFrom(this.http.get<Contributions>(`/api/nutrients/${code}/contributions`, { params: { from, to } }));
  }
}
