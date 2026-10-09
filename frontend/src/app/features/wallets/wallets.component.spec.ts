import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, RouterOutlet } from '@angular/router';
import { Wallet } from '../../core/api.models';
import { WalletsComponent } from './wallets.component';

@Component({ standalone: true, imports: [RouterOutlet], template: '<router-outlet />' })
class RouterHostComponent {}

describe('WalletsComponent', () => {
  let http: HttpTestingController;
  let router: Router;

  const makeWallet = (overrides: Partial<Wallet> = {}): Wallet => ({
    id: 'wallet-1',
    name: 'Everyday',
    type: 'Regular',
    creditLimit: null,
    initialBalance: 120000,
    currency: 'VND',
    icon: null,
    color: null,
    ...overrides,
  });

  beforeEach(() => {
    localStorage.removeItem('money-tracker.language');
    TestBed.configureTestingModule({
      imports: [WalletsComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'wallets', component: WalletsComponent },
          { path: 'wallets/new', component: WalletsComponent },
          { path: 'wallets/:id/edit', component: WalletsComponent },
        ]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => http.verify());

  it('loads wallets and displays their opening balance and credit limit', () => {
    const fixture = TestBed.createComponent(WalletsComponent);
    fixture.detectChanges();

    http.expectOne('/api/wallets').flush([
      makeWallet(),
      makeWallet({
        id: 'wallet-credit',
        name: 'Travel card',
        type: 'Credit',
        creditLimit: 5000000,
        currency: 'VND',
      }),
    ]);
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.textContent).toContain('Everyday');
    expect(page.textContent).toContain('Opening balance');
    expect(page.textContent).toContain('Travel card');
    expect(page.textContent).toContain('Credit limit');
    expect(page.textContent).toContain('5,000,000 VND');
  });

  it('opens a wallet edit screen from the card content and removes the edit button', async () => {
    const fixture = TestBed.createComponent(RouterHostComponent);
    await router.navigateByUrl('/wallets');
    fixture.detectChanges();
    http.expectOne('/api/wallets').flush([makeWallet()]);
    fixture.detectChanges();

    const link = fixture.nativeElement.querySelector('.wallet-edit-link') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/wallets/wallet-1/edit');
    expect(fixture.nativeElement.querySelector('.card-actions').textContent).not.toContain('Edit');

    link.click();
    await fixture.whenStable();
    expect(router.url).toBe('/wallets/wallet-1/edit');
    http.expectOne('/api/wallets').flush([makeWallet()]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.wallet-form')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.wallet-list')).toBeNull();
  });

  it('opens wallet creation as a dedicated screen', async () => {
    const fixture = TestBed.createComponent(RouterHostComponent);
    await router.navigateByUrl('/wallets/new');
    fixture.detectChanges();
    http.expectOne('/api/wallets').flush([]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.wallet-form')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.wallet-list')).toBeNull();
    expect(fixture.nativeElement.querySelector('.form-page-heading h1').textContent).toContain(
      'Add wallet',
    );
  });

  it('creates a wallet with a client-generated ID and its credit limit', () => {
    const fixture = TestBed.createComponent(WalletsComponent);
    fixture.detectChanges();
    http.expectOne('/api/wallets').flush([]);

    const component = fixture.componentInstance;
    component.openCreate();
    component.form.patchValue({
      name: ' Travel card ',
      type: 'Credit',
      currency: 'usd',
      initialBalance: -50,
      creditLimit: 2500,
      icon: 'card',
      color: '#123456',
    });
    component.updateCreditLimitValidation();
    component.save();

    const request = http.expectOne('/api/wallets');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toMatchObject({
      name: 'Travel card',
      type: 'Credit',
      creditLimit: 2500,
      initialBalance: -50,
      currency: 'USD',
      icon: 'card',
      color: '#123456',
    });
    expect(request.request.body.id).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i,
    );

    const created = makeWallet({
      id: request.request.body.id,
      name: 'Travel card',
      type: 'Credit',
      creditLimit: 2500,
      initialBalance: -50,
      currency: 'USD',
      icon: 'card',
      color: '#123456',
    });
    request.flush(created);
    expect(component.wallets()).toEqual([created]);
    expect(component.formMode()).toBeNull();
  });

  it('updates editable fields without changing the wallet type or currency', () => {
    const existing = makeWallet({
      type: 'Credit',
      creditLimit: 5000,
    });
    const fixture = TestBed.createComponent(WalletsComponent);
    fixture.detectChanges();
    http.expectOne('/api/wallets').flush([existing]);

    const component = fixture.componentInstance;
    component.openEdit(existing);
    component.form.patchValue({
      name: 'Updated card',
      initialBalance: 200,
      creditLimit: 7000,
      icon: 'card',
      color: '#234567',
    });
    component.form.controls.color.markAsDirty();
    component.save();

    const request = http.expectOne('/api/wallets/wallet-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      name: 'Updated card',
      creditLimit: 7000,
      initialBalance: 200,
      icon: 'card',
      color: '#234567',
    });

    const updated = { ...existing, ...request.request.body };
    request.flush(updated);
    expect(component.wallets()).toEqual([updated]);
    expect(component.formMode()).toBeNull();
  });

  it('preserves an optional wallet color when it is not edited', () => {
    const wallet = makeWallet();
    const fixture = TestBed.createComponent(WalletsComponent);
    fixture.detectChanges();
    http.expectOne('/api/wallets').flush([wallet]);

    const component = fixture.componentInstance;
    component.openEdit(wallet);
    component.form.controls.name.setValue('Renamed wallet');
    component.save();

    const request = http.expectOne('/api/wallets/wallet-1');
    expect(request.request.body.color).toBeNull();
    request.flush({ ...wallet, name: 'Renamed wallet' });
  });

  it('confirms and deletes a wallet through the soft-delete endpoint', () => {
    const wallet = makeWallet();
    const fixture = TestBed.createComponent(WalletsComponent);
    fixture.detectChanges();
    http.expectOne('/api/wallets').flush([wallet]);

    const component = fixture.componentInstance;
    component.requestDelete(wallet);
    expect(component.confirmDeleteId()).toBe(wallet.id);
    component.deleteWallet(wallet);

    const request = http.expectOne('/api/wallets/wallet-1');
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
    expect(component.wallets()).toEqual([]);
    expect(component.confirmDeleteId()).toBeNull();
  });
});
