import { defineConfig, devices } from '@playwright/test';

// Node global; declared here so the suite needs no @types/node.
declare const process: { env: Record<string, string | undefined> };

/**
 * End-to-end suite: the demo script against the running stack.
 * Start it first, without an Anthropic key so the Vendor Assistant answers deterministically:
 *   ANTHROPIC_API_KEY= docker compose up -d --build
 * Every test resets the demo, so tests share one database and run one at a time.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env['E2E_BASE_URL'] ?? 'http://localhost:8080',
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
