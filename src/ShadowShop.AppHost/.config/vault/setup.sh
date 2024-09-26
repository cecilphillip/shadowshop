export VAULT_ADDR=http://0.0.0.0:$VAULT_PORT

echo "Enabling KV secrets engine at path 'shadowshop'"
vault secrets enable -path='shadowshop' -version=2 kv

echo "Writing stripe keys"
vault kv put -mount=shadowshop stripe public_key=""
vault kv patch -mount=shadowshop stripe secret_key=""
vault kv patch -mount=shadowshop stripe webhook_secret=""

exit 0