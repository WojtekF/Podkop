/// <reference types="vitest" />
import { defineConfig } from 'vite';
import angular from '@analogjs/vite-plugin-angular';

// Single source of truth for the test runner: `npm test`, `ng test`
// (@analogjs/vitest-angular:test builder), and the Vitest VS Code
// extension all read this config.
export default defineConfig({
  plugins: [angular()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['src/test-setup.ts'],
    include: ['src/**/*.spec.ts'],
    reporters: ['default'],
  },
});
