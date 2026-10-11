import { Routes } from '@angular/router';

/** The vendor app: a shell with three tabs. `/vendor` always opens on the requests tab. */
export const VENDOR_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./vendor-shell').then((m) => m.VendorShell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'requests' },
      {
        path: 'requests',
        loadComponent: () => import('./vendor-requests').then((m) => m.VendorRequests),
        title: 'คำขอของฉัน · BKK Smart Market',
        data: { heading: 'คำขอของฉัน' },
      },
      {
        path: 'spaces',
        loadComponent: () => import('./vendor-spaces').then((m) => m.VendorSpaces),
        title: 'หาพื้นที่ · BKK Smart Market',
        data: { heading: 'หาพื้นที่ขายของ' },
      },
      {
        path: 'assistant',
        loadComponent: () => import('./assistant-chat').then((m) => m.AssistantChat),
        title: 'ถามระเบียบ · BKK Smart Market',
        data: { heading: 'ถามระเบียบ' },
      },
    ],
  },
];
