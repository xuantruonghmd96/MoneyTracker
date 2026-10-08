import { HttpErrorResponse } from '@angular/common/http';
import { ApiError } from './api.models';

export function authErrorMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Something went wrong. Please try again.';
  }

  const response = error.error as ApiError | null;
  switch (response?.error) {
    case 'INVALID_CREDENTIALS':
      return 'That email and password combination was not recognized.';
    case 'EMAIL_TAKEN':
      return 'An account with this email already exists. Try signing in instead.';
    case 'VALIDATION_FAILED':
      return 'Please check your details and try again.';
    default:
      return error.status === 0
        ? 'Could not reach MoneyTracker. Check that the API is running.'
        : 'We could not complete your request. Please try again.';
  }
}
