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
