// Production environment configuration.
// These values are swapped in for environment.ts during a production build
// (see the "fileReplacements" entry in angular.json).
//
// Replace the placeholders below at deploy time. See EMSAngular/README.md
// ("Production configuration") for what each value should be.
export const environment = {
  production: true,
  // Base URL of the deployed API, e.g. 'https://api.your-domain.com'.
  apiBaseUrl: 'https://REPLACE_WITH_PRODUCTION_API_URL',
  // Stripe publishable key for the live account, e.g. 'pk_live_...'.
  // This key is public and safe to ship in the client bundle.
  stripePublishableKey: 'pk_live_REPLACE_WITH_YOUR_KEY',
};
