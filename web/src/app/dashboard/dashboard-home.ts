import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { Api, Category, DashboardKpis, Ratio } from '../core/api';
import { DemoSession } from '../core/demo-session';
import { CATEGORY_LABEL, formatThaiDate } from '../shared/labels';

type Load = { state: 'loading' } | { state: 'error' } | { state: 'ready'; data: DashboardKpis };

const CATEGORIES: Category[] = ['Legacy', 'Vulnerable', 'New'];

@Component({
  selector: 'app-dashboard-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard-home.html',
  styleUrl: './dashboard-home.scss',
})
export class DashboardHome {
  private readonly api = inject(Api);
  protected readonly session = inject(DemoSession);

  protected readonly categories = CATEGORIES;
  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly date = formatThaiDate;

  protected readonly canRead = computed(() => this.session.role() !== 'vendor');

  protected readonly kpis = toSignal(
    toObservable(this.canRead).pipe(
      switchMap((ok) =>
        ok
          ? this.api.dashboard().pipe(
              map((data) => ({ state: 'ready', data }) as Load),
              catchError(() => of({ state: 'error' } as Load)),
              startWith({ state: 'loading' } as Load),
            )
          : of({ state: 'error' } as Load),
      ),
    ),
    { initialValue: { state: 'loading' } as Load },
  );

  protected readonly data = computed(() => {
    const k = this.kpis();
    return k.state === 'ready' ? k.data : null;
  });

  /** One shared scale for every bar in the quota chart: the largest quota or winner count. */
  protected readonly scaleMax = computed(() =>
    Math.max(1, ...(this.data()?.spaces ?? []).flatMap((s) => s.quota.flatMap((q) => [q.base, q.winners]))),
  );

  protected pct(r: Ratio): string {
    return r.percent === null ? '–' : `${r.percent.toLocaleString('th-TH', { maximumFractionDigits: 1 })}%`;
  }

  protected width(n: number): string {
    return `${(100 * n) / this.scaleMax()}%`;
  }

  protected quotaFor(row: DashboardKpis['spaces'][number], c: Category) {
    return row.quota.find((q) => q.category === c) ?? null;
  }
}
