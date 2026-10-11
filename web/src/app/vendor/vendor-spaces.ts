import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Button } from 'primeng/button';
import { SpaceSummary } from '../core/api';
import { DemoSession } from '../core/demo-session';
import { SpaceFacts } from './space-facts';
import { SpaceMatching } from './space-matching';
import { VendorStore } from './vendor-store';
import { StatusTag } from '../shared/status-tag';
import { formatThaiDate } from '../shared/labels';

/** Tab หาพื้นที่: Space Matching, then every space in the district with its open round. */
@Component({
  selector: 'app-vendor-spaces',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, SpaceFacts, SpaceMatching, StatusTag],
  templateUrl: './vendor-spaces.html',
  styleUrl: './vendor-spaces.scss',
})
export class VendorSpaces {
  private readonly store = inject(VendorStore);
  protected readonly session = inject(DemoSession);

  protected readonly date = formatThaiDate;

  protected readonly spaces = this.store.spaces;
  protected readonly spaceList = this.store.spaceList;
  protected readonly requestStatusByRound = this.store.requestStatusByRound;
  protected readonly closesAt = this.store.closesAt;
  protected readonly submitting = this.store.submitting;
  protected readonly matching = this.store.showingMatches;

  protected submit(space: SpaceSummary): void {
    this.store.submit(space);
  }

  /** Retry after a failed load. */
  protected reload(): void {
    this.store.reload();
  }
}
