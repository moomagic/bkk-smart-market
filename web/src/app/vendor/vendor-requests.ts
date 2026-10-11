import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Button } from 'primeng/button';
import { StallRequest } from '../core/api';
import { StatusTag } from '../shared/status-tag';
import { CATEGORY_LABEL, formatThaiDate } from '../shared/labels';
import { cardLine } from './request-text';
import { VendorStore } from './vendor-store';

/** Tab คำขอ: the vendor's requests, each a card leading to its own screen. */
@Component({
  selector: 'app-vendor-requests',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, RouterLink, Button, StatusTag],
  templateUrl: './vendor-requests.html',
  styleUrl: './vendor-requests.scss',
})
export class VendorRequests {
  private readonly store = inject(VendorStore);

  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly date = formatThaiDate;
  protected readonly line = cardLine;

  protected readonly myRequests = this.store.myRequests;
  protected readonly current = this.store.current;
  protected readonly finished = this.store.finished;

  /** The request-card template hands its row over untyped; this restores the type for the checker. */
  protected asRequest(req: StallRequest): StallRequest {
    return req;
  }

  /** Retry after a failed load. */
  protected reload(): void {
    this.store.reload();
  }
}
