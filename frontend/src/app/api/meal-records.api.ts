import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CopyResult, MealRecordInput, MealRecordSaved, MealRecordsOfDay, RecentItems } from '../types/api';

/** API-13〜API-16, API-20, API-21 */
@Injectable({ providedIn: 'root' })
export class MealRecordsApi {
  private readonly http = inject(HttpClient);

  ofDay(date: string): Promise<MealRecordsOfDay> {
    return firstValueFrom(this.http.get<MealRecordsOfDay>('/api/meal-records', { params: { date } }));
  }

  create(input: MealRecordInput): Promise<MealRecordSaved> {
    return firstValueFrom(this.http.post<MealRecordSaved>('/api/meal-records', input));
  }

  update(id: number, input: MealRecordInput): Promise<MealRecordSaved> {
    return firstValueFrom(this.http.put<MealRecordSaved>(`/api/meal-records/${id}`, input));
  }

  remove(id: number): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/meal-records/${id}`));
  }

  recent(limit = 10): Promise<RecentItems> {
    return firstValueFrom(this.http.get<RecentItems>('/api/recent-items', { params: { limit } }));
  }

  copyDay(fromDate: string, toDate: string, allowExisting: boolean): Promise<CopyResult> {
    return firstValueFrom(this.http.post<CopyResult>('/api/meal-records/copy', { fromDate, toDate, allowExisting }));
  }
}
