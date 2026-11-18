// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

const site = 'https://relate.ambystech.io';

export default defineConfig({
  site,
  integrations: [
    starlight({
      title: 'Relate.NET',
      description: 'Relate.NET: a .NET client for Relate by Neo4j, the embedded Cypher graph database for AI agents.',
      logo: { src: './src/assets/pet.png', alt: 'Relate.NET mascot' },
      favicon: '/favicon.png',
      social: [
        { icon: 'github', label: 'GitHub', href: 'https://github.com/ambystechcom/Ambystech.Relate' },
      ],
      editLink: {
        baseUrl: 'https://github.com/ambystechcom/Ambystech.Relate/edit/main/site/',
      },
      customCss: ['./src/styles/custom.css'],
      head: [
        { tag: 'meta', attrs: { property: 'og:image', content: `${site}/og.png` } },
        { tag: 'meta', attrs: { name: 'twitter:card', content: 'summary_large_image' } },
        { tag: 'meta', attrs: { name: 'twitter:image', content: `${site}/og.png` } },
      ],
      sidebar: [
        {
          label: 'Getting started',
          items: [
            { label: 'Install', slug: 'getting-started/install' },
            { label: 'Quick start', slug: 'getting-started/quick-start' },
          ],
        },
        {
          label: 'Guides',
          items: [
            { label: 'Transactions', slug: 'guides/transactions' },
            { label: 'Vector index', slug: 'guides/vector-index' },
            { label: 'Agent memory', slug: 'guides/agent-memory' },
            { label: 'Errors', slug: 'guides/errors' },
            { label: 'ADO.NET', slug: 'guides/ado-net' },
          ],
        },
        {
          label: 'Reference',
          items: [
            { label: 'Type mapping', slug: 'reference/type-mapping' },
            { label: 'Threading and store model', slug: 'reference/threading-and-store-model' },
            { label: 'Native library resolution', slug: 'reference/native-library-resolution' },
            { label: 'Changelog', slug: 'reference/changelog' },
          ],
        },
      ],
    }),
  ],
});
