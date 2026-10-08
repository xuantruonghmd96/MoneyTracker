import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="app-shell">
      <aside class="sidebar">
        <a class="brand" routerLink="/" aria-label="MoneyTracker home">
          <span class="brand-mark" aria-hidden="true">↗</span>
          <span>money<span class="brand-light">tracker</span></span>
        </a>
        <p class="workspace-label">YOUR MONEY</p>
        <nav class="desktop-nav" aria-label="Main navigation">
          <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">
            <span aria-hidden="true">⇄</span> Transactions
          </a>
          <a routerLink="/categories" routerLinkActive="active">
            <span aria-hidden="true">◈</span> Categories <small>SOON</small>
          </a>
          <a routerLink="/report" routerLinkActive="active">
            <span aria-hidden="true">▥</span> Report <small>SOON</small>
          </a>
          <a routerLink="/account" routerLinkActive="active">
            <span aria-hidden="true">○</span> Account <small>SOON</small>
          </a>
        </nav>
        <div class="sidebar-spacer"></div>
        <div class="sidebar-message">
          <span>✳</span>
          <p>A little more clarity, every day.</p>
        </div>
        <div class="profile">
          <span class="avatar">{{ initials() }}</span>
          <span class="profile-copy">
            <strong>{{ displayName() }}</strong>
            <span>Personal account</span>
          </span>
          <button type="button" class="logout-button" (click)="logout()" aria-label="Sign out">↗</button>
        </div>
      </aside>

      <div class="main-area">
        <main class="page-content"><router-outlet /></main>
      </div>

      <nav class="bottom-nav" aria-label="Mobile navigation">
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">
          <span aria-hidden="true">⇄</span><span>Transactions</span>
        </a>
        <a routerLink="/categories" routerLinkActive="active">
          <span aria-hidden="true">◈</span><span>Categories</span>
        </a>
        <a class="add-button" routerLink="/add-transaction" aria-label="Add transaction">
          <span aria-hidden="true">+</span>
        </a>
        <a routerLink="/report" routerLinkActive="active">
          <span aria-hidden="true">▥</span><span>Report</span>
        </a>
        <a routerLink="/account" routerLinkActive="active">
          <span aria-hidden="true">○</span><span>Account</span>
        </a>
      </nav>
    </div>
  `,
  styleUrl: './app-shell.scss',
})
export class AppShellComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly displayName = computed(() => this.auth.user()?.displayName ?? 'My account');
  readonly initials = computed(() => this.displayName().trim().charAt(0).toUpperCase() || 'M');

  logout(): void {
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl('/login'),
      error: () => void this.router.navigateByUrl('/login'),
    });
  }
}
