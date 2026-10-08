import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { Observable, catchError, finalize, shareReplay, tap, throwError } from 'rxjs';
import { AuthResponse, User } from './api.models';

const SESSION_KEY = 'money-tracker.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly user = signal<User | null>(null);
  private session = this.readSession();
  private refreshRequest: Observable<AuthResponse> | null = null;

  constructor(private readonly http: HttpClient) {
    this.user.set(this.session?.user ?? null);
  }

  get accessToken(): string | null {
    return this.session?.accessToken ?? null;
  }

  get refreshToken(): string | null {
    return this.session?.refreshToken ?? null;
  }

  get isAuthenticated(): boolean {
    return this.session !== null;
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/login', { email, password })
      .pipe(tap((response) => this.saveSession(response)));
  }

  register(email: string, password: string, displayName: string): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/register', { email, password, displayName })
      .pipe(tap((response) => this.saveSession(response)));
  }

  refreshSession(): Observable<AuthResponse> {
    const refreshToken = this.refreshToken;
    if (!refreshToken) {
      return throwError(() => new Error('No refresh token is available.'));
    }

    if (!this.refreshRequest) {
      this.refreshRequest = this.http
        .post<AuthResponse>('/api/auth/refresh', { refreshToken })
        .pipe(
          tap((response) => this.saveSession(response)),
          catchError((error: unknown) => {
            this.clearSession();
            return throwError(() => error);
          }),
          finalize(() => (this.refreshRequest = null)),
          shareReplay({ bufferSize: 1, refCount: false }),
        );
    }

    return this.refreshRequest;
  }

  logout(): Observable<void> {
    const refreshToken = this.refreshToken;
    this.clearSession();

    return refreshToken
      ? this.http.post<void>('/api/auth/logout', { refreshToken })
      : new Observable<void>((subscriber) => {
          subscriber.next();
          subscriber.complete();
        });
  }

  clearSession(): void {
    this.session = null;
    this.user.set(null);
    localStorage.removeItem(SESSION_KEY);
  }

  private saveSession(response: AuthResponse): void {
    this.session = response;
    this.user.set(response.user);
    localStorage.setItem(SESSION_KEY, JSON.stringify(response));
  }

  private readSession(): AuthResponse | null {
    const saved = localStorage.getItem(SESSION_KEY);
    if (!saved) return null;

    try {
      const session: AuthResponse = JSON.parse(saved);
      if (session.accessToken && session.refreshToken && session.user?.id) return session;
    } catch (error) {
      console.warn('Stored MoneyTracker session could not be read.', error);
    }

    localStorage.removeItem(SESSION_KEY);
    return null;
  }
}
