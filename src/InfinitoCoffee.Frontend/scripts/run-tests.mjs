import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const forwardedArgs = process.argv.slice(2);
const hasRunFlag = forwardedArgs.includes('--run');
const filteredArgs = forwardedArgs.filter((arg) => arg !== '--run');
const ngExecutable = process.execPath;
const ngCliPath = fileURLToPath(new URL('../node_modules/@angular/cli/bin/ng.js', import.meta.url));
const ngArgs = [ngCliPath, 'test'];

if (hasRunFlag) {
  ngArgs.push('--watch=false');
}

ngArgs.push(...filteredArgs);

const result = spawnSync(ngExecutable, ngArgs, {
  stdio: 'inherit',
  shell: false,
});

if (result.error) {
  console.error(result.error);
}

process.exit(result.status ?? 1);
