#!/usr/bin/env bash
#
# One-time provisioning for the EMS Azure deployment.
#
# WHY THIS IS A SCRIPT AND NOT A GITHUB WORKFLOW:
# Provisioning needs ARM access (`az login`). GitHub Actions would authenticate with an Entra
# app registration, which this subscription's permissions do not allow us to create. So this
# runs from your laptop, where you are already logged in. The two DEPLOY workflows do run in
# GitHub Actions, because they need only a Docker registry login and a kubeconfig.
#
# Run it once. It is idempotent enough to re-run, but it will not re-create what exists.
#
# Requires: az (logged in), kubectl, helm, gh (logged in), openssl.

set -euo pipefail

RG="rg-ems-prod"
LOCATION="southindia"
LETSENCRYPT_EMAIL="${LETSENCRYPT_EMAIL:-saisidharthan@synergeek.in}"

echo "==> Preflight"
for cmd in az kubectl helm gh openssl jq; do
  command -v "$cmd" >/dev/null || { echo "FATAL: $cmd not found"; exit 1; }
done
az account show >/dev/null || { echo "FATAL: run 'az login'"; exit 1; }
gh auth status >/dev/null 2>&1 || { echo "FATAL: run 'gh auth login'"; exit 1; }

# ── 1. Postgres admin password ────────────────────────────────────────────────────────
# Alphanumeric only, deliberately: base64 can emit + / = which are awkward inside an Npgsql
# connection string, and Azure Postgres rejects ' " and @.
if gh secret list | grep -q POSTGRES_ADMIN_PASSWORD; then
  echo "==> POSTGRES_ADMIN_PASSWORD already set in GitHub."
  echo "    This script cannot read it back. If you do not have it saved, delete the secret"
  echo "    and re-run, or reset the server password later."
  read -rp "    Enter the existing Postgres admin password: " PG_PASS
else
  PG_PASS="$(LC_ALL=C tr -dc 'A-Za-z0-9' < /dev/urandom | head -c 32)"
  gh secret set POSTGRES_ADMIN_PASSWORD --body "$PG_PASS"
  echo ""
  echo "    ############################################################"
  echo "    #  SAVE THIS NOW — Postgres admin password:"
  echo "    #  $PG_PASS"
  echo "    #  It is not recoverable from GitHub."
  echo "    ############################################################"
  echo ""
  read -rp "    Press Enter once you have saved it..."
fi

# ── 2. Infrastructure ─────────────────────────────────────────────────────────────────
echo "==> Creating resource group"
az group create -n "$RG" -l "$LOCATION" -o none

echo "==> Deploying Bicep (this takes ~10 minutes, mostly AKS)"
az deployment group create \
  --resource-group "$RG" \
  --name main \
  --template-file infra/main.bicep \
  --parameters postgresAdminPassword="$PG_PASS" \
  -o none

OUT="$(az deployment group show -g "$RG" -n main --query properties.outputs -o json)"
ACR_LOGIN_SERVER=$(echo "$OUT" | jq -r .acrLoginServer.value)
ACR_NAME=$(echo "$OUT"         | jq -r .acrName.value)
AKS_NAME=$(echo "$OUT"         | jq -r .aksName.value)
NODE_RG=$(echo "$OUT"          | jq -r .aksNodeResourceGroup.value)
KV_NAME=$(echo "$OUT"          | jq -r .keyVaultName.value)
PG_FQDN=$(echo "$OUT"          | jq -r .postgresFqdn.value)
PG_NAME=$(echo "$OUT"          | jq -r .postgresName.value)
WI_CLIENT_ID=$(echo "$OUT"     | jq -r .workloadIdentityClientId.value)
SWA_NAME=$(echo "$OUT"         | jq -r .swaName.value)
TENANT_ID=$(az account show --query tenantId -o tsv)

echo "    ACR:      $ACR_LOGIN_SERVER"
echo "    AKS:      $AKS_NAME  (node RG: $NODE_RG)"
echo "    KeyVault: $KV_NAME"
echo "    Postgres: $PG_FQDN"

# ── 3. Cluster credentials ────────────────────────────────────────────────────────────
echo "==> Fetching kubeconfig"
az aks get-credentials -g "$RG" -n "$AKS_NAME" --overwrite-existing

# ── 4. Ingress + cert-manager ─────────────────────────────────────────────────────────
# AKS allocates the LoadBalancer's public IP in its own node resource group. We do not
# pre-create the IP, because attaching an IP from rg-ems-prod would need a Network
# Contributor role assignment this subscription cannot make.
echo "==> Installing ingress-nginx"
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx >/dev/null
helm repo add jetstack https://charts.jetstack.io >/dev/null
helm repo update >/dev/null

helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx \
  --namespace ingress-nginx --create-namespace \
  --set controller.replicaCount=1 \
  --set controller.resources.requests.memory=128Mi \
  --wait --timeout 10m

echo "==> Waiting for the LoadBalancer to get a public IP"
for _ in $(seq 1 60); do
  INGRESS_IP=$(kubectl get svc -n ingress-nginx ingress-nginx-controller \
    -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || true)
  [ -n "$INGRESS_IP" ] && break
  sleep 10
done
[ -n "${INGRESS_IP:-}" ] || { echo "FATAL: LoadBalancer never got an IP"; exit 1; }
echo "    Ingress IP: $INGRESS_IP"

# The DNS label is what makes TLS possible at all: Let's Encrypt issues certificates for
# domain names, not bare IPs. No hostname => no cert => Stripe webhooks do not work.
echo "==> Attaching a DNS label to the ingress IP"
PIP_NAME=$(az network public-ip list -g "$NODE_RG" \
  --query "[?ipAddress=='$INGRESS_IP'].name | [0]" -o tsv)
DNS_LABEL="ems-api-$(echo "$AKS_NAME$RG" | shasum | head -c 8)"

az network public-ip update -g "$NODE_RG" -n "$PIP_NAME" --dns-name "$DNS_LABEL" -o none
INGRESS_FQDN=$(az network public-ip show -g "$NODE_RG" -n "$PIP_NAME" \
  --query dnsSettings.fqdn -o tsv)
echo "    Ingress FQDN: $INGRESS_FQDN"

echo "==> Installing cert-manager"
helm upgrade --install cert-manager jetstack/cert-manager \
  --namespace cert-manager --create-namespace \
  --set crds.enabled=true \
  --wait --timeout 10m

# ── 5. Postgres firewall ──────────────────────────────────────────────────────────────
# The AKS egress IP does not exist until AKS has built its load balancer, which is why this
# cannot live in the Bicep template.
echo "==> Allowing the AKS egress IP through the Postgres firewall"
EGRESS_IP=$(az network public-ip list -g "$NODE_RG" \
  --query "[?starts_with(name,'kubernetes')].ipAddress | [0]" -o tsv)
echo "    AKS egress IP: $EGRESS_IP"
az postgres flexible-server firewall-rule create \
  -g "$RG" -n "$PG_NAME" --rule-name allow-aks-egress \
  --start-ip-address "$EGRESS_IP" --end-ip-address "$EGRESS_IP" -o none

# ── 6. Image pull secret ──────────────────────────────────────────────────────────────
# The cleaner AcrPull managed-identity model needs a role assignment we cannot make, so
# Kubernetes pulls with the ACR admin credentials instead.
echo "==> Creating the ACR imagePullSecret"
ACR_USERNAME=$(az acr credential show -n "$ACR_NAME" --query username -o tsv)
ACR_PASSWORD=$(az acr credential show -n "$ACR_NAME" --query 'passwords[0].value' -o tsv)

kubectl create namespace ems --dry-run=client -o yaml | kubectl apply -f -
kubectl create secret docker-registry acr-pull \
  --namespace ems \
  --docker-server="$ACR_LOGIN_SERVER" \
  --docker-username="$ACR_USERNAME" \
  --docker-password="$ACR_PASSWORD" \
  --dry-run=client -o yaml | kubectl apply -f -

# ── 7. GitHub secrets and variables ───────────────────────────────────────────────────
echo "==> Setting GitHub secrets and variables"
SWA_TOKEN=$(az staticwebapp secrets list -g "$RG" -n "$SWA_NAME" \
  --query properties.apiKey -o tsv)
SWA_HOSTNAME=$(az staticwebapp show -g "$RG" -n "$SWA_NAME" \
  --query defaultHostname -o tsv)

# An admin kubeconfig: long-lived client certs. This is the credential OIDC would have
# replaced. Treat the GitHub secret as sensitive.
az aks get-credentials -g "$RG" -n "$AKS_NAME" --admin --overwrite-existing \
  --file /tmp/ems-kubeconfig-admin >/dev/null
gh secret set KUBE_CONFIG   --body "$(base64 < /tmp/ems-kubeconfig-admin)"
rm -f /tmp/ems-kubeconfig-admin

gh secret set ACR_PASSWORD        --body "$ACR_PASSWORD"
gh secret set SWA_DEPLOYMENT_TOKEN --body "$SWA_TOKEN"

gh variable set ACR_LOGIN_SERVER            --body "$ACR_LOGIN_SERVER"
gh variable set ACR_USERNAME                --body "$ACR_USERNAME"
gh variable set KEYVAULT_NAME               --body "$KV_NAME"
gh variable set WORKLOAD_IDENTITY_CLIENT_ID --body "$WI_CLIENT_ID"
gh variable set AZURE_TENANT_ID             --body "$TENANT_ID"
gh variable set INGRESS_FQDN                --body "$INGRESS_FQDN"
gh variable set SWA_HOSTNAME                --body "$SWA_HOSTNAME"
gh variable set LETSENCRYPT_EMAIL           --body "$LETSENCRYPT_EMAIL"

# ── 8. Key Vault secrets ──────────────────────────────────────────────────────────────
# SSL Mode=Require is not optional: Azure Postgres refuses plaintext connections.
echo "==> Writing Key Vault secrets"
az keyvault secret set --vault-name "$KV_NAME" --name db-connection-string \
  --value "Host=$PG_FQDN;Port=5432;Database=eventmanagement;Username=emsadmin;Password=$PG_PASS;SSL Mode=Require;Trust Server Certificate=true" \
  -o none
az keyvault secret set --vault-name "$KV_NAME" --name jwt-key \
  --value "$(openssl rand -base64 48)" -o none

echo ""
echo "############################################################################"
echo "  Provisioning done. THREE secrets still need real values before deploying:"
echo ""
echo "    az keyvault secret set --vault-name $KV_NAME --name stripe-secret-key     --value 'sk_test_...'"
echo "    az keyvault secret set --vault-name $KV_NAME --name stripe-webhook-secret --value 'whsec_...'"
echo "    az keyvault secret set --vault-name $KV_NAME --name resend-api-key        --value 're_...'"
echo ""
echo "  For stripe-webhook-secret, first register this endpoint in the Stripe"
echo "  dashboard (TEST mode) and copy the signing secret it gives you:"
echo ""
echo "    https://$INGRESS_FQDN/api/stripe/webhook"
echo ""
echo "  Then deploy:   git checkout -B prod && git push -u origin prod"
echo ""
echo "  API:      https://$INGRESS_FQDN"
echo "  Frontend: https://$SWA_HOSTNAME"
echo "############################################################################"
