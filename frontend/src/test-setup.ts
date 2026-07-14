import '@angular/compiler';
import '@analogjs/vitest-angular/setup-snapshots';
import { setupTestBed } from '@analogjs/vitest-angular/setup-testbed';

// Zoneless test environment (this app has no zone.js dependency).
setupTestBed();
