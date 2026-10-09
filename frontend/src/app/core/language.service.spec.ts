import { TestBed } from '@angular/core/testing';
import { LanguageService } from './language.service';

describe('LanguageService', () => {
  beforeEach(() => {
    localStorage.removeItem('money-tracker.language');
    TestBed.configureTestingModule({});
  });

  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('uses the browser language on first visit and falls back to English', () => {
    const language = TestBed.inject(LanguageService);

    expect(language.language()).toBe(
      navigator.language.toLowerCase().startsWith('vi') ? 'vi' : 'en',
    );
  });

  it('selects Vietnamese when the browser locale is Vietnamese', () => {
    vi.spyOn(navigator, 'language', 'get').mockReturnValue('vi-VN');

    expect(TestBed.inject(LanguageService).language()).toBe('vi');
  });

  it('persists a language change and updates translated document metadata', () => {
    const language = TestBed.inject(LanguageService);

    language.setLanguage('vi');

    expect(localStorage.getItem('money-tracker.language')).toBe('vi');
    expect(language.t('transactions.allWallets')).toBe('Tất cả ví');
    expect(document.documentElement.lang).toBe('vi');
    expect(document.title).toContain('Tài chính');

    language.setLanguage('en');

    expect(localStorage.getItem('money-tracker.language')).toBe('en');
    expect(language.t('transactions.allWallets')).toBe('All wallets');
    expect(document.documentElement.lang).toBe('en');
  });

  it('restores a saved language before consulting the browser language', () => {
    localStorage.setItem('money-tracker.language', 'vi');

    expect(TestBed.inject(LanguageService).language()).toBe('vi');
  });
});
