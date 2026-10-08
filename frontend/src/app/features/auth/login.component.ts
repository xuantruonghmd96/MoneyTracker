import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { authErrorMessage } from '../../core/auth-error-message';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <main class="auth-layout">
      <section class="auth-card" aria-labelledby="page-title">
        <a class="brand" routerLink="/" aria-label="MoneyTracker home">
          <span class="brand-mark" aria-hidden="true">↗</span>
          <span>money<span class="brand-light">tracker</span></span>
        </a>
        <div class="auth-heading">
          <p class="eyebrow">WELCOME BACK</p>
          <h1 id="page-title">Your money,<br />all in one place.</h1>
          <p class="auth-description">Sign in to pick up where you left off.</p>
        </div>
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          @if (errorMessage()) {
            <div class="form-alert" role="alert">{{ errorMessage() }}</div>
          }
          <label for="email">Email address</label>
          <input
            id="email"
            type="email"
            formControlName="email"
            autocomplete="email"
            placeholder="you@example.com"
            [attr.aria-invalid]="hasError('email')"
          />
          @if (hasError('email')) {
            <span class="field-error">Enter a valid email address.</span>
          }
          <label for="password">Password</label>
          <input
            id="password"
            type="password"
            formControlName="password"
            autocomplete="current-password"
            placeholder="Your password"
            [attr.aria-invalid]="hasError('password')"
          />
          @if (hasError('password')) {
            <span class="field-error">Enter your password.</span>
          }
          <button class="primary-button" type="submit" [disabled]="submitting()">
            {{ submitting() ? 'Signing in…' : 'Sign in' }}
            @if (!submitting()) {
              <span aria-hidden="true">→</span>
            }
          </button>
        </form>
        <p class="auth-switch">New to MoneyTracker? <a routerLink="/register">Create an account</a></p>
      </section>
      <aside class="auth-aside" aria-label="MoneyTracker overview">
        <div class="aside-orbit orbit-one"></div>
        <div class="aside-orbit orbit-two"></div>
        <div class="aside-copy">
          <span class="aside-tag"><span></span> YOUR FINANCES, IN FOCUS</span>
          <h2>Make room<br />for <em>what matters.</em></h2>
          <p>A calmer, clearer view of your money — day by day, month by month.</p>
          <div class="aside-note"><span>✳</span> Small steps add up.</div>
        </div>
        <div class="aside-foot">A LITTLE MORE CLARITY, EVERY DAY.</div>
      </aside>
    </main>
  `,
  styleUrl: './auth-page.scss',
})
export class LoginComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly submitting = signal(false);
  readonly errorMessage = signal('');
  readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) return;

    this.submitting.set(true);
    this.errorMessage.set('');
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => void this.router.navigateByUrl('/'),
      error: (error: unknown) => {
        this.errorMessage.set(authErrorMessage(error));
        this.submitting.set(false);
      },
    });
  }

  hasError(field: 'email' | 'password'): boolean {
    return this.form.controls[field].invalid && this.form.controls[field].touched;
  }
}
