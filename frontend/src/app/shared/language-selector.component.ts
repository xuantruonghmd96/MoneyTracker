import { Component, inject } from '@angular/core';
import { LanguageService } from '../core/language.service';

@Component({
  selector: 'app-language-selector',
  template: `
    <div class="language-selector" role="group" [attr.aria-label]="language.t('language.select')">
      <button
        type="button"
        [attr.aria-label]="language.t('language.english')"
        [attr.aria-pressed]="language.language() === 'en'"
        (click)="language.setLanguage('en')"
      >
        EN
      </button>
      <button
        type="button"
        [attr.aria-label]="language.t('language.vietnamese')"
        [attr.aria-pressed]="language.language() === 'vi'"
        (click)="language.setLanguage('vi')"
      >
        VI
      </button>
    </div>
  `,
  styles: `
    .language-selector {
      display: inline-flex;
      gap: 3px;
      padding: 3px;
      border: 1px solid rgba(239, 244, 230, 0.11);
      border-radius: 8px;
      background: rgba(239, 244, 230, 0.035);
    }
    button {
      min-width: 31px;
      min-height: 27px;
      padding: 0 6px;
      border: 0;
      border-radius: 5px;
      background: transparent;
      color: var(--muted, #929b90);
      cursor: pointer;
      font: inherit;
      font-size: 10px;
      font-weight: 700;
    }
    button[aria-pressed='true'] {
      background: rgba(166, 217, 154, 0.14);
      color: var(--green, #a6d99a);
    }
  `,
})
export class LanguageSelectorComponent {
  readonly language = inject(LanguageService);
}
