import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize, forkJoin, map, of } from 'rxjs';
import {
  Category,
  CreateTransactionRequest,
  Participant,
  Transaction,
  UpdateTransactionRequest,
  Wallet,
} from '../../core/api.models';
import { LanguageService, TranslationKey } from '../../core/language.service';
import { MoneyApiService } from '../../core/money-api.service';

@Component({
  selector: 'app-transaction-form',
  imports: [ReactiveFormsModule],
  templateUrl: './transaction-form.component.html',
  styleUrl: './transaction-form.component.scss',
})
export class TransactionFormComponent implements OnInit {
  private readonly api = inject(MoneyApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly language = inject(LanguageService);
  readonly mode = signal<'create' | 'edit'>('create');
  readonly loading = signal(true);
  readonly loadingError = signal<TranslationKey | null>(null);
  readonly categories = signal<Category[]>([]);
  readonly wallets = signal<Wallet[]>([]);
  readonly participants = signal<Participant[]>([]);
  readonly participantsLoading = signal(false);
  readonly participantsError = signal(false);
  readonly categoryWalletAssignments = signal<Record<string, string[]>>({});
  readonly categoryAssignmentsLoading = signal(false);
  readonly categoryAssignmentsError = signal(false);
  readonly saving = signal(false);
  readonly saveError = signal(false);
  readonly editingTransaction = signal<Transaction | null>(null);
  readonly formWalletId = signal('');
  readonly selectableCategories = computed(() => {
    if (this.mode() === 'edit') return this.categories();
    const walletId = this.formWalletId();
    if (!walletId) return [];
    const assignments = this.categoryWalletAssignments();
    return this.categories().filter(
      (category) => assignments[category.id]?.includes(walletId) ?? false,
    );
  });
  readonly form = new FormGroup({
    amount: new FormControl(0, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0.01)],
    }),
    occurredAt: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    walletId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    categoryId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    participantId: new FormControl<string | null>(null),
    note: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2048)] }),
  });
  private categoryAssignmentsLoaded = false;

  constructor() {
    this.form.controls.walletId.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((walletId) => {
        this.formWalletId.set(walletId);
        this.syncCreateCategory();
      });
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.mode.set(id ? 'edit' : 'create');
    this.loading.set(true);
    this.loadingError.set(null);
    this.categoryAssignmentsLoaded = false;
    this.categoryWalletAssignments.set({});

    forkJoin({
      categories: this.api.getCategories(),
      wallets: this.api.getWallets(),
      transaction: id ? this.api.getTransaction(id) : of(null),
    })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: ({ categories, wallets, transaction }) => {
          this.categories.set(categories);
          this.wallets.set(wallets);
          this.editingTransaction.set(transaction);
          this.initializeForm(transaction);
          if (this.mode() === 'create') this.loadCategoryWalletAssignments();
          this.loadParticipants();
        },
        error: (error: unknown) => {
          this.loadingError.set(
            error instanceof HttpErrorResponse && error.status === 0
              ? 'transactions.errorOffline'
              : 'transactions.errorLoad',
          );
        },
      });
  }

  cancel(): void {
    if (!this.saving()) void this.router.navigateByUrl('/');
  }

  retryCategoryAssignments(): void {
    if (this.categoryAssignmentsLoading()) return;
    this.categoryAssignmentsLoaded = false;
    this.categoryWalletAssignments.set({});
    this.loadCategoryWalletAssignments();
  }

  shiftOccurredAtDay(days: number): void {
    const value = this.form.controls.occurredAt.value;
    if (!value) return;
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return;
    date.setDate(date.getDate() + days);
    this.form.controls.occurredAt.setValue(this.toLocalDateTimeInput(date));
  }

  saveTransaction(): void {
    if (
      this.form.invalid ||
      this.saving() ||
      this.participantsLoading() ||
      this.categoryAssignmentsLoading() ||
      this.categoryAssignmentsError()
    ) {
      this.form.markAllAsTouched();
      return;
    }

    const { amount, occurredAt, walletId, categoryId, participantId, note } =
      this.form.getRawValue();
    const request = {
      amount,
      occurredAt: new Date(occurredAt).toISOString(),
      walletId,
      categoryId,
      participantId,
      note: note.trim() || null,
    };
    const transaction = this.editingTransaction();
    this.saving.set(true);
    this.saveError.set(false);
    const request$ = transaction
      ? this.api.updateTransaction(transaction.id, request satisfies UpdateTransactionRequest)
      : this.api.createTransaction({
          id: crypto.randomUUID(),
          ...request,
        } satisfies CreateTransactionRequest);

    request$
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => void this.router.navigateByUrl('/'),
        error: () => this.saveError.set(true),
      });
  }

  private initializeForm(transaction: Transaction | null): void {
    if (transaction) {
      this.form.reset({
        amount: transaction.amount,
        occurredAt: this.toLocalDateTimeInput(new Date(transaction.occurredAt)),
        walletId: transaction.walletId,
        categoryId: transaction.categoryId,
        participantId: transaction.participantId,
        note: transaction.note ?? '',
      });
      this.formWalletId.set(transaction.walletId);
      return;
    }

    const occurredAt = new Date();
    this.form.reset({
      amount: 0,
      occurredAt: this.toLocalDateTimeInput(occurredAt),
      walletId: this.wallets()[0]?.id ?? '',
      categoryId: '',
      participantId: null,
      note: '',
    });
    this.formWalletId.set(this.form.controls.walletId.value);
  }

  private loadParticipants(): void {
    this.participantsLoading.set(true);
    this.participantsError.set(false);
    this.api
      .getParticipants()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.participantsLoading.set(false)),
      )
      .subscribe({
        next: (participants) => this.participants.set(participants),
        error: () => this.participantsError.set(true),
      });
  }

  private loadCategoryWalletAssignments(): void {
    if (this.categoryAssignmentsLoaded || this.categoryAssignmentsLoading()) return;
    const assignedCategories = this.categories().filter(
      (category) => !category.appliesToAllWallets,
    );
    if (!assignedCategories.length) {
      this.categoryWalletAssignments.set(
        Object.fromEntries(
          this.categories().map((category) => [category.id, this.wallets().map(({ id }) => id)]),
        ),
      );
      this.categoryAssignmentsLoaded = true;
      this.syncCreateCategory();
      return;
    }

    this.categoryAssignmentsLoading.set(true);
    this.categoryAssignmentsError.set(false);
    forkJoin(
      assignedCategories.map((category) =>
        this.api
          .getAssignedWallets(category.id)
          .pipe(map((walletIds) => [category.id, walletIds] as const)),
      ),
    )
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.categoryAssignmentsLoading.set(false)),
      )
      .subscribe({
        next: (assignments) => {
          this.categoryWalletAssignments.set({
            ...Object.fromEntries(
              this.categories()
                .filter((category) => category.appliesToAllWallets)
                .map((category) => [category.id, this.wallets().map(({ id }) => id)]),
            ),
            ...Object.fromEntries(assignments),
          });
          this.categoryAssignmentsLoaded = true;
          this.syncCreateCategory();
        },
        error: () => this.categoryAssignmentsError.set(true),
      });
  }

  private syncCreateCategory(): void {
    if (this.mode() !== 'create') return;
    const categories = this.selectableCategories();
    if (categories.some((category) => category.id === this.form.controls.categoryId.value)) return;
    this.form.controls.categoryId.setValue(categories[0]?.id ?? '');
  }

  private toLocalDateTimeInput(date: Date): string {
    const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
    return local.toISOString().slice(0, 16);
  }
}
