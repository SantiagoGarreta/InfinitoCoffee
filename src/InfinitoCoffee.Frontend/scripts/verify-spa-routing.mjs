import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const frontendRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

const appHtml = read('src/app/app.html');
assert.match(appHtml, /<router-outlet\s*\/>/);
assert.doesNotMatch(appHtml, /<nav|app-authenticated-sidebar/);

const sidebarHtml = read('src/app/layout/authenticated-sidebar/authenticated-sidebar.component.html');
assert.match(sidebarHtml, /routerLink="\/admin"/);
assert.match(sidebarHtml, /routerLink="\/kitchen"/);
assert.match(sidebarHtml, /routerLink="\/orders\/new"/);
assert.match(sidebarHtml, /href="\/pickup"[^>]*target="_blank"[^>]*rel="noopener"/);
assert.doesNotMatch(appHtml, /http:\/\/localhost\/(?:kitchen|pickup|orders\/new)/);

const appTs = read('src/app/app.ts');
assert.match(appTs, /imports:\s*\[\s*RouterOutlet\s*\]/s);

const indexHtml = read('src/index.html');
assert.match(indexHtml, /<base href="\/">/);

const nginxConfig = read('nginx/default.conf');
assert.match(nginxConfig, /location \/ \{\s*try_files \$uri \$uri\/ \/index\.html;\s*\}/s);
assert.doesNotMatch(nginxConfig, /https?:\/\/localhost(?!:4200)/);

const dockerfile = read('Dockerfile');
assert.match(dockerfile, /cp \/usr\/share\/nginx\/html\/index\.csr\.html \/usr\/share\/nginx\/html\/index\.html/s);
assert.match(
  dockerfile,
  /rm -rf \/usr\/share\/nginx\/html\/login \/usr\/share\/nginx\/html\/kitchen \/usr\/share\/nginx\/html\/pickup \/usr\/share\/nginx\/html\/orders/,
);

function read(relativePath) {
  return readFileSync(resolve(frontendRoot, relativePath), 'utf8');
}
