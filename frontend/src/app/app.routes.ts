import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/login.component').then((module) => module.LoginComponent),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/register.component').then((module) => module.RegisterComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./shared/app-shell.component').then((module) => module.AppShellComponent),
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () =>
          import('./features/transactions/transactions.component').then(
            (module) => module.TransactionsComponent,
          ),
      },
      {
        path: 'account',
        loadComponent: () =>
          import('./features/account/account.component').then(
            (module) => module.AccountComponent,
          ),
      },
      {
        path: 'wallets',
        loadComponent: () =>
          import('./features/wallets/wallets.component').then(
            (module) => module.WalletsComponent,
          ),
      },
      ...['categories', 'report', 'add-transaction'].map((path) => ({
        path,
        loadComponent: () =>
          import('./shared/coming-soon.component').then((module) => module.ComingSoonComponent),
        data: { feature: path },
      })),
    ],
  },
  { path: '**', redirectTo: '' },
];
