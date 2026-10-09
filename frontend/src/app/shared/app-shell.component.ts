import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { LanguageService } from '../core/language.service';
import { LanguageSelectorComponent } from './language-selector.component';

@Component({
  selector: 'app-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, LanguageSelectorComponent],
  template: `
    <div class="app-shell">
      <aside class="sidebar">
        <a class="brand" routerLink="/" [attr.aria-label]="language.t('brand.home')">
          <span class="brand-mark" aria-hidden="true">↗</span>
          <span>money<span class="brand-light">tracker</span></span>
        </a>
        <p class="workspace-label">{{ language.t('nav.workspace') }}</p>
        <nav class="desktop-nav" [attr.aria-label]="language.t('nav.main')">
          <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">
            <span aria-hidden="true">⇄</span> {{ language.t('nav.transactions') }}
          </a>
          <a routerLink="/categories" routerLinkActive="active">
            <span aria-hidden="true">◈</span> {{ language.t('nav.categories') }} <small>{{ language.t('nav.soon') }}</small>
          </a>
          <a routerLink="/wallets" routerLinkActive="active">
            <span aria-hidden="true">▰</span> {{ language.t('nav.wallets') }}
          </a>
          <a routerLink="/account" routerLinkActive="active">
            <span aria-hidden="true">○</span> {{ language.t('nav.account') }} <small>{{ language.t('nav.soon') }}</small>
          </a>
        </nav>
        <div class="sidebar-spacer"></div>
        <app-language-selector />
        <div class="sidebar-message">
          <span>✳</span>
          <p>{{ language.t('nav.tagline') }}</p>
        </div>
        <div class="profile">
          <span class="avatar">{{ initials() }}</span>
          <span class="profile-copy">
            <strong>{{ displayName() }}</strong>
            <span>{{ language.t('nav.personalAccount') }}</span>
          </span>
          <button type="button" class="logout-button" (click)="logout()" [attr.aria-label]="language.t('nav.signOut')">↗</button>
        </div>
      </aside>

      <div class="main-area">
        <main class="page-content"><router-outlet /></main>
      </div>

      <nav class="bottom-nav" [attr.aria-label]="language.t('nav.mobile')">
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">
          <span aria-hidden="true">⇄</span><span>{{ language.t('nav.transactions') }}</span>
        </a>
        <a routerLink="/categories" routerLinkActive="active">
          <span aria-hidden="true">◈</span><span>{{ language.t('nav.categories') }}</span>
        </a>
        <a class="add-button" routerLink="/add-transaction" [attr.aria-label]="language.t('coming.transactionTitle')">
          <span aria-hidden="true">+</span>
        </a>
        <a routerLink="/wallets" routerLinkActive="active">
          <span aria-hidden="true">▰</span><span>{{ language.t('nav.wallets') }}</span>
        </a>
        <a routerLink="/account" routerLinkActive="active">
          <span aria-hidden="true">○</span><span>{{ language.t('nav.account') }}</span>
        </a>
      </nav>
    </div>
  `,
  styleUrl: './app-shell.scss',
})
export class AppShellComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly language = inject(LanguageService);
  readonly displayName = computed(() => this.auth.user()?.displayName ?? this.language.t('nav.myAccount'));
  readonly initials = computed(() => this.displayName().trim().charAt(0).toUpperCase() || 'M');

  logout(): void {
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl('/login'),
      error: () => void this.router.navigateByUrl('/login'),
    });
  }
}
