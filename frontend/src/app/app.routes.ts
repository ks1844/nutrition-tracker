import { Routes } from '@angular/router';
import { profileGuard } from './services/profile.service';
import { recordLeaveGuard } from './pages/record/record-leave.guard';

// 画面 ID は docs/08.画面設計.md に対応（SCR-01〜08）。
export const routes: Routes = [
  {
    path: '',
    canActivate: [profileGuard],
    loadComponent: () => import('./pages/home/home').then((m) => m.HomePage),
  }, // SCR-02
  {
    path: 'profile',
    loadComponent: () => import('./pages/profile/profile').then((m) => m.ProfilePage),
  }, // SCR-01
  {
    path: 'record',
    canActivate: [profileGuard],
    canDeactivate: [recordLeaveGuard],
    loadComponent: () => import('./pages/record/record').then((m) => m.RecordPage),
  }, // SCR-03
  {
    path: 'record/:id',
    canActivate: [profileGuard],
    canDeactivate: [recordLeaveGuard],
    loadComponent: () => import('./pages/record/record').then((m) => m.RecordPage),
  }, // SCR-03（編集）
  {
    path: 'menus',
    canActivate: [profileGuard],
    loadComponent: () => import('./pages/menus/menus').then((m) => m.MenusPage),
  }, // SCR-04
  {
    path: 'menus/new',
    canActivate: [profileGuard],
    loadComponent: () => import('./pages/menu-wizard/menu-wizard').then((m) => m.MenuWizardPage),
  }, // SCR-05
  {
    path: 'menus/:id/edit',
    canActivate: [profileGuard],
    loadComponent: () => import('./pages/menu-wizard/menu-wizard').then((m) => m.MenuWizardPage),
  }, // SCR-05
  {
    path: 'dashboard',
    canActivate: [profileGuard],
    loadComponent: () => import('./pages/dashboard/dashboard').then((m) => m.DashboardPage),
  }, // SCR-06
  {
    path: 'trend',
    canActivate: [profileGuard],
    loadComponent: () => import('./pages/trend/trend').then((m) => m.TrendPage),
  }, // SCR-07
  {
    path: 'nutrients/:code',
    canActivate: [profileGuard],
    loadComponent: () =>
      import('./pages/nutrient-detail/nutrient-detail').then((m) => m.NutrientDetailPage),
  }, // SCR-08
  { path: '**', redirectTo: '' },
];
