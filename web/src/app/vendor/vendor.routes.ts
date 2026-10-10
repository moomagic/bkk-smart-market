import { Routes } from '@angular/router';

export const VENDOR_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./vendor-home').then((m) => m.VendorHome), title: 'ผู้ค้า · BKK Smart Market' },
];
