import { Routes } from '@angular/router';

export const PORTAL_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./portal-home').then((m) => m.PortalHome), title: 'เจ้าหน้าที่ · BKK Smart Market' },
];
