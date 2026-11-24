FROM nginx:alpine

# Copiar archivos estáticos del frontend
COPY Front/ /usr/share/nginx/html/

# Copiar configuración personalizada de nginx
COPY Front/nginx.conf /etc/nginx/conf.d/default.conf

# Script de entrada para generar config.js dinámicamente
COPY Front/docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

EXPOSE 80

ENTRYPOINT ["/docker-entrypoint.sh"]
CMD ["nginx", "-g", "daemon off;"]
