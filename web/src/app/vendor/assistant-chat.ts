import { ChangeDetectionStrategy, Component, inject, model, signal } from '@angular/core';
import { Button } from 'primeng/button';
import { Panel } from 'primeng/panel';
import { InputText } from 'primeng/inputtext';
import { Api, AssistantSource, problemMessage } from '../core/api';

type Message = { from: 'me' | 'assistant'; text: string; found?: boolean; source?: AssistantSource };

/**
 * Vendor Assistant: answers from the published rules only, with the file and section it used. A panel the vendor
 * folds away; the page decides whether it starts open (it does until the vendor has applied this round).
 */
@Component({
  selector: 'app-assistant-chat',
  imports: [Button, InputText, Panel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="assistant-heading">
      <p-panel toggleable toggler="header" [collapsed]="!open()" (collapsedChange)="open.set(!$event)">
      <ng-template #header>
        <h2 id="assistant-heading">ถามผู้ช่วยเรื่องระเบียบ</h2>
      </ng-template>
      <p class="hint">ตอบจากระเบียบที่ประกาศเท่านั้น ไม่ตอบเรื่องผลของแต่ละคน</p>

      @if (messages().length > 0) {
        <ol class="messages" aria-live="polite">
          @for (m of messages(); track $index) {
            <li [class.me]="m.from === 'me'" [class.not-found]="m.found === false">
              {{ m.text }}
              @if (m.source) {
                <span class="source">{{ m.source === 'claude' ? 'ตอบโดย AI จากระเบียบที่ประกาศ' : 'ค้นจากหัวข้อระเบียบ' }}</span>
              }
            </li>
          }
        </ol>
      }

      <div class="suggestions">
        @for (q of suggestions; track q) {
          <!-- Buttons, not p-chip: each suggestion is an action -->
          <p-button rounded variant="outlined" severity="secondary" size="small" [label]="q" (onClick)="ask(q)" [disabled]="busy()" />
        }
      </div>

      <form (submit)="$event.preventDefault(); ask(draft())">
        <label class="sr-only" for="assistant-question">คำถาม</label>
        <input
          pInputText
          id="assistant-question"
          type="text"
          maxlength="500"
          placeholder="พิมพ์คำถาม เช่น ต้องใช้เอกสารอะไร"
          [value]="draft()"
          (input)="draft.set($any($event.target).value)"
        />
        <p-button type="submit" [label]="busy() ? 'กำลังตอบ…' : 'ถาม'" [disabled]="busy() || draft().trim().length === 0" />
      </form>
      </p-panel>
    </section>
  `,
  styles: `
    h2 { font-size: var(--text-h3); margin: 0; }
    .hint { color: var(--muted); font-size: var(--text-small); margin: 0 0 var(--space-3); }
    .messages { list-style: none; margin: 0 0 var(--space-3); padding: 0; display: flex; flex-direction: column; gap: var(--space-2); }
    .messages li { white-space: pre-line; background: var(--accent-soft); border-radius: 8px; padding: var(--space-2) var(--space-3); max-width: 92%; }
    .messages li.me { align-self: flex-end; background: var(--ground); }
    .messages li.not-found { background: var(--surface); border: 1px solid var(--line); color: var(--muted); }
    .source { display: block; margin-top: var(--space-1); font-size: var(--text-caption); color: var(--muted); }
    .suggestions { display: flex; flex-wrap: wrap; gap: var(--space-2); margin-bottom: var(--space-3); }
    form { display: flex; gap: var(--space-2); }
    input { flex: 1; min-width: 0; }
  `,
})
export class AssistantChat {
  private readonly api = inject(Api);

  /** Two-way: the page sets where it starts, the vendor's folding wins after that. */
  readonly open = model(true);

  protected readonly suggestions = ['ต้องใช้เอกสารอะไร', 'จัดสรรล็อคยังไง', 'ต้องยืนยันสิทธิ์ภายในกี่วัน'];
  protected readonly messages = signal<Message[]>([]);
  protected readonly draft = signal('');
  protected readonly busy = signal(false);

  protected ask(question: string): void {
    const q = question.trim();
    if (!q || this.busy()) return;
    this.busy.set(true);
    this.draft.set('');
    this.messages.update((m) => [...m, { from: 'me', text: q }]);
    this.api.askAssistant(q).subscribe({
      next: (a) => {
        this.busy.set(false);
        this.messages.update((m) => [...m, { from: 'assistant', text: a.text, found: a.found, source: a.source }]);
      },
      error: (err) => {
        this.busy.set(false);
        this.messages.update((m) => [...m, { from: 'assistant', text: problemMessage(err), found: false }]);
      },
    });
  }
}
