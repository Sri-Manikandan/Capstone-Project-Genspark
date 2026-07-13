// Production environment configuration.
// These values are swapped in for environment.ts during a production build
// (see the "fileReplacements" entry in angular.json).
//
// __INGRESS_FQDN__ is substituted by the deploy-web workflow at build time from the AKS
// ingress hostname, so the value is never hardcoded here and cannot go stale.
export const environment = {
  production: true,
  apiBaseUrl: 'https://__INGRESS_FQDN__',
  // Stripe TEST publishable key. This key is public and safe to ship in the client bundle.
  // It must stay pk_test_ — this is a demo and must never be able to move real money.
  // deploy-web.yml fails the build if a pk_live_ key is found in the bundle.
  stripePublishableKey:
    'pk_test_51Tk1KgDlDAgZgiPTi3aYubmttJEhLKybjjIiOjf9bPknQz70gArdOxFqQNZM2Z0tT90anDA3zw99DdVNCjf73Ul000LaAknaU3',
};
