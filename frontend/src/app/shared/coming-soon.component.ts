import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

const FEATURE_COPY: Record<string, { title: string; detail: string }> = {
  categories: {
    title: 'Categories',
    detail: 'Organize your spending and income with categories.',
  },
  report: {
    title: 'Reports',
    detail: 'Explore a deeper breakdown of your finances.',
  },
  account: {
    title: 'Your account',
    detail: 'Manage your profile and preferences.',
  },
  'add-transaction': {
    title: 'Add a transaction',
    detail: 'Transaction creation is coming in a later stage.',
  },
};

@Component({
  selector: 'app-coming-soon',
  imports: [RouterLink],
  template: `
    <section class="placeholder">
      <span class="placeholder-icon" aria-hidden="true">✳</span>
      <p class="eyebrow">COMING IN A LATER STAGE</p>
      <h1>{{ feature.title }}</h1>
      <p class="placeholder-copy">{{ feature.detail }}</p>
      <a routerLink="/">Back to transactions <span aria-hidden="true">→</span></a>
    </section>
  `,
  styles: `
    .placeholder {
      display: grid;
      min-height: 55vh;
      align-content: center;
      justify-items: center;
      padding: 40px 20px;
      text-align: center;
    }
    .placeholder-icon {
      display: grid;
      width: 54px;
      height: 54px;
      place-items: center;
      border: 1px solid rgba(166, 217, 154, 0.2);
      border-radius: 18px;
      background: rgba(166, 217, 154, 0.08);
      color: var(--green);
      font-size: 23px;
    }
    .eyebrow {
      margin: 22px 0 9px;
      color: var(--muted);
      font-size: 10px;
      font-weight: 700;
      letter-spacing: 1px;
    }
    h1 {
      margin: 0;
      color: var(--text);
      font-size: 26px;
      font-weight: 600;
    }
    .placeholder-copy {
      color: var(--muted);
      font-size: 13px;
    }
    a {
      display: flex;
      gap: 9px;
      align-items: center;
      margin-top: 12px;
      color: var(--green);
      font-size: 12px;
      text-decoration: none;
    }
  `,
})
export class ComingSoonComponent {
  readonly feature: { title: string; detail: string };

  constructor() {
    const key = inject(ActivatedRoute).snapshot.data['feature'] as string;
    this.feature = FEATURE_COPY[key] ?? FEATURE_COPY['add-transaction'];
  }
}
