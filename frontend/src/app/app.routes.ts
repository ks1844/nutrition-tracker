import { Routes } from '@angular/router';
import { profileGuard } from './services/profile.service';

// 画面 ID は docs/08.画面設計.md に対応（SCR-01〜08）。
export const routes: Routes = [
  { path: '', canActivate: [profileGuard], loadComponent: () => import('./pages/home.page').then((m) => m.HomePage) }, // SCR-02
  { path: 'profile', loadComponent: () => import('./pages/profile.page').then((m) => m.ProfilePage) }, // SCR-01
  { path: 'record', canActivate: [profileGuard], loadComponent: () => import('./pages/record.page').then((m) => m.RecordPage) }, // SCR-03
  { path: 'record/:id', canActivate: [profileGuard], loadComponent: () => import('./pages/record.page').then((m) => m.RecordPage) }, // SCR-03（編集）
  { path: 'menus', canActivate: [profileGuard], loadComponent: () => import('./pages/menus.page').then((m) => m.MenusPage) }, // SCR-04
  { path: 'menus/new', canActivate: [profileGuard], loadComponent: () => import('./pages/menu-wizard.page').then((m) => m.MenuWizardPage) }, // SCR-05
  { path: 'menus/:id/edit', canActivate: [profileGuard], loadComponent: () => import('./pages/menu-wizard.page').then((m) => m.MenuWizardPage) }, // SCR-05
  { path: 'dashboard', canActivate: [profileGuard], loadComponent: () => import('./pages/dashboard.page').then((m) => m.DashboardPage) }, // SCR-06
  { path: 'trend', canActivate: [profileGuard], loadComponent: () => import('./pages/trend.page').then((m) => m.TrendPage) }, // SCR-07
  { path: 'nutrients/:code', canActivate: [profileGuard], loadComponent: () => import('./pages/nutrient-detail.page').then((m) => m.NutrientDetailPage) }, // SCR-08
  { path: '**', redirectTo: '' },
];
