import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { authErrorMessage } from '../../core/auth-error-message';
import { LanguageService, TranslationKey } from '../../core/language.service';
import { LanguageSelectorComponent } from '../../shared/language-selector.component';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, LanguageSelectorComponent],
  template: `
    <main class="auth-layout">
      <section class="auth-card" aria-labelledby="page-title">
        <div class="auth-brand-row">
          <a class="brand" routerLink="/" [attr.aria-label]="language.t('brand.home')">
            <span class="brand-mark" aria-hidden="true">↗</span>
            <span>money<span class="brand-light">tracker</span></span>
          </a>
          <app-language-selector />
        </div>
        <div class="auth-heading">
          <p class="eyebrow">{{ language.t('auth.freshStart') }}</p>
          <h1 id="page-title">{{ language.t('auth.registerTitleFirst') }}<br />{{ language.t('auth.registerTitleSecond') }}</h1>
          <p class="auth-description">{{ language.t('auth.registerDescription') }}</p>
        </div>
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          @if (errorMessage()) {
            <div class="form-alert" role="alert">{{ errorMessage() }}</div>
          }
          <label for="displayName">{{ language.t('auth.name') }}</label>
          <input
            id="displayName"
            type="text"
            formControlName="displayName"
            autocomplete="name"
            [placeholder]="language.t('auth.namePlaceholder')"
            [attr.aria-invalid]="hasError('displayName')"
          />
          @if (hasError('displayName')) {
            <span class="field-error">{{ language.t('auth.requiredName') }}</span>
          }
          <label for="email">{{ language.t('auth.email') }}</label>
          <input
            id="email"
            type="email"
            formControlName="email"
            autocomplete="email"
            [placeholder]="language.t('auth.emailPlaceholder')"
            [attr.aria-invalid]="hasError('email')"
          />
          @if (hasError('email')) {
            <span class="field-error">{{ language.t('auth.invalidEmail') }}</span>
          }
          <label for="password">{{ language.t('auth.createPassword') }}</label>
          <input
            id="password"
            type="password"
            formControlName="password"
            autocomplete="new-password"
            [placeholder]="language.t('auth.passwordLengthPlaceholder')"
            [attr.aria-invalid]="hasError('password')"
          />
          @if (hasError('password')) {
            <span class="field-error">{{ language.t('auth.passwordLengthError') }}</span>
          }
          <button class="primary-button" type="submit" [disabled]="submitting()">
            {{ submitting() ? language.t('auth.creatingAccount') : language.t('auth.createAccountAction') }}
            @if (!submitting()) {
              <span aria-hidden="true">→</span>
            }
          </button>
        </form>
        <p class="auth-switch">
          {{ language.t('auth.alreadyHaveAccount') }}
          <a routerLink="/login">{{ language.t('auth.signIn') }}</a>
        </p>
      </section>
      <aside class="auth-aside" [attr.aria-label]="language.t('auth.overview')">
        <div class="aside-orbit orbit-one"></div>
        <div class="aside-orbit orbit-two"></div>
        <div class="aside-copy">
          <span class="aside-tag"><span></span> {{ language.t('auth.focusTag') }}</span>
          <h2>{{ language.t('auth.brandPromiseFirst') }}<br />{{ language.t('auth.brandConnective') }} <em>{{ language.t('auth.brandPromiseEmphasis') }}</em></h2>
          <p>{{ language.t('auth.brandDescription') }}</p>
          <div class="aside-note"><span>✳</span> {{ language.t('auth.smallSteps') }}</div>
        </div>
        <div class="aside-foot">{{ language.t('auth.brandFooter') }}</div>
      </aside>
    </main>
  `,
  styleUrl: './auth-page.scss',
})
export class RegisterComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly language = inject(LanguageService);
  readonly submitting = signal(false);
  private readonly errorKey = signal<TranslationKey | null>(null);
  readonly errorMessage = computed(() => {
    const key = this.errorKey();
    return key ? this.language.t(key) : '';
  });
  readonly form = this.formBuilder.nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(128)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
  });

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) return;

    this.submitting.set(true);
    this.errorKey.set(null);
    const { displayName, email, password } = this.form.getRawValue();
    this.auth.register(email, password, displayName).subscribe({
      next: () => void this.router.navigateByUrl('/'),
      error: (error: unknown) => {
        this.errorKey.set(authErrorMessage(error));
        this.submitting.set(false);
      },
    });
  }

  hasError(field: 'displayName' | 'email' | 'password'): boolean {
    return this.form.controls[field].invalid && this.form.controls[field].touched;
  }
}
