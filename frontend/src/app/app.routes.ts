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
        path: 'transactions/new',
        loadComponent: () =>
          import('./features/transactions/transaction-form.component').then(
            (module) => module.TransactionFormComponent,
          ),
      },
      {
        path: 'transactions/:id/edit',
        loadComponent: () =>
          import('./features/transactions/transaction-form.component').then(
            (module) => module.TransactionFormComponent,
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
      {
        path: 'wallets/new',
        loadComponent: () =>
          import('./features/wallets/wallets.component').then(
            (module) => module.WalletsComponent,
          ),
      },
      {
        path: 'wallets/:id/edit',
        loadComponent: () =>
          import('./features/wallets/wallets.component').then(
            (module) => module.WalletsComponent,
          ),
      },
      {
        path: 'categories',
        loadComponent: () =>
          import('./features/categories/categories.component').then(
            (module) => module.CategoriesComponent,
          ),
      },
      {
        path: 'categories/new',
        loadComponent: () =>
          import('./features/categories/categories.component').then(
            (module) => module.CategoriesComponent,
          ),
      },
      {
        path: 'categories/:id/edit',
        loadComponent: () =>
          import('./features/categories/categories.component').then(
            (module) => module.CategoriesComponent,
          ),
      },
      {
        path: 'report',
        loadComponent: () =>
          import('./shared/coming-soon.component').then((module) => module.ComingSoonComponent),
        data: { feature: 'report' },
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
