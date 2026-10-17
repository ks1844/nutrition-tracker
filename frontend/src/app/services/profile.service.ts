import { Injectable, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Profile } from '../types/api';
import { ProfileApi } from '../api/profile.api';
import { isNotFound } from '../api/api-client';

/** プロフィールの保持。未登録（API-04 が 404）なら SCR-01 へ誘導する。 */
@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly api = inject(ProfileApi);
  readonly profile = signal<Profile | null>(null);
  readonly loaded = signal(false);

  /** 登録済みなら true。 */
  async ensureLoaded(force = false): Promise<boolean> {
    if (this.loaded() && !force) return this.profile() !== null;
    try {
      this.profile.set(await this.api.get());
    } catch (e) {
      if (!isNotFound(e)) throw e;
      this.profile.set(null);
    }
    this.loaded.set(true);
    return this.profile() !== null;
  }

  set(p: Profile | null): void {
    this.profile.set(p);
    this.loaded.set(true);
  }
}

export const profileGuard: CanActivateFn = async () => {
  const svc = inject(ProfileService);
  const router = inject(Router);
  try {
    if (await svc.ensureLoaded()) return true;
  } catch {
    return true; // 取得に失敗しても画面側でエラー表示する
  }
  return router.createUrlTree(['/profile'], { queryParams: { first: 1 } });
};
