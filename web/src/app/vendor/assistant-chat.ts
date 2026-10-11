import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Api, AssistantSource, problemMessage } from '../core/api';

type Message = { from: 'me' | 'assistant'; text: string; found?: boolean; source?: AssistantSource };

/** Vendor Assistant: answers from the published rules only, with the file and section it used. */
@Component({
  selector: 'app-assistant-chat',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="chat" aria-labelledby="assistant-heading">
      <h1 id="assistant-heading">ถามผู้ช่วยเรื่องระเบียบ</h1>
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
          <button type="button" class="chip" (click)="ask(q)" [disabled]="busy()">{{ q }}</button>
        }
      </div>

      <form (submit)="$event.preventDefault(); ask(draft())">
        <label class="sr-only" for="assistant-question">คำถาม</label>
        <input
          id="assistant-question"
          type="text"
          maxlength="500"
          placeholder="พิมพ์คำถาม เช่น ต้องใช้เอกสารอะไร"
          [value]="draft()"
          (input)="draft.set($any($event.target).value)"
        />
        <button type="submit" [disabled]="busy() || draft().trim().length === 0">{{ busy() ? 'กำลังตอบ…' : 'ถาม' }}</button>
      </form>
    </section>
  `,
  styles: `
    .chat { background: var(--surface); border: 1px solid var(--line); border-radius: 8px; padding: 14px; }
    h1 { font-size: 1.1rem; margin: 0; }
    .hint { color: var(--muted); font-size: 0.85rem; margin: 2px 0 10px; }
    .messages { list-style: none; margin: 0 0 10px; padding: 0; display: flex; flex-direction: column; gap: 8px; }
    .messages li { white-space: pre-line; background: var(--accent-soft); border-radius: 8px; padding: 8px 10px; max-width: 92%; }
    .messages li.me { align-self: flex-end; background: var(--ground); }
    .messages li.not-found { background: var(--surface); border: 1px solid var(--line); color: var(--muted); }
    .source { display: block; margin-top: 4px; font-size: 0.75rem; color: var(--muted); }
    .suggestions { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 10px; }
    .chip { font: inherit; font-size: 0.85rem; padding: 4px 10px; border-radius: 999px; border: 1px solid var(--line); background: var(--surface); color: var(--ink); cursor: pointer; }
    form { display: flex; gap: 8px; }
    input { flex: 1; min-width: 0; font: inherit; padding: 8px 10px; border: 1px solid var(--line); border-radius: 6px; }
    form button { font: inherit; font-weight: 600; padding: 8px 14px; color: var(--surface); background: var(--accent); border: 0; border-radius: 6px; cursor: pointer; }
    button:disabled { opacity: 0.6; cursor: default; }
  `,
})
export class AssistantChat {
  private readonly api = inject(Api);

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
