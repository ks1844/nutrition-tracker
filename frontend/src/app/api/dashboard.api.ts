import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { DailyDashboard, WeeklyDashboard } from '../types/api';

/** API-17, API-18 */
@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);

  daily(date: string): Promise<DailyDashboard> {
    return firstValueFrom(this.http.get<DailyDashboard>('/api/dashboard/daily', { params: { date } }));
  }

  weekly(endDate: string): Promise<WeeklyDashboard> {
    return firstValueFrom(this.http.get<WeeklyDashboard>('/api/dashboard/weekly', { params: { endDate } }));
  }
}
