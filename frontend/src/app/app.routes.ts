import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('../main-page/main-page').then((m) => m.MainPage),
  },
  {
    path: 'finding/:id',
    loadComponent: () => import('../finding-detail/finding-detail').then((m) => m.FindingDetail),
  },
  {
    path: 'statute',
    loadComponent: () => import('../documents/statute-page/statute-page').then((m) => m.StatutePage),
  },
  {
    path: 'privacy-policy',
    loadComponent: () =>
      import('../documents/privacy-policy-page/privacy-policy-page').then(
        (m) => m.PrivacyPolicyPage,
      ),
  },
];
