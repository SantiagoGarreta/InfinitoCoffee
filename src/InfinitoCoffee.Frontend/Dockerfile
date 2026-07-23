FROM node:24-alpine AS build
WORKDIR /app

COPY src/InfinitoCoffee.Frontend/package.json src/InfinitoCoffee.Frontend/package-lock.json ./
RUN npm ci

COPY src/InfinitoCoffee.Frontend/ ./
RUN npm run build

FROM nginx:1.29-alpine
COPY src/InfinitoCoffee.Frontend/nginx/default.conf /etc/nginx/conf.d/default.conf
COPY src/InfinitoCoffee.Frontend/docker/40-generate-runtime-config.sh /docker-entrypoint.d/40-generate-runtime-config.sh
RUN chmod +x /docker-entrypoint.d/40-generate-runtime-config.sh
COPY --from=build /app/dist/InfinitoCoffee.Frontend/browser/ /usr/share/nginx/html/
RUN cp /usr/share/nginx/html/index.csr.html /usr/share/nginx/html/index.html
