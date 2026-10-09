import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { CreateWalletRequest, UpdateWalletRequest, Wallet } from '../../core/api.models';
import { LanguageService, TranslationKey } from '../../core/language.service';
import { MoneyApiService } from '../../core/money-api.service';

type WalletFormMode = 'create' | 'edit' | null;

@Component({
  selector: 'app-wallets',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="wallets-page">
      @if (formMode()) {
        <header class="form-page-heading">
          <a class="back-link" routerLink="/wallets">← {{ language.t('wallets.cancel') }}</a>
          <h1>{{ language.t(formMode() === 'create' ? 'wallets.add' : 'wallets.edit') }}</h1>
        </header>
      } @else {
        <header class="page-heading">
          <div>
            <p class="eyebrow">{{ language.t('wallets.eyebrow') }}</p>
            <h1>{{ language.t('wallets.title') }}</h1>
            <p class="subtitle">{{ language.t('wallets.subtitle') }}</p>
          </div>
          <a class="primary-button" routerLink="/wallets/new">
            <span aria-hidden="true">+</span> {{ language.t('wallets.add') }}
          </a>
        </header>
      }

      @if (errorKey() && !formMode()) {
        <p class="error-message" role="alert">{{ language.t(errorKey()!) }}</p>
      }

      @if (formMode()) {
        @if (loading()) {
          <p class="status-message" role="status">{{ language.t('transactions.loading') }}</p>
        } @else if (loadFailed()) {
          <p class="error-message" role="alert">{{ language.t(errorKey()!) }}</p>
          <button class="secondary-button retry-button" type="button" (click)="loadWallets()">
            {{ language.t('wallets.loadRetry') }}
          </button>
        } @else {
          <form class="wallet-form" [formGroup]="form" (ngSubmit)="save()">
          @if (errorKey()) {
            <p class="error-message" role="alert">{{ language.t(errorKey()!) }}</p>
          }
          <div class="form-grid">
            <label class="field">
              <span>{{ language.t('wallets.name') }}</span>
              <input
                formControlName="name"
                required
                maxlength="128"
                [placeholder]="language.t('wallets.namePlaceholder')"
                autocomplete="off"
              />
              @if (form.controls.name.touched && form.controls.name.invalid) {
                <small>{{ language.t('wallets.nameRequired') }}</small>
              }
            </label>

            @if (formMode() === 'create') {
              <label class="field">
                <span>{{ language.t('wallets.type') }}</span>
                <select formControlName="type" (change)="updateCreditLimitValidation()">
                  <option value="Regular">{{ language.t('wallets.regular') }}</option>
                  <option value="Credit">{{ language.t('wallets.credit') }}</option>
                </select>
              </label>
              <label class="field">
                <span>{{ language.t('wallets.currency') }}</span>
                <input
                  formControlName="currency"
                  required
                  maxlength="8"
                  [placeholder]="language.t('wallets.currencyPlaceholder')"
                  autocomplete="off"
                />
                @if (form.controls.currency.touched && form.controls.currency.invalid) {
                  <small>{{ language.t('wallets.currencyRequired') }}</small>
                }
              </label>
            } @else {
              <div class="field read-only-field">
                <span>{{ language.t('wallets.type') }}</span>
                <strong>{{ walletTypeLabel(form.controls.type.value) }}</strong>
              </div>
              <div class="field read-only-field">
                <span>{{ language.t('wallets.currency') }}</span>
                <strong>{{ form.controls.currency.value }}</strong>
              </div>
            }

            <label class="field">
              <span>{{ language.t('wallets.openingBalance') }}</span>
              <input formControlName="initialBalance" type="number" step="0.01" />
            </label>

            @if (form.controls.type.value === 'Credit') {
              <label class="field">
                <span>{{ language.t('wallets.creditLimit') }}</span>
                <input
                  formControlName="creditLimit"
                  type="number"
                  min="0.01"
                  step="0.01"
                  required
                />
              </label>
            }

            <label class="field">
              <span>{{ language.t('wallets.icon') }}</span>
              <input formControlName="icon" maxlength="64" autocomplete="off" />
            </label>
            <label class="field">
              <span>{{ language.t('wallets.color') }}</span>
              <input class="color-input" formControlName="color" type="color" />
            </label>
          </div>

          <div class="form-actions">
            <button
              class="secondary-button"
              type="button"
              [disabled]="saving()"
              (click)="closeForm()"
            >
              {{ language.t('wallets.cancel') }}
            </button>
            <button class="primary-button" type="submit" [disabled]="saving()">
              {{ language.t(saving() ? 'wallets.saving' : 'wallets.save') }}
            </button>
          </div>
          </form>
        }

      } @else if (loading()) {
        <p class="status-message" role="status">{{ language.t('transactions.loading') }}</p>
      } @else if (loadFailed()) {
        <button class="secondary-button retry-button" type="button" (click)="loadWallets()">
          {{ language.t('wallets.loadRetry') }}
        </button>
      } @else if (wallets().length === 0) {
        <section class="empty-state">
          <span class="empty-icon" aria-hidden="true">▰</span>
          <h2>{{ language.t('wallets.emptyTitle') }}</h2>
          <p>{{ language.t('wallets.emptyDescription') }}</p>
          <a class="primary-button" routerLink="/wallets/new">{{ language.t('wallets.add') }}</a>
        </section>
      } @else {
        <section class="wallet-list" [attr.aria-label]="language.t('wallets.title')">
          @for (wallet of wallets(); track wallet.id) {
            <article class="wallet-card">
                <a class="wallet-edit-link" [routerLink]="['/wallets', wallet.id, 'edit']">
                  <div class="wallet-heading">
                    <span
                      class="wallet-icon"
                      [style.background-color]="wallet.color || null"
                      aria-hidden="true"
                      >{{ wallet.icon || '▰' }}</span
                    >
                    <div class="wallet-identity">
                      <h2>{{ wallet.name }}</h2>
                      <span class="wallet-type">{{
                        language.t(
                          wallet.type === 'Credit'
                            ? 'wallets.creditDescription'
                            : 'wallets.regularDescription'
                        )
                      }}</span>
                    </div>
                    <span class="currency-badge">{{ wallet.currency }}</span>
                  </div>
                  <div class="wallet-details">
                    <div>
                      <span>{{ language.t('wallets.openingBalanceSummary') }}</span>
                      <strong>{{ formatAmount(wallet.initialBalance, wallet.currency) }}</strong>
                    </div>
                    @if (wallet.type === 'Credit' && wallet.creditLimit !== null) {
                      <div>
                        <span>{{ language.t('wallets.creditLimitSummary') }}</span>
                        <strong>{{ formatAmount(wallet.creditLimit, wallet.currency) }}</strong>
                      </div>
                    }
                  </div>
                </a>

              @if (confirmDeleteId() === wallet.id) {
                <div class="delete-confirmation" role="alert">
                  <p>{{ language.t('wallets.deletePrompt') }}</p>
                  <button
                    class="danger-button"
                    type="button"
                    [disabled]="saving()"
                    (click)="deleteWallet(wallet)"
                  >
                    {{ language.t(saving() ? 'wallets.deleting' : 'wallets.confirmDelete') }}
                  </button>
                  <button
                    class="text-button"
                    type="button"
                    [disabled]="saving()"
                    (click)="cancelDelete()"
                  >
                    {{ language.t('wallets.cancel') }}
                  </button>
                </div>
              } @else {
                <div class="card-actions">
                  <button
                    class="text-button danger-text"
                    type="button"
                    (click)="requestDelete(wallet)"
                  >
                    {{ language.t('wallets.delete') }}
                  </button>
                </div>
              }
            </article>
          }
        </section>
      }
    </section>
  `,
  styles: `
    :host {
      display: block;
    }
    .wallets-page {
      display: grid;
      gap: 19px;
    }
    .page-heading {
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      gap: 14px;
    }
    .form-page-heading {
      display: grid;
      gap: 12px;
      padding-bottom: 12px;
      border-bottom: 1px solid var(--border);
    }
    .form-page-heading h1 {
      font-size: 18px;
      letter-spacing: 0;
    }
    .back-link,
    .wallet-edit-link {
      color: inherit;
      text-decoration: none;
    }
    .back-link {
      width: fit-content;
      color: var(--muted);
      font-size: 11px;
    }
    .wallet-edit-link {
      display: grid;
      gap: 15px;
      border-radius: 3px;
    }
    .wallet-edit-link:focus-visible {
      outline: 1px solid var(--green);
      outline-offset: 3px;
    }
    .eyebrow {
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
    .primary-button,
    .secondary-button,
    .danger-button {
      display: inline-flex;
      min-height: 37px;
      align-items: center;
      justify-content: center;
      gap: 7px;
      padding: 0 13px;
      border: 1px solid transparent;
      border-radius: 8px;
      cursor: pointer;
      font: inherit;
      font-size: 11px;
      font-weight: 600;
    }
    .primary-button {
      background: #8fc77e;
      color: #172216;
    }
    .primary-button:hover {
      background: #a2d991;
    }
    .secondary-button {
      border-color: var(--border);
      background: transparent;
      color: var(--muted);
    }
    .danger-button {
      background: rgba(233, 140, 131, 0.12);
      color: #efa39c;
    }
    button:disabled {
      cursor: wait;
      opacity: 0.65;
    }
    .wallet-form,
    .wallet-card,
    .empty-state {
      border: 1px solid var(--border);
      border-radius: 13px;
      background: var(--surface);
    }
    .wallet-form {
      display: grid;
      gap: 17px;
      padding: 18px;
    }
    .wallet-form h2,
    .empty-state h2 {
      margin: 0;
      color: var(--text);
      font-size: 14px;
      font-weight: 600;
    }
    .form-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 13px;
    }
    .field {
      display: grid;
      min-width: 0;
      gap: 6px;
      color: var(--muted);
      font-size: 10px;
    }
    .field input,
    .field select {
      width: 100%;
      min-height: 38px;
      box-sizing: border-box;
      padding: 0 10px;
      border: 1px solid rgba(239, 244, 230, 0.11);
      border-radius: 8px;
      outline: none;
      background: #131713;
      color: var(--text);
      font: inherit;
      font-size: 11px;
    }
    .field input:focus,
    .field select:focus {
      border-color: rgba(166, 217, 154, 0.5);
    }
    .field small {
      color: #efa39c;
    }
    .read-only-field strong {
      display: flex;
      min-height: 38px;
      align-items: center;
      padding: 0 10px;
      border: 1px solid rgba(239, 244, 230, 0.06);
      border-radius: 8px;
      background: rgba(19, 23, 19, 0.5);
      color: #cbd2c7;
      font-size: 11px;
      font-weight: 500;
    }
    .field .color-input {
      height: 38px;
      padding: 4px;
    }
    .form-actions {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
    }
    .error-message {
      margin: 0;
      padding: 11px 13px;
      border: 1px solid rgba(233, 140, 131, 0.22);
      border-radius: 9px;
      background: rgba(233, 140, 131, 0.07);
      color: #efa39c;
      font-size: 11px;
    }
    .status-message {
      color: var(--muted);
      font-size: 12px;
    }
    .retry-button {
      justify-self: start;
    }
    .empty-state {
      display: grid;
      min-height: 230px;
      align-content: center;
      justify-items: center;
      gap: 10px;
      padding: 25px;
      text-align: center;
    }
    .empty-icon {
      display: grid;
      width: 48px;
      height: 48px;
      place-items: center;
      border-radius: 15px;
      background: rgba(166, 217, 154, 0.08);
      color: var(--green);
      font-size: 20px;
    }
    .empty-state p {
      margin: 0 0 5px;
      color: var(--muted);
      font-size: 11px;
    }
    .wallet-list {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 13px;
    }
    .wallet-card {
      display: grid;
      min-width: 0;
      gap: 15px;
      padding: 16px;
    }
    .wallet-heading {
      display: flex;
      min-width: 0;
      align-items: center;
      gap: 10px;
    }
    .wallet-icon {
      display: grid;
      width: 38px;
      height: 38px;
      flex: 0 0 auto;
      place-items: center;
      overflow: hidden;
      border-radius: 12px;
      background: rgba(166, 217, 154, 0.11);
      color: #c5e2b8;
      font-size: 17px;
    }
    .wallet-identity {
      display: grid;
      min-width: 0;
      gap: 4px;
    }
    .wallet-identity h2 {
      overflow: hidden;
      margin: 0;
      color: var(--text);
      font-size: 12px;
      font-weight: 600;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .wallet-type {
      color: var(--muted);
      font-size: 9px;
    }
    .currency-badge {
      margin-left: auto;
      padding: 5px 7px;
      border: 1px solid var(--border);
      border-radius: 6px;
      color: var(--muted);
      font-size: 9px;
    }
    .wallet-details {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 10px;
    }
    .wallet-details > div {
      display: grid;
      gap: 5px;
    }
    .wallet-details span {
      color: var(--muted);
      font-size: 9px;
    }
    .wallet-details strong {
      overflow: hidden;
      color: #e0e7dc;
      font-size: 12px;
      font-weight: 600;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .card-actions {
      display: flex;
      justify-content: flex-end;
      gap: 13px;
      padding-top: 9px;
      border-top: 1px solid rgba(239, 244, 230, 0.06);
    }
    .text-button {
      padding: 3px 0;
      border: 0;
      background: transparent;
      color: #b8c2b3;
      cursor: pointer;
      font: inherit;
      font-size: 10px;
    }
    .text-button:hover {
      color: var(--text);
    }
    .danger-text {
      color: #d9938d;
    }
    .delete-confirmation {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px 12px;
      padding-top: 10px;
      border-top: 1px solid rgba(239, 244, 230, 0.06);
    }
    .delete-confirmation p {
      width: 100%;
      margin: 0;
      color: #d9b8b3;
      font-size: 10px;
      line-height: 1.5;
    }
    @media (max-width: 700px) {
      .wallet-list {
        grid-template-columns: minmax(0, 1fr);
      }
    }
    @media (max-width: 480px) {
      .wallets-page {
        gap: 14px;
      }
      .page-heading {
        align-items: flex-start;
      }
      .page-heading .primary-button {
        flex: 0 0 auto;
      }
      .form-grid {
        grid-template-columns: minmax(0, 1fr);
      }
      .wallet-form,
      .wallet-card {
        padding: 14px;
      }
    }
  `,
})
export class WalletsComponent {
  private readonly api = inject(MoneyApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly language = inject(LanguageService);
  readonly wallets = signal<Wallet[]>([]);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly saving = signal(false);
  readonly formMode = signal<WalletFormMode>(null);
  readonly confirmDeleteId = signal<string | null>(null);
  readonly errorKey = signal<TranslationKey | null>(null);

  readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(128)],
    }),
    type: new FormControl<Wallet['type']>('Regular', { nonNullable: true }),
    currency: new FormControl('VND', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(8)],
    }),
    initialBalance: new FormControl(0, { nonNullable: true }),
    creditLimit: new FormControl<number | null>(null),
    icon: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(64)] }),
    color: new FormControl('#8fc77e', {
      nonNullable: true,
      validators: [Validators.maxLength(16)],
    }),
  });

  private editingWallet: Wallet | null = null;

  constructor() {
    const path = this.route.snapshot.routeConfig?.path;
    if (path === 'wallets/new') {
      this.formMode.set('create');
    } else if (path === 'wallets/:id/edit') {
      this.formMode.set('edit');
    }
    this.loadWallets();
  }

  openCreate(): void {
    this.editingWallet = null;
    this.form.reset({
      name: '',
      type: 'Regular',
      currency: 'VND',
      initialBalance: 0,
      creditLimit: null,
      icon: '',
      color: '#8fc77e',
    });
    this.formMode.set('create');
    this.errorKey.set(null);
  }

  openEdit(wallet: Wallet): void {
    if (this.saving()) return;
    this.editingWallet = wallet;
    this.form.reset({
      name: wallet.name,
      type: wallet.type,
      currency: wallet.currency,
      initialBalance: wallet.initialBalance,
      creditLimit: wallet.creditLimit,
      icon: wallet.icon ?? '',
      color: wallet.color ?? '#8fc77e',
    });
    this.formMode.set('edit');
    this.confirmDeleteId.set(null);
    this.errorKey.set(null);
  }

  closeForm(): void {
    const routePath = this.route.snapshot.routeConfig?.path;
    if (routePath === 'wallets/new' || routePath === 'wallets/:id/edit') {
      void this.router.navigateByUrl('/wallets');
      return;
    }
    this.formMode.set(null);
    this.editingWallet = null;
  }

  updateCreditLimitValidation(): void {
    const control = this.form.controls.creditLimit;
    if (this.form.controls.type.value === 'Credit') {
      control.setValidators([Validators.required, Validators.min(0.01)]);
    } else {
      control.clearValidators();
    }
    control.updateValueAndValidity();
  }

  save(): void {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    const value = this.form.getRawValue();
    const name = value.name.trim();
    if (!name) {
      this.errorKey.set('wallets.nameRequired');
      return;
    }
    if (this.form.controls.name.invalid) {
      this.errorKey.set('wallets.saveError');
      return;
    }
    if (value.type === 'Credit' && (!value.creditLimit || value.creditLimit <= 0)) {
      this.errorKey.set('wallets.invalidCreditLimit');
      return;
    }
    if (value.currency.trim().length === 0) {
      this.errorKey.set('wallets.currencyRequired');
      return;
    }
    if (this.formMode() === 'create' && this.form.controls.currency.invalid) {
      this.errorKey.set('wallets.saveError');
      return;
    }

    this.errorKey.set(null);
    this.saving.set(true);
    if (this.formMode() === 'edit' && this.editingWallet) {
      const request: UpdateWalletRequest = {
        name,
        creditLimit: value.creditLimit,
        initialBalance: value.initialBalance,
        icon: value.icon.trim() || null,
        color: this.form.controls.color.dirty ? value.color || null : this.editingWallet.color,
      };
      this.api
        .updateWallet(this.editingWallet.id, request)
        .pipe(
          takeUntilDestroyed(this.destroyRef),
          finalize(() => this.saving.set(false)),
        )
        .subscribe({
          next: (wallet) => {
            this.wallets.update((wallets) =>
              wallets.map((item) => (item.id === wallet.id ? wallet : item)),
            );
            this.closeForm();
          },
          error: (error: unknown) => this.errorKey.set(this.getErrorKey(error, 'save')),
        });
      return;
    }

    const request: CreateWalletRequest = {
      id: globalThis.crypto.randomUUID(),
      name,
      type: value.type,
      creditLimit: value.type === 'Credit' ? value.creditLimit : null,
      initialBalance: value.initialBalance,
      currency: value.currency.trim().toUpperCase(),
      icon: value.icon.trim() || null,
      color: value.color || null,
    };
    this.api
      .createWallet(request)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: (wallet) => {
          this.wallets.update((wallets) => [...wallets, wallet]);
          this.closeForm();
        },
        error: (error: unknown) => this.errorKey.set(this.getErrorKey(error, 'save')),
      });
  }

  requestDelete(wallet: Wallet): void {
    if (this.saving()) return;
    this.confirmDeleteId.set(wallet.id);
    this.errorKey.set(null);
  }

  cancelDelete(): void {
    this.confirmDeleteId.set(null);
  }

  deleteWallet(wallet: Wallet): void {
    if (this.saving()) return;
    this.errorKey.set(null);
    this.saving.set(true);
    this.api
      .deleteWallet(wallet.id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => {
          this.wallets.update((wallets) => wallets.filter((item) => item.id !== wallet.id));
          this.cancelDelete();
        },
        error: (error: unknown) => this.errorKey.set(this.getErrorKey(error, 'delete')),
      });
  }

  formatAmount(amount: number, currency: string): string {
    const formatted = new Intl.NumberFormat(this.language.locale(), {
      maximumFractionDigits: 2,
    }).format(amount);
    return `${formatted} ${currency}`;
  }

  walletTypeLabel(type: Wallet['type']): string {
    return this.language.t(type === 'Credit' ? 'wallets.credit' : 'wallets.regular');
  }

  loadWallets(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.errorKey.set(null);
    this.api
      .getWallets()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (wallets) => {
          this.wallets.set(wallets);
          const id = this.route.snapshot.paramMap.get('id');
          if (id) {
            const wallet = wallets.find((item) => item.id === id);
            if (wallet) {
              this.openEdit(wallet);
            } else {
              this.loadFailed.set(true);
              this.errorKey.set('wallets.notFound');
            }
          } else if (this.formMode() === 'create') {
            this.openCreate();
          }
        },
        error: (error: unknown) => {
          this.loadFailed.set(true);
          this.errorKey.set(this.getErrorKey(error, 'load'));
        },
      });
  }

  private getErrorKey(error: unknown, operation: 'load' | 'save' | 'delete'): TranslationKey {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) return 'wallets.offlineError';
      switch (error.error?.error) {
        case 'INVALID_CREDIT_LIMIT':
          return 'wallets.invalidCreditLimit';
        case 'ID_ALREADY_EXISTS':
          return 'wallets.duplicateId';
        case 'NOT_FOUND':
          return 'wallets.notFound';
      }
    }
    return operation === 'load'
      ? 'wallets.loadError'
      : operation === 'delete'
        ? 'wallets.deleteError'
        : 'wallets.saveError';
  }
}
