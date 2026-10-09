import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, RouterOutlet } from '@angular/router';
import { CategoriesComponent } from './categories.component';

@Component({ standalone: true, imports: [RouterOutlet], template: '<router-outlet />' })
class RouterHostComponent {}

describe('CategoriesComponent', () => {
  let http: HttpTestingController;
  let router: Router;

  const category = (overrides: Record<string, unknown> = {}) => ({
    id: 'category-1',
    name: 'Food',
    type: 'Expense',
    parentId: null,
    appliesToAllWallets: true,
    icon: null,
    color: null,
    isSystem: false,
    systemKey: null,
    ...overrides,
  });

  beforeEach(() => {
    localStorage.removeItem('money-tracker.language');
    TestBed.configureTestingModule({
      imports: [RouterHostComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'categories', component: CategoriesComponent },
          { path: 'categories/new', component: CategoriesComponent },
          { path: 'categories/:id/edit', component: CategoriesComponent },
        ]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => http.verify());

  it('opens editable category items by clicking their content and has no edit button', async () => {
    const fixture = TestBed.createComponent(RouterHostComponent);
    await router.navigateByUrl('/categories');
    fixture.detectChanges();

    http.expectOne('/api/categories').flush([
      category(),
      category({ id: 'system-category', name: 'Debt', isSystem: true }),
    ]);
    http.expectOne('/api/wallets').flush([]);
    fixture.detectChanges();

    const link = fixture.nativeElement.querySelector('.category-edit-link') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/categories/category-1/edit');
    expect(fixture.nativeElement.querySelectorAll('.category-edit-link')).toHaveLength(1);
    expect(fixture.nativeElement.querySelector('.card-actions').textContent).not.toContain('Edit');

    link.click();
    await fixture.whenStable();
    expect(router.url).toBe('/categories/category-1/edit');
    http.expectOne('/api/categories').flush([category()]);
    http.expectOne('/api/wallets').flush([]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.category-form')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.category-list')).toBeNull();
  });

  it('opens category creation as a dedicated screen', async () => {
    const fixture = TestBed.createComponent(RouterHostComponent);
    await router.navigateByUrl('/categories/new');
    fixture.detectChanges();

    http.expectOne('/api/categories').flush([]);
    http.expectOne('/api/wallets').flush([]);
    fixture.detectChanges();

    expect(router.url).toBe('/categories/new');
    expect(fixture.nativeElement.querySelector('.category-form')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.category-list')).toBeNull();
    expect(fixture.nativeElement.querySelector('.form-page-heading h1').textContent).toContain(
      'Add category',
    );
  });
});
