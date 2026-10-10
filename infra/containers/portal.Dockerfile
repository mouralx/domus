FROM node:24-bookworm-slim AS build
WORKDIR /app
COPY src/portal/package.json src/portal/package-lock.json ./
RUN npm ci
COPY src/portal ./
RUN npm run build
FROM nginx:stable-alpine
COPY infra/containers/portal.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
