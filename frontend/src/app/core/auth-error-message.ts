import { HttpErrorResponse } from '@angular/common/http';
import { ApiError } from './api.models';
import { TranslationKey } from './language.service';

export function authErrorMessage(error: unknown): TranslationKey {
  if (!(error instanceof HttpErrorResponse)) {
    return 'auth.errorGeneric';
  }

  const response = error.error as ApiError | null;
  switch (response?.error) {
    case 'INVALID_CREDENTIALS':
      return 'auth.errorCredentials';
    case 'EMAIL_TAKEN':
      return 'auth.errorEmailTaken';
    case 'VALIDATION_FAILED':
      return 'auth.errorValidation';
    default:
      return error.status === 0 ? 'auth.errorOffline' : 'auth.errorRequest';
  }
}
