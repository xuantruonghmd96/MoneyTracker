import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { LanguageService } from '../../core/language.service';
import { AccountComponent } from './account.component';

describe('AccountComponent', () => {
  it('shows the signed-in user and logs out to the login route', async () => {
    const logout = vi.fn(() => of(void 0));
    TestBed.configureTestingModule({
      imports: [AccountComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'login', component: AccountComponent }]),
        {
          provide: AuthService,
          useValue: {
            user: () => ({ id: 'user-1', email: 'alex@example.com', displayName: 'Alex Morgan' }),
            logout,
            clearSession: vi.fn(),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(AccountComponent);
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('alex@example.com');
    fixture.nativeElement.querySelector('app-language-selector button:last-child').click();
    fixture.detectChanges();

    expect(TestBed.inject(LanguageService).language()).toBe('vi');
    expect(fixture.nativeElement.textContent).toContain('Tài khoản');

    fixture.nativeElement.querySelector('.session-card button').click();
    await fixture.whenStable();

    expect(logout).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith('/login');
  });
});
