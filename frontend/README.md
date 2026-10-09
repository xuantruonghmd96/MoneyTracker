# MoneyTracker frontend

Angular web client for the MoneyTracker API. The first stage includes login,
registration, a responsive transactions list with wallet and period filters,
and an account screen with logout.

## Run locally

Start the API on `http://localhost:5100` with the `http` launch profile, then:

```bash
npm install
npm start
```

Open `http://localhost:4200`. The Angular development server forwards `/api`
requests to the API through `proxy.conf.json`.

## Deploy to Vercel

The `vercel.json` rewrites proxy `/api/*` requests to the deployed Render API
and send other paths to `index.html` for Angular routing. API calls in the
Angular app should therefore remain relative (for example, `/api/transactions`).

1. Push the repository, including `frontend/vercel.json`, to GitHub.
2. In Vercel, choose **Add New → Project**, import the GitHub repository, and
   set **Root Directory** to `frontend`.
3. Use these project settings:
   - **Framework Preset:** Angular
   - **Build Command:** `npm run build`
   - **Install Command:** `npm ci`
   - **Output Directory:** `dist/frontend/browser`
4. Click **Deploy**. Once complete, open the assigned `*.vercel.app` URL and
   test registration/login and an authenticated API-backed screen.
5. For later updates, push to the connected GitHub branch; Vercel will rebuild
   and deploy automatically.

The browser calls the Vercel origin and Vercel forwards API traffic to Render,
so the frontend does not need a separately configured API base URL or a browser
CORS exception for this setup. The API must still be awake and reachable on
Render; the free service may take a while to respond after being idle.

## Project structure

- `src/app/core` — authentication, JWT interceptor, route guards, API service,
  and API models.
- `src/app/features/auth` — login and registration screens.
- `src/app/features/transactions` — day/week/month/quarter/year navigation,
  swipe gestures, wallet and text filtering, and grouped transactions.
- `src/app/features/account` — account details and logout.
- `src/app/shared` — authenticated application shell and later-stage placeholders.

Access and refresh tokens are stored in browser local storage. Expired access
tokens are refreshed automatically after an API `401` response. The wallet header currently shows the selected wallet’s opening balance; “All
wallets” shows opening balances grouped by currency. Periods can be viewed by
day, week (Monday–Sunday), month, quarter, or year. The three-period selector
shows the previous, selected, and next periods; horizontal swipes over the
summary and transaction list also navigate between periods.

The mobile navigation includes Categories, Report, Account, and Add transaction.
Categories, Report, and Add transaction are placeholders in this first stage.
The Account screen displays the signed-in profile and provides a logout action.

## Build and tests

```bash
npm run build
npm test -- --watch=false
```
