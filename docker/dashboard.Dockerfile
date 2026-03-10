FROM node:22-alpine AS build
WORKDIR /app

COPY src/dashboard/package.json src/dashboard/package-lock.json* ./
RUN npm ci

COPY src/dashboard/ .
RUN npm run build

FROM nginx:alpine AS runtime
# Install Node.js for the auth server
RUN apk add --no-cache nodejs

COPY --from=build /app/dist /usr/share/nginx/html
COPY docker/nginx.conf /etc/nginx/templates/default.conf.template
COPY docker/auth-server.mjs /opt/auth-server.mjs
COPY docker/dashboard-entrypoint.sh /docker-entrypoint.d/99-auth-server.sh
RUN chmod +x /docker-entrypoint.d/99-auth-server.sh

EXPOSE 80
