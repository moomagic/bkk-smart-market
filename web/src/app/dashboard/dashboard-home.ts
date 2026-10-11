import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, from, map, of, startWith, switchMap } from 'rxjs';
import { Chart, ChartData, ChartOptions } from 'chart.js';
import annotationPlugin, { AnnotationOptions } from 'chartjs-plugin-annotation';
import { Card } from 'primeng/card';
import { ChartModule } from 'primeng/chart';
import { TableModule } from 'primeng/table';
import { Api, Category, DashboardKpis, Ratio } from '../core/api';
import { DemoSession } from '../core/demo-session';
import { CATEGORY_LABEL, formatThaiDate } from '../shared/labels';

type SpaceRow = DashboardKpis['spaces'][number];
type QuotaChart = { data: ChartData<'bar'>; options: ChartOptions<'bar'>; label: string };

type Load = { state: 'loading' } | { state: 'error' } | { state: 'ready'; data: DashboardKpis };

const CATEGORIES: Category[] = ['Legacy', 'Vulnerable', 'New'];

// App-wide registration in Chart.js's shared registry, done when the dashboard chunk loads; registering (rather than
// passing the plugin per chart) is what gives the plugin its defaults.
Chart.register(annotationPlugin);

@Component({
  selector: 'app-dashboard-home',
  imports: [Card, ChartModule, TableModule],
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

  /** Canvas text is drawn once, so the charts wait for the Thai body font rather than freezing the fallback. */
  private readonly fontsReady = toSignal(from(document.fonts.ready).pipe(map(() => true)), { initialValue: false });

  /** Bar chart per space with an approved run: winners per group, a vertical marker at each group's quota. */
  protected readonly charts = computed(() => {
    if (!this.fontsReady()) return new Map<string, QuotaChart>();
    const spaces = this.data()?.spaces ?? [];
    const max = this.scaleMax();
    const css = getComputedStyle(document.documentElement);
    const token = (name: string) => css.getPropertyValue(name).trim();
    const colour: Record<Category, string> = { Legacy: token('--series-1'), Vulnerable: token('--series-2'), New: token('--series-3') };
    const font = { family: token('--font-body'), size: 12 };
    const charts = new Map<string, QuotaChart>();
    for (const row of spaces) {
      if (!row.referenceNo) continue;
      const groups = CATEGORIES.flatMap((c) => this.quotaFor(row, c) ?? []);
      if (groups.length === 0) continue;
      charts.set(row.spaceId, {
        label: `${row.name}: ` + groups.map((g) => `${CATEGORY_LABEL[g.category]} ได้สิทธิ์ ${g.winners} โควตา ${g.base}`).join(' · '),
        data: {
          labels: groups.map((g) => CATEGORY_LABEL[g.category]),
          datasets: [
            {
              data: groups.map((g) => g.winners),
              backgroundColor: groups.map((g) => colour[g.category]),
              borderRadius: 4,
              barPercentage: 0.7,
              categoryPercentage: 1,
            },
          ],
        },
        options: {
          indexAxis: 'y',
          maintainAspectRatio: false,
          animation: false,
          plugins: {
            legend: { display: false },
            tooltip: { callbacks: { label: (item) => `ได้สิทธิ์ ${item.parsed.x} · โควตา ${groups[item.dataIndex].base}` } },
            annotation: {
              annotations: Object.fromEntries(
                groups.map((g, i): [string, AnnotationOptions<'line'>] => [
                  `quota-${g.category}`,
                  { type: 'line', xMin: g.base, xMax: g.base, yMin: i - 0.45, yMax: i + 0.45, borderColor: token('--ink'), borderWidth: 2 },
                ]),
              ),
            },
          },
          scales: {
            x: { min: 0, max, ticks: { stepSize: 1, precision: 0, font, color: token('--muted') }, grid: { color: token('--line') } },
            y: { ticks: { font, color: token('--muted') }, grid: { display: false } },
          },
        },
      });
    }
    return charts;
  });

  /** p-table hands rows to its templates untyped; this restores the type for the template checker. */
  protected asSpace(row: SpaceRow): SpaceRow {
    return row;
  }

  protected quotaFor(row: DashboardKpis['spaces'][number], c: Category) {
    return row.quota.find((q) => q.category === c) ?? null;
  }
}
