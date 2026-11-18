// Copies the repo's source-of-truth docs and branding assets into the Starlight project.
// Outputs are generated (gitignored): edit ../docs, ../CHANGELOG.md and ../public instead.
import { copyFileSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const siteRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const repoRoot = resolve(siteRoot, '..');
const reference = join(siteRoot, 'src/content/docs/reference');

rmSync(reference, { recursive: true, force: true });
mkdirSync(reference, { recursive: true });

// Reference pages: docs/*.md + CHANGELOG.md, with Starlight frontmatter derived from the H1.
const sources = [
  ...readdirSync(join(repoRoot, 'docs'))
    .filter((f) => f.endsWith('.md'))
    .map((f) => ({ from: join(repoRoot, 'docs', f), to: f })),
  { from: join(repoRoot, 'CHANGELOG.md'), to: 'changelog.md' },
];

for (const { from, to } of sources) {
  const text = readFileSync(from, 'utf8').replace(/\r\n/g, '\n');
  const match = text.match(/^# (.+)\n+/);
  if (!match) throw new Error(`${from}: expected a leading "# Title" heading`);
  const title = match[1].trim().replace(/"/g, '\\"');
  writeFileSync(join(reference, to), `---\ntitle: "${title}"\n---\n\n${text.slice(match[0].length)}`);
}

// Branding: logo, favicon and social image come from the repo's public/ folder.
mkdirSync(join(siteRoot, 'src/assets'), { recursive: true });
mkdirSync(join(siteRoot, 'public'), { recursive: true });
copyFileSync(join(repoRoot, 'public/pet.png'), join(siteRoot, 'src/assets/pet.png'));
copyFileSync(join(repoRoot, 'public/pet.png'), join(siteRoot, 'public/favicon.png'));
copyFileSync(join(repoRoot, 'public/og.png'), join(siteRoot, 'public/og.png'));

console.log(`Synced ${sources.length} reference pages and branding assets.`);
