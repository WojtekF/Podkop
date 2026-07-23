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
];
