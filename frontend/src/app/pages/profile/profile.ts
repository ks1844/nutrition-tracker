// SCR-01 プロフィール設定（FR-001, FR-002）
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ProfileApi } from '../../api/profile.api';
import { MockAdminApi } from '../../api/mock.api';
import { fieldErrors, toApiError } from '../../api/api-client';
import { Goal, ProfileInput, Sex } from '../../types/api';
import { ProfileService } from '../../services/profile.service';
import { UiService } from '../../services/ui.service';
import { ACTIVITY_LABEL, GOAL_LABEL, fmtNum } from '../../services/format';
import { todayString } from '../../services/date-util';
import { PageHeader } from '../../components/page-header/page-header';
import { Disclaimer } from '../../components/disclaimer/disclaimer';
import { Icon } from '../../components/icon/icon';
import { ChipGroup, ChipOption } from '../../components/chip-group/chip-group';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-profile-page',
  imports: [PageHeader, Disclaimer, Icon, ChipGroup],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'page' },
  templateUrl: './profile.html',
})
export class ProfilePage implements OnInit {
  readonly first = input(false, { transform: (v: unknown) => v === '1' || v === 1 || v === true });

  private readonly api = inject(ProfileApi);
  private readonly mockApi = inject(MockAdminApi);
  private readonly svc = inject(ProfileService);
  private readonly ui = inject(UiService);
  private readonly router = inject(Router);

  protected readonly mock = environment.useMockApi;
  protected readonly today = todayString();
  protected readonly sexes: { v: Sex; l: string }[] = [{ v: 'female', l: '女性' }, { v: 'male', l: '男性' }];
  protected readonly goals: Goal[] = ['skin', 'muscle', 'both'];
  protected readonly goalLabel = GOAL_LABEL;
  protected readonly levelOptions: ChipOption[] = [1, 2, 3, 4].map((lv) => ({ value: lv, label: ACTIVITY_LABEL[lv] ?? String(lv) }));

  protected readonly form = signal<ProfileInput>({
    sex: 'female', birthDate: '', heightCm: 0, weightKg: 0, activityLevel: 2, goal: 'both', energyTargetKcal: null,
  });
  protected readonly override = signal(false);
  protected readonly errs = signal<Record<string, string>>({});
  protected readonly saving = signal(false);

  /** 入力途中でも目標を即時再計算して見せる（式は docs/06.スコアリング仕様.md §2。確定値は保存後にサーバーが返す）。 */
  protected readonly energyText = computed(() => {
    const f = this.form();
    if (this.override() && f.energyTargetKcal) return f.energyTargetKcal.toLocaleString('ja-JP');
    if (!f.birthDate || !f.heightCm || !f.weightKg) return '—';
    const b = new Date(f.birthDate);
    const t = new Date();
    let age = t.getFullYear() - b.getFullYear();
    if (t.getMonth() < b.getMonth() || (t.getMonth() === b.getMonth() && t.getDate() < b.getDate())) age -= 1;
    const bmr = 10 * f.weightKg + 6.25 * f.heightCm - 5 * age + (f.sex === 'male' ? 5 : -161);
    const factor = [1.2, 1.375, 1.55, 1.725][f.activityLevel - 1] ?? 1.2;
    return Math.round(bmr * factor).toLocaleString('ja-JP');
  });
  protected readonly proteinText = computed(() => {
    const f = this.form();
    if (!f.weightKg) return '—';
    return fmtNum(f.weightKg * ({ skin: 1.0, both: 1.4, muscle: 1.6 }[f.goal]), 1);
  });

  async ngOnInit(): Promise<void> {
    try {
      if (await this.svc.ensureLoaded(true)) {
        const p = this.svc.profile();
        if (p) {
          const { targets: _t, ...input } = p;
          this.form.set({ ...input });
          this.override.set(p.energyTargetKcal !== null);
        }
      }
    } catch (e) {
      this.ui.error(toApiError(e).message);
    }
  }

  protected patch(p: Partial<ProfileInput>): void {
    this.form.update((f) => ({ ...f, ...p }));
  }

  protected num(ev: Event): number {
    const v = (ev.target as HTMLInputElement).value;
    return v === '' ? 0 : Number(v);
  }

  protected toggleOverride(on: boolean): void {
    this.override.set(on);
    if (!on) this.patch({ energyTargetKcal: null });
  }

  protected async save(ev: Event): Promise<void> {
    ev.preventDefault();
    this.saving.set(true);
    this.errs.set({});
    try {
      const input = { ...this.form(), energyTargetKcal: this.override() ? this.form().energyTargetKcal : null };
      const saved = await this.api.save(input);
      this.svc.set(saved);
      this.ui.toast('プロフィールを保存しました');
      await this.router.navigateByUrl('/');
    } catch (e) {
      this.errs.set(fieldErrors(e));
      this.ui.error(toApiError(e).message || '保存できませんでした');
    } finally {
      this.saving.set(false);
    }
  }

  protected async resetMock(withProfile: boolean): Promise<void> {
    await this.mockApi.reset(withProfile);
    this.svc.set(null);
    this.svc.loaded.set(false);
    this.ui.toast(withProfile ? 'サンプルデータに戻しました' : 'プロフィール未登録にしました', 'info');
    await this.router.navigateByUrl(withProfile ? '/' : '/profile?first=1');
  }
}
