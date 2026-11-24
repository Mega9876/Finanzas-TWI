#!/bin/sh
set -e

# Generar config.js con la URL de la API proporcionada por variable de entorno
# Por defecto apunta a http://localhost:5000 (puerto del backend en dev)
cat <<EOF > /usr/share/nginx/html/config.js
// Configuración dinámica generada por docker-entrypoint.sh
window.APP_CONFIG = {
    API_URL: "${API_URL:-http://localhost:5000}"
};
EOF

echo "[frontend] config.js generado con API_URL: ${API_URL:-http://localhost:5000}"

exec "$@"
