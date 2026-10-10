import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'vendor' },
  { path: 'vendor', loadChildren: () => import('./vendor/vendor.routes').then((m) => m.VENDOR_ROUTES) },
  { path: 'portal', loadChildren: () => import('./portal/portal.routes').then((m) => m.PORTAL_ROUTES) },
  { path: 'dashboard', loadChildren: () => import('./dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES) },
  { path: '**', redirectTo: 'vendor' },
];
