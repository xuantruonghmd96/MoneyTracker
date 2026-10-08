import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { authErrorMessage } from '../../core/auth-error-message';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <main class="auth-layout">
      <section class="auth-card" aria-labelledby="page-title">
        <a class="brand" routerLink="/" aria-label="MoneyTracker home">
          <span class="brand-mark" aria-hidden="true">↗</span>
          <span>money<span class="brand-light">tracker</span></span>
        </a>
        <div class="auth-heading">
          <p class="eyebrow">A FRESH START</p>
          <h1 id="page-title">A clearer picture<br />starts here.</h1>
          <p class="auth-description">Create your account. Your next good habit starts today.</p>
        </div>
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          @if (errorMessage()) {
            <div class="form-alert" role="alert">{{ errorMessage() }}</div>
          }
          <label for="displayName">Your name</label>
          <input
            id="displayName"
            type="text"
            formControlName="displayName"
            autocomplete="name"
            placeholder="How should we call you?"
            [attr.aria-invalid]="hasError('displayName')"
          />
          @if (hasError('displayName')) {
            <span class="field-error">Enter your name.</span>
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
          <label for="password">Create a password</label>
          <input
            id="password"
            type="password"
            formControlName="password"
            autocomplete="new-password"
            placeholder="At least 8 characters"
            [attr.aria-invalid]="hasError('password')"
          />
          @if (hasError('password')) {
            <span class="field-error">Use a password between 8 and 128 characters.</span>
          }
          <button class="primary-button" type="submit" [disabled]="submitting()">
            {{ submitting() ? 'Creating account…' : 'Create account' }}
            @if (!submitting()) {
              <span aria-hidden="true">→</span>
            }
          </button>
        </form>
        <p class="auth-switch">Already have an account? <a routerLink="/login">Sign in</a></p>
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
export class RegisterComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly submitting = signal(false);
  readonly errorMessage = signal('');
  readonly form = this.formBuilder.nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(128)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
  });

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) return;

    this.submitting.set(true);
    this.errorMessage.set('');
    const { displayName, email, password } = this.form.getRawValue();
    this.auth.register(email, password, displayName).subscribe({
      next: () => void this.router.navigateByUrl('/'),
      error: (error: unknown) => {
        this.errorMessage.set(authErrorMessage(error));
        this.submitting.set(false);
      },
    });
  }

  hasError(field: 'displayName' | 'email' | 'password'): boolean {
    return this.form.controls[field].invalid && this.form.controls[field].touched;
  }
}
