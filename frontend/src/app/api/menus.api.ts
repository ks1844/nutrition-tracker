import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MenuDetail, MenuInput, MenuPreview, MenuSummary } from '../types/api';

/** API-06〜API-12 */
@Injectable({ providedIn: 'root' })
export class MenusApi {
  private readonly http = inject(HttpClient);

  list(): Promise<MenuSummary[]> {
    return firstValueFrom(this.http.get<MenuSummary[]>('/api/menus'));
  }

  get(id: number): Promise<MenuDetail> {
    return firstValueFrom(this.http.get<MenuDetail>(`/api/menus/${id}`));
  }

  create(input: MenuInput): Promise<MenuDetail> {
    return firstValueFrom(this.http.post<MenuDetail>('/api/menus', input));
  }

  update(id: number, input: MenuInput): Promise<MenuDetail> {
    return firstValueFrom(this.http.put<MenuDetail>(`/api/menus/${id}`, input));
  }

  remove(id: number): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/menus/${id}`));
  }

  duplicate(id: number): Promise<MenuDetail> {
    return firstValueFrom(this.http.post<MenuDetail>(`/api/menus/${id}/duplicate`, {}));
  }

  preview(input: Partial<MenuInput>): Promise<MenuPreview> {
    return firstValueFrom(this.http.post<MenuPreview>('/api/menus/preview', input));
  }
}
