FROM node:22-alpine AS build
WORKDIR /app

COPY src/dashboard/package.json src/dashboard/package-lock.json* ./
RUN npm ci

COPY src/dashboard/ .
RUN npm run build

FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY docker/nginx.conf /etc/nginx/templates/default.conf.template
EXPOSE 80
