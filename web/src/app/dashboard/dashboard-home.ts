import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, from, map, of, startWith, switchMap } from 'rxjs';
import { Chart, ChartData, ChartOptions } from 'chart.js';
import annotationPlugin, { AnnotationOptions } from 'chartjs-plugin-annotation';
import { Button } from 'primeng/button';
import { Card } from 'primeng/card';
import { ChartModule } from 'primeng/chart';
import { Skeleton } from 'primeng/skeleton';
import { TableModule } from 'primeng/table';
import { Api, Category, DashboardKpis, DashboardSummary, Ratio } from '../core/api';
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
  imports: [Button, Card, ChartModule, Skeleton, TableModule],
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

  /** Bumped by "ลองอีกครั้ง" after a failed load. */
  private readonly retry = signal(0);
  private readonly loadKey = computed(() => ({ ok: this.canRead(), retry: this.retry() }));

  protected readonly kpis = toSignal(
    toObservable(this.loadKey).pipe(
      switchMap(({ ok }) =>
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

  protected reload(): void {
    this.retry.update((n) => n + 1);
  }

  /** The executive summary, asked for with the button and never made on load. */
  protected readonly summary = signal<
    { state: 'loading' } | { state: 'done'; data: DashboardSummary } | { state: 'error' } | null
  >(null);

  protected summarize(): void {
    this.summary.set({ state: 'loading' });
    this.api.dashboardSummary().subscribe({
      next: (data) => this.summary.set({ state: 'done', data }),
      error: () => this.summary.set({ state: 'error' }),
    });
  }

  /** One decimal on every percentage, so 71.0% and 88.6% read as the same kind of figure. */
  protected pct(r: Ratio): string {
    return r.percent === null
      ? '–'
      : `${r.percent.toLocaleString('th-TH', { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
  }

  /** Spaces with an approved run; the others have no quota to compare against. */
  protected readonly allocated = computed(() => (this.data()?.spaces ?? []).filter((s) => s.referenceNo !== null));

  /** The time the figures cover: each space's latest approved round, earliest start to latest end. */
  protected readonly scope = computed(() => {
    const rounds = this.allocated().filter((s) => s.startsOn && s.endsOn);
    if (rounds.length === 0) return null;
    const from = rounds.map((s) => s.startsOn!).sort()[0];
    const to = rounds.map((s) => s.endsOn!).sort().at(-1)!;
    return { from, to };
  });

  /** Column sums for the table footer: the same counts the KPI tiles divide (locks, winners, confirmed). */
  protected readonly totals = computed(() => {
    const spaces = this.data()?.spaces ?? [];
    const sum = (f: (s: SpaceRow) => number) => spaces.reduce((n, s) => n + f(s), 0);
    return {
      locks: sum((s) => s.locks),
      winners: sum((s) => s.winners),
      confirmed: sum((s) => s.confirmed),
      quota: CATEGORIES.map((c) => ({
        category: c,
        winners: sum((s) => this.quotaFor(s, c)?.winners ?? 0),
        base: sum((s) => this.quotaFor(s, c)?.base ?? 0),
      })),
    };
  });

  /**
   * Plain statements read off the figures above, shown only when the data says them; no targets, no scores.
   * The open-locks note only appears when the table's sums reconcile with the occupancy tile it explains.
   */
  protected readonly notes = computed(() => {
    const k = this.data();
    if (!k) return [];
    const notes: string[] = [];
    const t = this.totals();
    const open = t.locks - t.confirmed;
    if (open > 0 && t.locks === k.occupancy.of && t.confirmed === k.occupancy.count) {
      const parts = k.spaces.flatMap((s) => {
        if (!s.referenceNo) return [`${s.name} ยังไม่มีผลจัดสรรที่อนุมัติ ${s.locks} ล็อค`];
        const out: string[] = [];
        if (s.winners < s.locks) out.push(`${s.name} ไม่มีผู้ได้สิทธิ์ ${s.locks - s.winners} ล็อค`);
        if (s.confirmed < s.winners) out.push(`${s.name} รอผู้ได้สิทธิ์ยืนยัน ${s.winners - s.confirmed} ล็อค`);
        return out;
      });
      notes.push(`ล็อคที่ยังไม่มีผู้ยืนยันสิทธิ์ ${open} จาก ${t.locks} ล็อค: ${parts.join(' · ')}`);
    }
    const shifts = t.quota.filter((q) => q.winners !== q.base);
    if (shifts.length > 0) {
      notes.push('โควตาที่โอนข้ามกลุ่มเมื่อผู้สมัครไม่พอ: ' + shifts.map((q) => `${CATEGORY_LABEL[q.category]} ${this.shift(q)}`).join(' · '));
    }
    return notes;
  });

  /** The portal's wording for a group off its base quota; on an approved run the rollover is a fact, not a warning. */
  protected shift(q: { winners: number; base: number }): string {
    return q.winners > q.base ? `รับโอน +${q.winners - q.base}` : `โอนออก ${q.base - q.winners}`;
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
    const charts = new Map<string, QuotaChart>();
    for (const row of spaces) {
      if (!row.referenceNo) continue;
      const groups = CATEGORIES.flatMap((c) => this.quotaFor(row, c) ?? []);
      if (groups.length === 0) continue;
      const ink = token('--ink');
      charts.set(row.spaceId, {
        label: `${row.name}: ` + groups.map((g) => `${CATEGORY_LABEL[g.category]} ได้สิทธิ์ ${g.winners} โควตา ${g.base}`).join(' · '),
        data: {
          labels: groups.map((g) => CATEGORY_LABEL[g.category]),
          datasets: [
            {
              data: groups.map((g) => g.winners),
              backgroundColor: groups.map((g) => colour[g.category]),
              borderRadius: 4,
              barPercentage: 0.72,
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
                groups.flatMap((g, i): [string, AnnotationOptions][] => [
                  [
                    `quota-${g.category}`,
                    { type: 'line', xMin: g.base, xMax: g.base, yMin: i - 0.46, yMax: i + 0.46, borderColor: ink, borderWidth: 3 },
                  ],
                  // The figure sits just past whichever ends later, the bar or its quota mark, so neither covers it
                  [
                    `value-${g.category}`,
                    {
                      type: 'label',
                      xValue: Math.max(g.winners, g.base),
                      yValue: i,
                      xAdjust: 8,
                      position: { x: 'start', y: 'center' },
                      content: `${g.winners}/${g.base}`,
                      color: ink,
                      font: { family: token('--font-mono'), size: 13, weight: 500 },
                      padding: 0,
                    },
                  ],
                ]),
              ),
            },
          },
          scales: {
            // Values are labelled on the bars, so the axis carries no ticks; the headroom keeps the label inside the plot
            x: { min: 0, max: Math.ceil(max * 1.4), display: false },
            y: { ticks: { font: { family: token('--font-body'), size: 13 }, color: token('--muted') }, grid: { display: false }, border: { display: false } },
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
