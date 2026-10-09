import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { Category, CategoryType, CreateCategoryRequest, UpdateCategoryRequest } from '../../core/api.models';
import { LanguageService, TranslationKey } from '../../core/language.service';
import { MoneyApiService } from '../../core/money-api.service';

type CategoryFormMode = 'create' | 'edit' | null;

@Component({
  selector: 'app-categories',
  imports: [ReactiveFormsModule],
  template: `
    <section class="categories-page">
      <header class="page-heading">
        <div>
          <p class="eyebrow">{{ language.t('categories.eyebrow') }}</p>
          <h1>{{ language.t('categories.title') }}</h1>
          <p class="subtitle">{{ language.t('categories.subtitle') }}</p>
        </div>
        @if (!formMode()) {
          <button class="primary-button" type="button" (click)="openCreate()">
            <span aria-hidden="true">+</span> {{ language.t('categories.add') }}
          </button>
        }
      </header>

      @if (errorKey()) {
        <p class="error-message" role="alert">{{ language.t(errorKey()!) }}</p>
      }

      @if (formMode()) {
        <form class="category-form" [formGroup]="form" (ngSubmit)="save()">
          <h2>{{ language.t(formMode() === 'create' ? 'categories.add' : 'categories.edit') }}</h2>
          <div class="form-grid">
            <label class="field">
              <span>{{ language.t('categories.name') }}</span>
              <input
                formControlName="name"
                required
                maxlength="128"
                [placeholder]="language.t('categories.namePlaceholder')"
                autocomplete="off"
              />
              @if (form.controls.name.touched && form.controls.name.invalid) {
                <small>{{ language.t('categories.nameRequired') }}</small>
              }
            </label>

            <label class="field">
              <span>{{ language.t('categories.type') }}</span>
              <select formControlName="type">
                @for (categoryType of categoryTypes; track categoryType) {
                  <option [value]="categoryType">
                    {{ language.t(categoryTypeLabelKey(categoryType)) }}
                  </option>
                }
              </select>
            </label>

            <label class="field">
              <span>{{ language.t('categories.parent') }}</span>
              <select formControlName="parentId">
                <option [ngValue]="null">{{ language.t('categories.root') }}</option>
                @for (item of selectableParents(); track item.id) {
                  <option [value]="item.id">{{ item.name }}</option>
                }
              </select>
            </label>

            <label class="checkbox-field">
              <input type="checkbox" formControlName="appliesToAllWallets" />
              <span>{{ language.t('categories.appliesToAllWallets') }}</span>
            </label>

            <label class="field">
              <span>{{ language.t('categories.icon') }}</span>
              <input formControlName="icon" maxlength="64" [placeholder]="language.t('categories.iconPlaceholder')" autocomplete="off" />
            </label>

            <label class="field">
              <span>{{ language.t('categories.color') }}</span>
              <input class="color-input" formControlName="color" type="color" />
            </label>
          </div>

          <div class="form-actions">
            <button class="secondary-button" type="button" [disabled]="saving()" (click)="closeForm()">
              {{ language.t('categories.cancel') }}
            </button>
            <button class="primary-button" type="submit" [disabled]="saving()">
              {{ language.t(saving() ? 'categories.saving' : 'categories.save') }}
            </button>
          </div>
        </form>
      }

      @if (loading()) {
        <p class="status-message" role="status">{{ language.t('transactions.loading') }}</p>
      } @else if (loadFailed()) {
        <button class="secondary-button retry-button" type="button" (click)="loadCategories()">
          {{ language.t('categories.loadRetry') }}
        </button>
      } @else if (categories().length === 0) {
        <section class="empty-state">
          <span class="empty-icon" aria-hidden="true">◈</span>
          <h2>{{ language.t('categories.emptyTitle') }}</h2>
          <p>{{ language.t('categories.emptyDescription') }}</p>
          @if (!formMode()) {
            <button class="primary-button" type="button" (click)="openCreate()">
              {{ language.t('categories.add') }}
            </button>
          }
        </section>
      } @else {
        <section class="category-list" [attr.aria-label]="language.t('categories.title')">
          @for (category of categories(); track category.id) {
            <article class="category-card">
              <div class="category-topline">
                <span class="category-icon" [style.background-color]="category.color || null" aria-hidden="true">
                  {{ category.icon || '◈' }}
                </span>
                <div class="category-summary">
                  <h2>{{ category.name }}</h2>
                  <div class="meta-row">
                    <span class="badge type-badge">{{ language.t(categoryTypeLabelKey(category.type)) }}</span>
                    @if (category.isSystem) {
                      <span class="badge system-badge">{{ language.t('categories.system') }}</span>
                    }
                    @if (category.appliesToAllWallets) {
                      <span class="badge scope-badge">{{ language.t('categories.allWallets') }}</span>
                    }
                  </div>
                </div>
              </div>

              <div class="category-details">
                <div>
                  <span>{{ language.t('categories.parent') }}</span>
                  <strong>{{ category.parentId ? parentName(category.parentId) : language.t('categories.root') }}</strong>
                </div>
                <div>
                  <span>{{ language.t('categories.scope') }}</span>
                  <strong>{{ category.appliesToAllWallets ? language.t('categories.allWallets') : language.t('categories.selectedWallets') }}</strong>
                </div>
              </div>

              <div class="card-actions">
                <button class="text-button" type="button" [disabled]="!canManage(category)" (click)="openEdit(category)">
                  {{ language.t('categories.edit') }}
                </button>
                <button class="text-button danger-text" type="button" [disabled]="!canManage(category)" (click)="requestDelete(category)">
                  {{ language.t('categories.delete') }}
                </button>
              </div>

              @if (confirmDeleteId() === category.id) {
                <div class="delete-confirmation" role="alert">
                  <p>{{ language.t('categories.deletePrompt') }}</p>
                  <button class="danger-button" type="button" [disabled]="saving()" (click)="deleteCategory(category)">
                    {{ language.t(saving() ? 'categories.deleting' : 'categories.confirmDelete') }}
                  </button>
                  <button class="text-button" type="button" [disabled]="saving()" (click)="cancelDelete()">
                    {{ language.t('categories.cancel') }}
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
    .categories-page {
      display: grid;
      gap: 18px;
    }
    .page-heading {
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      gap: 14px;
    }
    .eyebrow {
      margin: 0 0 6px;
      color: var(--muted);
      font-size: 10px;
      letter-spacing: 0.12em;
      text-transform: uppercase;
    }
    h1 {
      margin: 0;
      color: var(--text);
      font-size: 24px;
      line-height: 1.2;
    }
    .subtitle {
      margin: 6px 0 0;
      color: var(--muted);
      font-size: 11px;
    }
    .primary-button,
    .secondary-button,
    .danger-button,
    .text-button {
      border-radius: 9px;
      font: inherit;
      cursor: pointer;
    }
    .primary-button {
      display: inline-flex;
      align-items: center;
      gap: 7px;
      padding: 0 13px;
      min-height: 34px;
      border: 1px solid transparent;
      background: #8fc77e;
      color: #172216;
      font-size: 11px;
      font-weight: 600;
    }
    .primary-button:hover {
      background: #a2d991;
    }
    .secondary-button {
      min-height: 34px;
      padding: 0 13px;
      border: 1px solid var(--border);
      background: transparent;
      color: var(--muted);
      font-size: 11px;
    }
    .danger-button {
      min-height: 32px;
      padding: 0 12px;
      border: 1px solid rgba(233, 140, 131, 0.25);
      background: rgba(233, 140, 131, 0.12);
      color: #efa39c;
      font-size: 10px;
      font-weight: 600;
    }
    button:disabled {
      cursor: not-allowed;
      opacity: 0.55;
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
    .category-form,
    .category-card,
    .empty-state {
      border: 1px solid var(--border);
      border-radius: 13px;
      background: var(--surface);
    }
    .category-form {
      display: grid;
      gap: 18px;
      padding: 18px;
    }
    .category-form h2,
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
    .field,
    .checkbox-field {
      display: grid;
      gap: 6px;
      min-width: 0;
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
    .checkbox-field {
      display: flex;
      align-items: center;
      gap: 9px;
      min-height: 38px;
      padding: 0 2px;
      border: 1px solid rgba(239, 244, 230, 0.08);
      border-radius: 8px;
      background: rgba(19, 23, 19, 0.45);
    }
    .checkbox-field input {
      width: 16px;
      height: 16px;
      accent-color: #8fc77e;
    }
    .checkbox-field span {
      color: var(--text);
      font-size: 11px;
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
    .status-message {
      color: var(--muted);
      font-size: 12px;
    }
    .retry-button {
      justify-self: start;
      min-height: 34px;
      padding: 0 13px;
      border: 1px solid var(--border);
      border-radius: 9px;
      background: transparent;
      color: var(--muted);
      font: inherit;
      font-size: 11px;
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
    .category-list {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 13px;
    }
    .category-card {
      display: grid;
      gap: 14px;
      padding: 16px;
      min-width: 0;
    }
    .category-topline {
      display: flex;
      align-items: center;
      gap: 10px;
      min-width: 0;
    }
    .category-icon {
      display: grid;
      width: 38px;
      height: 38px;
      place-items: center;
      flex: 0 0 auto;
      border-radius: 12px;
      color: #c5e2b8;
      font-size: 17px;
    }
    .category-summary {
      display: grid;
      gap: 6px;
      min-width: 0;
    }
    .category-summary h2 {
      margin: 0;
      color: var(--text);
      font-size: 12px;
      font-weight: 600;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .meta-row {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }
    .badge {
      display: inline-flex;
      align-items: center;
      padding: 3px 6px;
      border-radius: 999px;
      font-size: 8px;
      letter-spacing: 0.04em;
      text-transform: uppercase;
    }
    .type-badge {
      background: rgba(143, 199, 126, 0.08);
      color: #c5e2b8;
    }
    .system-badge {
      background: rgba(159, 173, 255, 0.08);
      color: #bfc5ff;
    }
    .scope-badge {
      background: rgba(255, 193, 94, 0.08);
      color: #f7d287;
    }
    .category-details {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 10px;
    }
    .category-details > div {
      display: grid;
      gap: 5px;
    }
    .category-details span {
      color: var(--muted);
      font-size: 9px;
    }
    .category-details strong {
      color: #e0e7dc;
      font-size: 11px;
      font-weight: 600;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .card-actions {
      display: flex;
      justify-content: flex-end;
      gap: 13px;
      padding-top: 8px;
      border-top: 1px solid rgba(239, 244, 230, 0.06);
    }
    .text-button {
      padding: 0;
      border: 0;
      background: transparent;
      color: #b8c2b3;
      font-size: 10px;
    }
    .text-button:hover:not(:disabled) {
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
      .category-list {
        grid-template-columns: minmax(0, 1fr);
      }
      .form-grid {
        grid-template-columns: minmax(0, 1fr);
      }
      .page-heading {
        flex-direction: column;
        align-items: flex-start;
      }
    }
  `,
})
export class CategoriesComponent {
  private readonly api = inject(MoneyApiService);
  private readonly destroyRef = inject(DestroyRef);
  readonly language = inject(LanguageService);
  readonly categories = signal<Category[]>([]);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly saving = signal(false);
  readonly formMode = signal<CategoryFormMode>(null);
  readonly confirmDeleteId = signal<string | null>(null);
  readonly errorKey = signal<TranslationKey | null>(null);
  readonly categoryTypes: CategoryType[] = ['Expense', 'Income', 'Debt'];

  readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(128)],
    }),
    type: new FormControl<CategoryType>('Expense', { nonNullable: true }),
    parentId: new FormControl<string | null>(null),
    appliesToAllWallets: new FormControl(true, { nonNullable: true }),
    icon: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(64)] }),
    color: new FormControl('#8fc77e', {
      nonNullable: true,
      validators: [Validators.maxLength(16)],
    }),
  });

  private editingCategory: Category | null = null;

  constructor() {
    this.loadCategories();
  }

  openCreate(): void {
    this.editingCategory = null;
    this.form.reset({
      name: '',
      type: 'Expense',
      parentId: null,
      appliesToAllWallets: true,
      icon: '',
      color: '#8fc77e',
    });
    this.formMode.set('create');
    this.confirmDeleteId.set(null);
    this.errorKey.set(null);
  }

  openEdit(category: Category): void {
    if (!this.canManage(category) || this.saving()) return;
    this.editingCategory = category;
    this.form.reset({
      name: category.name,
      type: category.type,
      parentId: category.parentId,
      appliesToAllWallets: category.appliesToAllWallets,
      icon: category.icon ?? '',
      color: category.color ?? '#8fc77e',
    });
    this.formMode.set('edit');
    this.confirmDeleteId.set(null);
    this.errorKey.set(null);
  }

  closeForm(): void {
    this.formMode.set(null);
    this.editingCategory = null;
  }

  save(): void {
    if (this.saving()) return;
    this.form.markAllAsTouched();

    const value = this.form.getRawValue();
    const name = value.name.trim();
    if (!name) {
      this.errorKey.set('categories.nameRequired');
      return;
    }
    if (this.form.controls.name.invalid) {
      this.errorKey.set('categories.saveError');
      return;
    }

    const parentId = value.parentId && value.parentId !== this.editingCategory?.id ? value.parentId : null;
    this.errorKey.set(null);
    this.saving.set(true);

    if (this.formMode() === 'edit' && this.editingCategory) {
      const request: UpdateCategoryRequest = {
        name,
        parentId,
        appliesToAllWallets: value.appliesToAllWallets,
        icon: value.icon.trim() || null,
        color: value.color || null,
      };

      this.api
        .updateCategory(this.editingCategory.id, request)
        .pipe(
          takeUntilDestroyed(this.destroyRef),
          finalize(() => this.saving.set(false)),
        )
        .subscribe({
          next: (category) => {
            this.categories.update((items) => items.map((item) => (item.id === category.id ? category : item)));
            this.closeForm();
          },
          error: (error: unknown) => this.errorKey.set(this.getErrorKey(error, 'save')),
        });
      return;
    }

    const request: CreateCategoryRequest = {
      id: globalThis.crypto.randomUUID(),
      name,
      type: value.type,
      parentId: parentId,
      appliesToAllWallets: value.appliesToAllWallets,
      icon: value.icon.trim() || null,
      color: value.color || null,
    };

    this.api
      .createCategory(request)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: (category) => {
          this.categories.update((items) => [...items, category]);
          this.closeForm();
        },
        error: (error: unknown) => this.errorKey.set(this.getErrorKey(error, 'save')),
      });
  }

  requestDelete(category: Category): void {
    if (!this.canManage(category) || this.saving()) return;
    this.confirmDeleteId.set(category.id);
    this.errorKey.set(null);
  }

  cancelDelete(): void {
    this.confirmDeleteId.set(null);
  }

  deleteCategory(category: Category): void {
    if (!this.canManage(category) || this.saving()) return;
    this.errorKey.set(null);
    this.saving.set(true);
    this.api
      .deleteCategory(category.id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => {
          this.categories.update((items) => items.filter((item) => item.id !== category.id));
          this.cancelDelete();
        },
        error: (error: unknown) => this.errorKey.set(this.getErrorKey(error, 'delete')),
      });
  }

  loadCategories(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.errorKey.set(null);
    this.api
      .getCategories()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (categories) => this.categories.set(categories),
        error: (error: unknown) => {
          this.loadFailed.set(true);
          this.errorKey.set(this.getErrorKey(error, 'load'));
        },
      });
  }

  categoryTypeLabelKey(type: CategoryType): TranslationKey {
    switch (type) {
      case 'Income':
        return 'categories.typeIncome';
      case 'Debt':
        return 'categories.typeDebt';
      default:
        return 'categories.typeExpense';
    }
  }

  selectableParents(): Category[] {
    return this.categories().filter((item) => item.id !== this.editingCategory?.id && !item.isSystem);
  }

  parentName(parentId: string): string {
    return this.categories().find((item) => item.id === parentId)?.name ?? this.language.t('categories.root');
  }

  canManage(category: Category): boolean {
    return !category.isSystem;
  }

  private getErrorKey(error: unknown, operation: 'load' | 'save' | 'delete'): TranslationKey {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) return 'categories.offlineError';
      switch (error.error?.error) {
        case 'SYSTEM_CATEGORY_READ_ONLY':
          return 'categories.systemReadOnly';
        case 'NOT_FOUND':
          return 'categories.notFound';
      }
    }
    return operation === 'load'
      ? 'categories.loadError'
      : operation === 'delete'
        ? 'categories.deleteError'
        : 'categories.saveError';
  }
}
