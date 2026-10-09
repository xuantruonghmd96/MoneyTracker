import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { LanguageService } from '../../core/language.service';
import { LanguageSelectorComponent } from '../../shared/language-selector.component';

@Component({
  selector: 'app-account',
  imports: [LanguageSelectorComponent],
  template: `
    <section class="account-page">
      <header>
        <p class="eyebrow">{{ language.t('account.eyebrow') }}</p>
        <h1>{{ language.t('account.title') }}</h1>
        <p class="subtitle">{{ language.t('account.subtitle') }}</p>
      </header>

      <div class="profile-card">
        <span class="avatar" aria-hidden="true">{{ initials() }}</span>
        <div class="identity">
          <strong>{{ user()?.displayName }}</strong>
          <span>{{ user()?.email }}</span>
          <app-language-selector />
        </div>
      </div>

      <section class="session-card">
        <div>
          <h2>{{ language.t('account.signOut') }}</h2>
          <p>{{ language.t('account.signOutDescription') }}</p>
        </div>
        <button type="button" [disabled]="loggingOut()" (click)="logout()">
          {{ loggingOut() ? language.t('account.signingOut') : language.t('account.logOut') }}
        </button>
      </section>
    </section>
  `,
  styles: `
    :host {
      display: block;
    }
    .account-page {
      display: grid;
      gap: 19px;
      max-width: 650px;
    }
    header .eyebrow {
      margin: 0 0 7px;
      color: var(--subtle);
      font-size: 9px;
      font-weight: 700;
      letter-spacing: 1.1px;
    }
    h1 {
      margin: 0;
      color: var(--text);
      font-size: clamp(22px, 2.4vw, 29px);
      font-weight: 600;
      letter-spacing: -1px;
    }
    .subtitle {
      margin: 7px 0 0;
      color: var(--muted);
      font-size: 12px;
    }
    .profile-card,
    .session-card {
      display: flex;
      align-items: center;
      gap: 13px;
      padding: 17px;
      border: 1px solid var(--border);
      border-radius: 12px;
      background: var(--surface);
    }
    .avatar {
      display: grid;
      width: 44px;
      height: 44px;
      flex: 0 0 auto;
      place-items: center;
      border-radius: 50%;
      background: #304038;
      color: #c5e2b8;
      font-size: 15px;
      font-weight: 700;
    }
    .identity {
      display: grid;
      min-width: 0;
      justify-items: start;
      gap: 4px;
    }
    .identity strong {
      overflow: hidden;
      color: #e6ebe2;
      font-size: 13px;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .identity span {
      overflow: hidden;
      color: var(--muted);
      font-size: 11px;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .session-card {
      justify-content: space-between;
      gap: 16px;
    }
    h2 {
      margin: 0;
      color: #e6ebe2;
      font-size: 12px;
      font-weight: 600;
    }
    .session-card p {
      margin: 5px 0 0;
      color: var(--muted);
      font-size: 10px;
      line-height: 1.5;
    }
    button {
      min-height: 36px;
      flex: 0 0 auto;
      padding: 0 12px;
      border: 1px solid rgba(233, 140, 131, 0.22);
      border-radius: 8px;
      background: rgba(233, 140, 131, 0.08);
      color: #efa39c;
      cursor: pointer;
      font-size: 10px;
      font-weight: 600;
    }
    button:disabled {
      cursor: wait;
      opacity: 0.65;
    }
    @media (max-width: 600px) {
      .account-page {
        gap: 14px;
      }
      .profile-card,
      .session-card {
        padding: 14px;
      }
    }
  `,
})
export class AccountComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly language = inject(LanguageService);
  readonly user = this.auth.user;
  readonly loggingOut = signal(false);
  readonly initials = computed(
    () => this.user()?.displayName.trim().charAt(0).toUpperCase() || 'M',
  );

  logout(): void {
    if (this.loggingOut()) return;
    this.loggingOut.set(true);
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl('/login'),
      error: () => {
        this.auth.clearSession();
        void this.router.navigateByUrl('/login');
      },
    });
  }
}
