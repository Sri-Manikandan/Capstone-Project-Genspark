# EMSAngular

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 22.0.3.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Production configuration

A production build (`ng build`, which defaults to the `production` configuration) swaps
`src/environments/environment.ts` for `src/environments/environment.prod.ts` via the
`fileReplacements` entry in `angular.json`. Before deploying, replace the placeholders in
`environment.prod.ts`:

| Field | Replace with | Example |
| --- | --- | --- |
| `apiBaseUrl` | Base URL of the deployed API (no trailing slash) | `https://api.your-domain.com` |
| `stripePublishableKey` | Stripe **publishable** key for the live account (public, safe to ship) | `pk_live_...` |

Never put the Stripe **secret** key or any server-side secret in this file — it is bundled
into the client and visible to users.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
