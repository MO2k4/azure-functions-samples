#!/usr/bin/env bash
# Sends the Part 3 security cases and the Part 4 version, CORS and product cases to a deployed
# ApimOrdersDemo and prints status code and body start for each. Not yet run against a live deployment: the expected codes come from Microsoft Learn, and
# this script is how you find out what your deployment actually returns.
#
# The J* and I* cases temporarily replace the API policy of the `orders` API on the instance and
# restore the committed infra/policies/api-orders.xml afterwards (also on error). The P* cases create
# two subscriptions (verify-free, verify-pending) and delete them at the end. Don't point it at a
# shared instance.
#
# Needs: az (signed in), azd environment from `azd up`, curl, jq.
# Env:   CLIENT_SECRET     secret of the test client app (README, "Entra ID and managed identity")
#        EXPIRED_TOKEN     optional, a gateway token saved more than ~75 minutes ago (case V9)
#        APIM_SUBSCRIPTION optional, APIM subscription to take the key from (default: contoso-shop)
# Usage: CLIENT_SECRET=... scripts/verify.sh [path-to-ApimOrdersDemo] | tee verify.log
set -uo pipefail
cd "${1:-$(dirname "$0")/..}"
eval "$(azd env get-values)"
: "${CLIENT_SECRET:?set CLIENT_SECRET}"
RG=${AZURE_RESOURCE_GROUP:-rg-$AZURE_ENV_NAME}
SUB_ID=${AZURE_SUBSCRIPTION_ID:-$(az account show --query id -o tsv)}
APIM_ARM="https://management.azure.com/subscriptions/$SUB_ID/resourceGroups/$RG/providers/Microsoft.ApiManagement/service/$APIM_NAME"
GW="$APIM_GATEWAY_URL/sales/orders"
FN="$FUNCTION_BASE_URL/orders"
KEY=$(az rest --method post --url "$APIM_ARM/subscriptions/${APIM_SUBSCRIPTION:-contoso-shop}/listSecrets?api-version=2024-05-01" --query primaryKey -o tsv)
WORK=$(mktemp -d)

token() { # $1 = app ID the token is for
  curl -s "https://login.microsoftonline.com/$AZURE_TENANT_ID/oauth2/v2.0/token" \
    -d grant_type=client_credentials -d client_id="$TEST_CLIENT_APP_ID" \
    --data-urlencode client_secret="$CLIENT_SECRET" -d scope="$1/.default" | jq -r .access_token
}
claims() { # JWT payload is unpadded base64url
  local p; p=$(cut -d. -f2 <<<"$1" | tr '_-' '/+')
  while (( ${#p} % 4 )); do p+='='; done
  base64 -d <<<"$p" 2>/dev/null | jq -c '{aud,iss,ver,azp,roles,exp}'
}
call() { # $1 label, rest = curl args
  local label=$1; shift
  printf '\n### %s\n' "$label"
  curl -s -o "$WORK/body" -w 'HTTP %{http_code}\n' "$@"; head -c 400 "$WORK/body"; echo
}
put_api_policy() { # $1 = xml file; the sleep gives the gateway time to pick up the change
  jq -n --rawfile v "$1" '{properties:{format:"rawxml",value:$v}}' > "$WORK/policy.json"
  az rest --method put --url "$APIM_ARM/apis/orders/policies/policy?api-version=2024-05-01" \
    --body @"$WORK/policy.json" --query properties.format -o tsv; sleep 30
}

GOOD=$(token "$ORDERS_API_APP_ID")
WRONG_AUD=$(token "$BACKEND_APP_ID")
echo "good token: $(claims "$GOOD")"; echo "wrong-aud token: $(claims "$WRONG_AUD")"
TAMPERED="${GOOD%.*}.$(rev <<<"${GOOD##*.}")"

call "V1 valid token + key (expect 200)" -H "Ocp-Apim-Subscription-Key: $KEY" -H "Authorization: Bearer $GOOD" "$GW"
call "V2 key, no token (expect 401)" -H "Ocp-Apim-Subscription-Key: $KEY" "$GW"
call "V3 token for the backend app, wrong audience (expect 401)" -H "Ocp-Apim-Subscription-Key: $KEY" -H "Authorization: Bearer $WRONG_AUD" "$GW"
call "V4 tampered signature (expect 401)" -H "Ocp-Apim-Subscription-Key: $KEY" -H "Authorization: Bearer $TAMPERED" "$GW"
call "V5 valid token, no subscription key (expect 401)" -H "Authorization: Bearer $GOOD" "$GW"
call "V6 function directly, no token (expect 401, Return401)" "$FN"
call "V7 function directly, right audience, not APIM's identity (expect 403)" -H "Authorization: Bearer $WRONG_AUD" "$FN"
call "V8 function directly, partner token for the gateway (status not documented)" -H "Authorization: Bearer $GOOD" "$FN"
[ -n "${EXPIRED_TOKEN:-}" ] && call "V9 expired token (expect 401)" -H "Ocp-Apim-Subscription-Key: $KEY" -H "Authorization: Bearer $EXPIRED_TOKEN" "$GW"

ORIG="$WORK/api-orders.xml"; cp infra/policies/api-orders.xml "$ORIG"
trap 'put_api_policy "$ORIG"' EXIT
TID=$AZURE_TENANT_ID
jwt_variant() { # $1 = expected issuer; the caller allowlist becomes a required azp claim
  sed -e "s#<validate-azure-ad-token tenant-id=\"{{tenant-id}}\">#<validate-jwt header-name=\"Authorization\" failed-validation-httpcode=\"401\" require-scheme=\"Bearer\"><openid-config url=\"https://login.microsoftonline.com/$TID/v2.0/.well-known/openid-configuration\" />#" \
      -e '/<client-application-ids>/,/<\/client-application-ids>/d' \
      -e "s#</audiences>#</audiences><issuers><issuer>$1</issuer></issuers>#" \
      -e 's#<required-claims>#<required-claims><claim name="azp" match="all"><value>{{orders-test-client-app-id}}</value></claim>#' \
      -e 's#</validate-azure-ad-token>#</validate-jwt>#' "$ORIG" > "$WORK/jwt.xml"
}
jwt_variant "https://login.microsoftonline.com/$TID/v2.0"; put_api_policy "$WORK/jwt.xml"
call "J1 validate-jwt, v2 issuer, valid token (expect 200)" -H "Ocp-Apim-Subscription-Key: $KEY" -H "Authorization: Bearer $GOOD" "$GW"
call "J2 validate-jwt, no token (expect 401)" -H "Ocp-Apim-Subscription-Key: $KEY" "$GW"
call "J3 validate-jwt, wrong audience (expect 401)" -H "Ocp-Apim-Subscription-Key: $KEY" -H "Authorization: Bearer $WRONG_AUD" "$GW"
jwt_variant "https://sts.windows.net/$TID/"; put_api_policy "$WORK/jwt.xml"
call "J4 validate-jwt expecting the v1 issuer, v2 token (expect 401)" -H "Ocp-Apim-Subscription-Key: $KEY" -H "Authorization: Bearer $GOOD" "$GW"

sed 's#<ip-filter action="forbid">#<ip-filter action="allow">#' "$ORIG" > "$WORK/ip.xml"; put_api_policy "$WORK/ip.xml"
call "I1 ip-filter allow-list without the caller's address (expect 403)" -H "Ocp-Apim-Subscription-Key: $KEY" -H "Authorization: Bearer $GOOD" "$GW"

put_api_policy "$ORIG"; trap - EXIT
call "R1 committed policy restored (expect 200)" -H "Ocp-Apim-Subscription-Key: $KEY" -H "Authorization: Bearer $GOOD" "$GW"

# Part 4: versions, the portal's CORS preflight, products.
PORTAL="https://$APIM_NAME.developer.azure-api.net"
subscription() { # $1 name, $2 product, $3 state; prints the primary key
  az rest --method put --url "$APIM_ARM/subscriptions/$1?api-version=2024-05-01" \
    --body "{\"properties\":{\"displayName\":\"$1\",\"scope\":\"/products/$2\",\"state\":\"$3\"}}" -o none
  az rest --method post --url "$APIM_ARM/subscriptions/$1/listSecrets?api-version=2024-05-01" --query primaryKey -o tsv
}
cleanup() {
  for s in verify-free verify-pending; do
    az rest --method delete --url "$APIM_ARM/subscriptions/$s?api-version=2024-05-01" -o none 2>/dev/null
  done
}
trap cleanup EXIT

call "P1 Original version still on the unversioned URL (expect 200, JSON array)" -H "Ocp-Apim-Subscription-Key: $KEY" -H "Authorization: Bearer $GOOD" "$GW"
call "P2 v2 (expect 200, {\"items\":[...],\"nextLink\":...})" -H "Ocp-Apim-Subscription-Key: $KEY" -H "Authorization: Bearer $GOOD" "$APIM_GATEWAY_URL/sales/v2/orders"
call "P3 unknown version identifier (status not documented)" -H "Ocp-Apim-Subscription-Key: $KEY" -H "Authorization: Bearer $GOOD" "$APIM_GATEWAY_URL/sales/v9/orders"

printf '\n### P4 preflight from the portal origin, no key (expect 200 with Access-Control-Allow-Origin)\n'
curl -s -o /dev/null -D - -X OPTIONS -H "Origin: $PORTAL" -H "Access-Control-Request-Method: GET" \
  -H "Access-Control-Request-Headers: ocp-apim-subscription-key,authorization" "$GW" | grep -iE '^(HTTP|access-control)'
printf '\n### P5 preflight from another origin (expect empty 200, no Access-Control-Allow-Origin)\n'
curl -s -o /dev/null -D - -X OPTIONS -H "Origin: https://example.com" -H "Access-Control-Request-Method: GET" \
  "$GW" | grep -iE '^(HTTP|access-control)'

FREE_KEY=$(subscription verify-free orders-free active); sleep 10
for i in 1 2 3 4 5 6; do
  call "P6.$i free product key, call $i in a minute (expect 200 for 1-5, 429 on 6)" -H "Ocp-Apim-Subscription-Key: $FREE_KEY" -H "Authorization: Bearer $GOOD" "$GW"
done

PENDING_KEY=$(subscription verify-pending orders-partners submitted); sleep 10
call "P7 partner subscription still submitted (expect 401)" -H "Ocp-Apim-Subscription-Key: $PENDING_KEY" -H "Authorization: Bearer $GOOD" "$GW"
az rest --method patch --url "$APIM_ARM/subscriptions/verify-pending?api-version=2024-05-01" \
  --body '{"properties":{"state":"active"}}' -o none; sleep 10
call "P8 same key after approval (expect 200)" -H "Ocp-Apim-Subscription-Key: $PENDING_KEY" -H "Authorization: Bearer $GOOD" "$GW"
