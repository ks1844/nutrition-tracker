import { Injectable, inject, signal } from '@angular/core';
import { NutrientsApi } from '../api/nutrients.api';
import { NutrientDef } from '../types/api';

/** 栄養素マスタ（API-03）を 1 度だけ読み込んで共有する。 */
@Injectable({ providedIn: 'root' })
export class NutrientCatalog {
  private readonly api = inject(NutrientsApi);
  readonly list = signal<NutrientDef[]>([]);
  private promise: Promise<NutrientDef[]> | null = null;

  load(): Promise<NutrientDef[]> {
    this.promise ??= this.api.list().then((l) => {
      this.list.set(l);
      return l;
    });
    return this.promise;
  }

  def(code: string): NutrientDef | undefined {
    return this.list().find((n) => n.code === code);
  }
}
